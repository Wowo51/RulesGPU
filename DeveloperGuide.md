# RulesGPU Developer Guide

*Version: 2025-06-18*
*Applies to solution root **`RulesGPU/src`***

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Solution Layout](#solution-layout)
3. [Building the Solution](#building-the-solution)
4. [Quick Start](#quick-start)
5. [The Core Libraries](#the-core-libraries)
   5.1 [RulesDMN – DMN model & parser](#rulesdmn)
   5.2 [RulesData – synthetic data & helpers](#rulesdata)
   5.3 [RulesGPU – GPU decision-table engine](#rulesgpu)
6. [Running on the GPU](#running-on-the-gpu)
7. [Generating & Testing Synthetic Rule Sets](#generating--testing-synthetic-rule-sets)
8. [Desktop Demo App](#desktop-demo-app)
9. [Unit-test Projects](#unit-test-projects)
10. [Extending the Engine](#extending-the-engine)
11. [Troubleshooting](#troubleshooting)
12. [Contributing & Code Style](#contributing--code-style)
13. [License](#license)

---

## Project Overview

**RulesGPU** is a .NET 9 decision-table rules engine that interprets Decision Model & Notation (DMN 1.3) *decision tables* and executes them in parallel on either CPU or CUDA GPUs (via **TorchSharp**).
It provides:

* A 100 % managed DMN object model & streaming parser (`RulesDMN`)
* Conversion of DMN tables to dense Torch tensors (`RulesGPU`)
* A GPU-accelerated evaluator that supports the **Unique**, **First** and **Collect** hit policies
* Synthetic rule/data generators for fuzzing & performance tests (`RulesData`)
* 300+ MSTest unit tests (CPU & GPU back-ends)
* A minimal WPF demo (**RulesGPUApp**) to paste DMN XML + CSV records and view results

The entire engine is dependency-free except for `TorchSharp-cuda-windows` and the MSTest runner.

---

## Solution Layout

```
RulesGPU.sln
│
├── RulesDMN/         // DMN models & parser
├── RulesData/        // Data generators & helpers
├── RulesGPU/         // GPU engine
│    └── ...Converter.cs
│    └── ...Engine.cs
│
├── RulesDMNTest/     // Unit tests for parser
├── RulesDataTest/    // Unit tests for generators
├── RulesGPUTest/     // Unit tests for GPU engine
│
└── RulesGPUApp/      // WPF desktop demo
```

All projects target **`net9.0`** (desktop projects target `net9.0-windows`).

---

## Building the Solution

1. **Prerequisites**

   * .NET SDK 9 (preview 5+).
   * Visual Studio 2022 17.14+ with **.NET desktop & UWP workloads**.
   * Optional: CUDA 11.8 / 12.x drivers for GPU execution (TorchSharp automatically selects CUDA if `torch.cuda.is_available()`).

2. **Restore & build**

   ```bash
   dotnet restore src/RulesGPU.sln
   dotnet build   src/RulesGPU.sln -c Release
   ```

   > Building on a machine **without** a CUDA-capable GPU is fine – TorchSharp will fall back to CPU.

3. **Run all unit tests**

   ```bash
   dotnet test src/RulesGPU.sln -c Release -m:2
   ```

---

## Quick Start

### Evaluate a DMN decision table from C\#

```csharp
using torch = TorchSharp.torch;
using TorchSharp;
using RulesDMN;
using RulesGPU;

string dmnXml = File.ReadAllText("LoanEligibility.dmn");

var device   = torch.cuda.is_available()
             ? new Device(DeviceType.CUDA)
             : new Device(DeviceType.CPU);

using var engine = new DmnGpuEngine(device);
bool ok = engine.LoadDmnDecisionTable(dmnXml);
if (!ok) throw new InvalidOperationException("No decision table found.");

var inputs = new Dictionary<string, object>
{
    ["Applicant Age"]    = 30,
    ["Applicant Income"] = 50_000,
    ["Credit Score"]     = 720
};

var result = (IReadOnlyDictionary<string, object>?) engine.Evaluate(inputs);
Console.WriteLine(result?["Decision"]);  // → Approved
```

---

## The Core Libraries

### <a id="rulesdmn"></a>5.1 **RulesDMN** – DMN parser & model

| Folder / File  | Purpose                                                                                                                                  |
| -------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `Models/`      | Plain-old CLR objects (POCOs) for DMN elements (`DecisionTable`, `Rule`, …). No external dependencies.                                   |
| `HitPolicy.cs` | Enumeration mirroring DMN spec.                                                                                                          |
| `DmnParser.cs` | Single-pass, namespace-agnostic XML parser. Gracefully tolerates partial / invalid files – returns `null` if critical nodes are missing. |

Supported DMN subset

* Decision tables (`<decisionTable>`) with nested `<input>`, `<output>`, `<rule>` nodes
* Literal expressions (`<literalExpression>`) for simple calculated decisions
* Hit policies: **UNIQUE**, **FIRST**, **COLLECT** (+ default **UNIQUE** if omitted)

### <a id="rulesdata"></a>5.2 **RulesData** – Synthetic test generators

* **`DataTypeGeneratorFactory`** maps FEEL type refs → `IDataTypeGenerator` implementations
* **`DecisionTableFactory`** quickly constructs *random* decision tables for fuzzing.
* **`SyntheticInputGenerator`** produces random input records conforming to a given table.
* **`SyntheticProblemSolutionPairGenerator`** *inverts* a rule: chooses an existing rule and synthesises an input record guaranteed to hit it, thereby yielding an exact problem/solution pair for regression tests.

### <a id="rulesgpu"></a>5.3 **RulesGPU** – GPU decision-table engine

```
                     +------------------------------+
DMN XML ──parse────► | DMN Models (RulesDMN)        |
                     +------------------------------+
                              │
                         convert (CPU)
                              ▼
                     +------------------------------+
                     | DmnToGpuConverter            |      Torch tensors
                     |   • maps strings→ints        |  ──────────────────► GPU/CPU
                     |   • builds condition masks   |
                     +------------------------------+
                              │
                              ▼
                     +------------------------------+
                     | GpuDecisionTableRepresentation| (immutable; disposes tensors)
                     +------------------------------+
                              │
                         evaluate (GPU)
                              ▼
                     +------------------------------+
                     | RulesGPUEngine              |
                     +------------------------------+
```

* **`StringValueEncoder`** – bijective mapping of strings ↔ small ints (stored as doubles in the tensor grid).
* Input tensor dimensions: *(rules × inputs)*; output tensor: *(rules × outputs)*
* Per-cell metadata: value, comparison operator, “don’t-care” mask.
* Evaluation uses element-wise broadcasting and boolean reductions entirely on the GPU – no per-record kernel launches.
* Hit-policy fan-out handled on CPU after boolean mask → rule-indices.

---

## Running on the GPU

The evaluator autodetects CUDA:

```csharp
var device = torch.cuda.is_available()
           ? new Device(DeviceType.CUDA)
           : new Device(DeviceType.CPU);
```

**Performance tips**

| Scenario                  | Recommendation                                                                                                                                                 |
| ------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Large rule sets (10 k+)   | Use `HitPolicy.Collect` sparingly – post-processing large result sets can dominate runtime.                                                                    |
| Many small record batches | Batch inputs with `Evaluate(IEnumerable<IDictionary<string,object>> records)` to avoid kernel-launch overhead.                                                 |
| String-heavy tables       | Call `DmnGpuEngine.LoadDmnDecisionTable` once and reuse the same engine for all evaluations – encoder cache is stored inside `GpuDecisionTableRepresentation`. |

---

## Generating & Testing Synthetic Rule Sets

```csharp
DecisionTable dt = DecisionTableFactory.CreateRandomDecisionTable(
        minInputs:2, maxInputs:4,
        minOutputs:1, maxOutputs:2,
        minRules:3, maxRules:6);

var (prob,sol) = SyntheticProblemSolutionPairGenerator.GeneratePair(dt).Value;

using var gpu = DmnToGpuConverter.ConvertDecisionTableToGpuRepresentation(dt, device);
var result = new RulesGPUEngine(device).Evaluate(gpu, prob);

Debug.Assert(sol.SequenceEqual((IReadOnlyDictionary<string,object>)result));
```

`RulesGPUTest/RulesEngineTests.cs` runs **10 000** such pairs (100 × 100) on every build.

---

## Desktop Demo App

**Project `RulesGPUApp`** – a single-file WPF window:

| Pane              | Purpose                                                                                                  |
| ----------------- | -------------------------------------------------------------------------------------------------------- |
| **DMN Rules**     | Paste or edit DMN XML (only first decision table is evaluated).                                          |
| **Records (CSV)** | Each subsequent line after the header row is a record. Columns mapped by header names → input variables. |
| **Output**        | Pretty-printed JSON result set.                                                                          |

Click **Solve** (or press **F5** in VS) to watch GPU evaluation in realtime.
Default data: a loan-eligibility table and four sample applicants.

---

## Unit-test Projects

| Project           | Focus                                            | Runtime                |
| ----------------- | ------------------------------------------------ | ---------------------- |
| **RulesDMNTest**  | Parser edge-cases & spec conformance             | < 1 s                  |
| **RulesDataTest** | Generator correctness & probability corner cases | < 1 s                  |
| **RulesGPUTest**  | Engine correctness (CPU & GPU), fuzzing suites   | 8–15 s CPU / 3–5 s GPU |

To restrict GPU tests to CPU-only machines, `TestUtils.IsCudaAvailable()` automatically skips CUDA-specific asserts.

---

## Extending the Engine

1. **New FEEL types**

   * Add an `IDataTypeGenerator` and register in `DataTypeGeneratorFactory`.
   * Extend `DmnToGpuConverter.ParseInputLiteralAndOperator` & `ParseOutputLiteral` with encoding logic.
2. **Additional hit policies** (`ANY`, `PRIORITY`, …)

   * Implement in `RulesGPUEngine.ApplyHitPolicy` (single) and `ApplyHitPolicyBatch` (batch).
3. **Custom operators** (`between`, `in`)

   * Extend `ComparisonOperator` enumeration and comparison sections in `RulesGPUEngine.Evaluate`.

---

## Troubleshooting

| Symptom                                        | Fix                                                                                                                       |
| ---------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `TorchSharp.Exceptions...: CUDA not available` | Ensure correct driver version (≥ r550), check `torch.cuda.is_available()` in immediate window.                            |
| Engine returns `null` for every record         | Likely no rule matched **and** hit policy is `UNIQUE`/`FIRST`. Double-check input header names vs. DMN input expressions. |
| `System.AccessViolationException` at disposal  | Always `Dispose()` `GpuDecisionTableRepresentation` **after** every evaluation or wrap in `using`.                        |
| Large strings incorrectly decoded              | Check that the same `StringValueEncoder` instance is used for encode **and** decode (converter passes it through).        |

---
