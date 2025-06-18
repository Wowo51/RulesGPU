## RulesGPU Help & Architecture Guide

*(v1 — June 2025)*

---

### Table of Contents

1. [Introduction](#introduction)
2. [High-level Architecture](#high-level-architecture)
3. [Project Structure](#project-structure)
4. [Decision-Model & Notation (DMN) Support](#dmn-support)
5. [GPU-Accelerated Rule Evaluation](#gpu-accelerated-rule-evaluation)
6. [String & Date Encoding Strategy](#encoding-strategy)
7. [Synthetic Data & Inverse Problem Generation](#synthetic-data--inverse-problem-generation)
8. [Unit-Testing Strategy](#unit-testing-strategy)
9. [WPF Demo App (`RulesGPUApp`)](#wpf-demo-app)
10. [Building & Running](#building--running)
11. [Extending the Engine](#extending-the-engine)
12. [Performance, Limits & Tips](#performance-limits--tips)
13. [Troubleshooting](#troubleshooting)
14. [License & Attribution](#license--attribution)

---

<a id="introduction"></a>

### 1  Introduction

**RulesGPU** is an end-to-end sample showing how a traditional business-rules engine can be expressed in DMN, compiled to TorchSharp **tensors**, and evaluated in parallel on **NVIDIA CUDA** (or CPU fallback).
The repo includes:

* **DMN parser** (100 % managed, no external DMN library).
* **TorchSharp–powered evaluator** with batched inference and multiple hit-policies.
* **Synthetic data generator** able to produce *inverse* problem/solution pairs for fuzz-testing.
* **Full MSTest suite** (CPU & CUDA).
* **WPF front-end** for interactive exploration.

---

<a id="high-level-architecture"></a>

### 2  High-level Architecture

```text
+-------------------+       +------------------------+       +-------------------------+
|   DMN XML Input   |  -->  |  RulesDMN (Parser &    |  -->  | DmnToGpuConverter       |
| (definitions/     |       |  Plain Objects)        |       |  • maps schema -> idx   |
| decision tables)  |       +------------------------+       |  • builds torch.Tensors |
+-------------------+                                       +-----------+-------------+
                                                                                       |
                                                                                       v
+-------------------+       +------------------------+       +-------------------------+
|  CSV / JSON /     |  -->  |  RulesGPU Engine       |  ...  |  batched GPU evaluation |
|  Dictionary input |       |  • tensorise input     |       |  (PyTorch kernels via   |
+-------------------+       |  • per rule comparison |       |   TorchSharp)           |
                            +------------------------+       +-----------+-------------+
                                                                                       |
                                                                                       v
                                  +--------------------+    aggregate according to hit-policy
                                  |   Output Objects   |
                                  +--------------------+
```

---

<a id="project-structure"></a>

### 3  Project Structure

| Folder / Project                                | Purpose                                                                                                                                                                                                                                                                |
| ----------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **RulesDMN**                                    | POCO model + `DmnParser.cs` (streaming LINQ-to-XML parser, DMN 1.3 subset).                                                                                                                                                                                            |
| **RulesData**                                   | Random data & decision-table generators (`DecisionTableFactory`, `SyntheticProblemSolutionPairGenerator` etc.).                                                                                                                                                        |
| **RulesGPU**                                    | GPU runtime:<br>  • `DmnToGpuConverter` – flattens DMN → tensors.<br>  • `GpuDecisionTableRepresentation` – owns GPU memory.<br>  • `RulesGPUEngine` – batched comparison kernel + hit-policy aggregator.<br>  • `DmnGpuEngine` – façade for loading XML & evaluating. |
| **RulesGPUApp**                                 | WPF demo with three panes (rules / records / output) & *Solve* button.                                                                                                                                                                                                 |
| **RulesDMNTest / RulesDataTest / RulesGPUTest** | 300+ MSTest cases; CUDA auto-skipped if unavailable.                                                                                                                                                                                                                   |

---

<a id="dmn-support"></a>

### 4  Decision-Model & Notation (DMN) Support

* **Subset:** Decision Tables, Literal Expressions, hit policies **UNIQUE**, **FIRST**, **COLLECT**, **ANY** (Unique/First/Collect implemented, others stubbed).
* **Type system:** `string`, `number`, `integer`, `boolean`, `date`, `datetime` (ISO 8601 form).
* **Expressions:** literal values and simple inline conditions (`>= 18`, `"foo"`, `-` for “don’t-care”).

Parsing is entirely single-pass; XPath-free, reflection-free, so startup cost is trivial.

---

<a id="gpu-accelerated-rule-evaluation"></a>

### 5  GPU-Accelerated Rule Evaluation

| Step                       | Detail                                                                                                                                                                                                                                     |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Tensor layout**          | `(numRules, numInputs)` tensors for **condition values** (FP64), **operator ids** (`int64`), **mask** (`bool`), plus `(numRules, numOutputs)` for outputs.                                                                                 |
| **Operator encoding**      | Enum `ComparisonOperator` → `int64` (Equal, GT, GE, LT, LE, NotEqual).                                                                                                                                                                     |
| **String & Date handling** | Strings encoded to stable ints via `StringValueEncoder`; dates → `DateTime.ToOADate()` doubles.                                                                                                                                            |
| **Kernel**                 | All comparisons are performed in one broadcasted tensor expression—no explicit for-loops on GPU. *NaN* semantics emulate DMN “null”.                                                                                                       |
| **Hit-policy phase**       | After `(batch, rules, inputs)` boolean tensor is reduced (`Tensor.all(dim:2)`), the 1D “rule-fired” mask is aggregated:<br>  • **FIRST / UNIQUE:** pick first or ensure singleton.<br>  • **COLLECT:** `index_select` gathers all winners. |
| **Batching**               | Caller may supply *N* heterogeneous records; engine pads into `(N, inputs)` tensor → thousands of rows per CUDA launch.                                                                                                                    |

*Fallback*: if CUDA unavailable TorchSharp silently routes to CPU; tensors remain, no `#ifdef`.

---

<a id="encoding-strategy"></a>

### 6  String & Date Encoding Strategy

1. **At import:** every unique string literal adds an **id** (incrementing int) ­→ stored in tensors as double (`id`).
2. **At runtime:** incoming strings are looked-up; unknown values encode to **NaN**, ensuring they fail all equality comparisons (consistent with DMN null semantics).
3. **Back-conversion:** output tensor double → type-ref aware decode (string lookup, rounding ints, `DateTime.FromOADate`).

Using doubles avoids type heterogeneity inside a single tensor and keeps GPU arithmetic simple.

---

<a id="synthetic-data--inverse-problem-generation"></a>

### 7  Synthetic Data & Inverse Problem Generation

* `DecisionTableFactory` ― randomises inputs/outputs/types/rules with controllable bounds.
* `SyntheticProblemSolutionPairGenerator` picks **one rule**, ■ copies its outputs, ■ creates an *inverse* input satisfying each guard:

  * numeric operators generate random offsets (e.g. `> 10` → `10 + ε`).
  * dash (`-`) chooses random value from generator for that type.
* 100×100 stress test in **`RulesGPUTest.Test_RandomRuleSets_WithInverseProblemGeneration`** proves round-trip correctness.

---

<a id="unit-testing-strategy"></a>

### 8  Unit-Testing Strategy

* **CPU & CUDA** branches both hit by tests; helper `TestUtils.IsCudaAvailable()` disables GPU-only assertions on hardware-less CI.
* Coverage includes:

  * DMN parsing corner-cases (missing IDs, empty text, malformed XML).
  * Generator factory choices & type fall-backs.
  * GPU comparison logic across numeric/boolean/string/date.
  * Batched evaluation and hit-policy semantics.
  * 10 000 randomly generated problem/solution pairs per run (100 rule-sets × 100 pairs).

---

<a id="wpf-demo-app"></a>

### 9  WPF Demo App (`RulesGPUApp`)

* **Left pane** – DMN XML (editable).
* **Middle pane** – CSV records (headers = input names).
* **Right pane** – JSON output per record.
* **Solve** button (50 px tall) calls `RulesGPUEngine` on GPU or CPU.
* Default DMN = *Loan Eligibility* example; default CSV = 4 sample applicants.

Minimal code-behind keeps UI thin; heavy lifting is still in core library.

---

<a id="building--running"></a>

### 10  Building & Running

```bash
git clone https://github.com/your-org/RulesGPU.git
cd RulesGPU/src
dotnet restore        # pulls TorchSharp-cuda-windows 0.105.0 (~2 GB)
dotnet test           # runs full MSTest suite
dotnet run -p RulesGPUApp  # launches WPF app (Windows only)
```

> **CUDA note** – TorchSharp detects GPU at runtime; ensure CUDA 11.x drivers present.
> **.NET 9 preview** is used; install latest **.NET SDK 9** (*works on 8 with minor csproj tweaks*).

---

<a id="extending-the-engine"></a>

### 11  Extending the Engine

| Need                                            | Where to change                                                                                                    |
| ----------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| **Add hit-policy** (e.g. `PRIORITY`)            | `RulesGPUEngine.ApplyHitPolicy*` – implement aggregation.                                                          |
| **Support ‘between’ or OR conditions**          | Enhance `DmnToGpuConverter.ParseInputLiteralAndOperator` to parse ranges; store two value tensors & operator code. |
| **New FEEL type**                               | Add generator → `RulesData.Generators`, update converters & value encoder.                                         |
| **Vector-valued outputs**                       | Increase `OutputClause` mapping; current tensors already N×M, so multi-column safe.                                |
| **Different front-end (Blazor, console, etc.)** | Reuse `DmnGpuEngine` façade – zero GPU code exposed.                                                               |

---

<a id="performance-limits--tips"></a>

### 12  Performance, Limits & Tips

* Throughput scales \~linearly with **numRecords × numRules × numInputs**; best gains when you batch thousands of records.
* **FP64** used for safety; switch to **FP32** by passing `dtype: float32` if memory-bound.
* String encoding table resides on **host**; encoded tensors on GPU → minimal PCIe traffic (ints only).
* Decision tables > 2 000 rules may hit GPU register pressure; consider splitting or columnar evaluation.

---

<a id="troubleshooting"></a>

### 13  Troubleshooting

| Symptom                  | Hint                                                                                      |
| ------------------------ | ----------------------------------------------------------------------------------------- |
| *TorchSharp DllNotFound* | Verify `torch_cuda.dll` in output; mismatch between driver & TorchSharp build.            |
| *All results null*       | No rule matched (Unique/First) **or** input key names differ from `<text>` labels in DMN. |
| *Memory leak warnings*   | Missing `using`/`Dispose()` on `GpuDecisionTableRepresentation`; wrap in `using` blocks.  |
| *Slow CPU path*          | Ensure `cuda.is_available()` returns *true*; otherwise engine runs on AVX.                |
| *Date comparisons wrong* | Confirm DMN uses `date("YYYY-MM-DD")`; time components require `date and time(...)`.      |

---

*Happy GPU-accelerated rule crunching!*
