# RulesGPU App – User Guide

*A quick-start reference for running DMN decision tables on your CPU / GPU*

---

## 1. Overview

RulesGPU App is a self-contained WPF desktop tool that lets you:

1. **Paste or type a DMN 1.3 decision table** (XML) in the **left pane**
2. **Paste or type test records** (CSV) in the **middle pane**
3. Click **Solve** to evaluate every record in parallel on your fastest available device (CUDA GPU if detected, otherwise CPU)
4. Inspect a **pretty-printed JSON report** for every record in the **right pane**

All computation is powered by the **RulesGPU** library (TorchSharp backend), so large rule sets and batches run far faster than a typical rules engine on the CPU.

---

## 2. Window Layout

| Area                                       | Purpose                                                                                                                                                                           | Notes                                                                                                      |
| ------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| **DMN Rules** – left pane                  | Write or paste a full `<definitions …>` DMN XML that contains *one* `<decisionTable>`.                                                                                            | The sample loan–eligibility table is loaded automatically at start-up.                                     |
| **Records (CSV)** – middle pane            | Supply one or more input rows. The **first row must be the header**; each header must exactly match the `<text>` element of a corresponding `<inputExpression>` in the DMN table. | Values are parsed as:<br>• numbers → `double`<br>• `true`/`false` → `bool`<br>• everything else → `string` |
| **Output** – right pane                    | Read-only box that shows one JSON object per record, headed by a `# Record N` marker.                                                                                             | Keys correspond to `<output name="…">` entries.                                                            |
| **Solve** – bottom button (height = 50 px) | Runs the engine.                                                                                                                                                                  | Resizes with the window; keyboard shortcut **Ctrl + Enter** also triggers the click.                       |

---

## 3. Quick Start

1. Start the app (⇢ **RulesGPUApp.exe**).
2. Review the default DMN loan-eligibility rules and four sample records.
3. Click **Solve**.
4. In the Output pane you will see :

```txt
# Record 1
{
  "Decision": "Declined (Underage)"
}

# Record 2
{
  "Decision": "Manual Review"
}

# Record 3
{
  "Decision": "Approved"
}

# Record 4
{
  "Decision": "Declined (Poor Credit)"
}
```

---

## 4. Supplying Your Own Data

### 4.1 DMN Rules

* **Must include exactly one `<decision>` element** (the first one found is used).
* Only **decision-table logic** is supported (literal expressions are ignored).
* Supported hit policies: **FIRST**, **UNIQUE**, **COLLECT** (others return *null*).
* Input types understood: `string`, `number`, `integer`, `boolean`, `date`, `datetime`.

### 4.2 CSV Records

* First row = **header** – names must match the `<text>` of each input clause.
* No quoting rules are enforced; commas split columns.
* Example template (fits the sample DMN):

```csv
Applicant Age,Applicant Income,Credit Score
25,45000,610
```

### 4.3 Batch Size

There is **no coded limit**; batches are streamed to the GPU in one go.
If you see out-of-memory errors on very large sets, split the CSV and evaluate in chunks.

---

## 5. Interpreting Results

* **Unique / First** – Output pane shows one dictionary per record or *null* if no rule matched.
* **Collect** – Output for each record is a JSON array of dictionaries, one per matching rule.
* Numeric outputs are returned as JSON numbers, booleans as `true`/`false`, dates as ISO-8601 strings.

---

## 6. Error Messages

| Message                    | Likely Cause                                         | Fix                                                     |
| -------------------------- | ---------------------------------------------------- | ------------------------------------------------------- |
| **“Could not parse DMN.”** | Malformed XML or missing `<decisionTable>`           | Validate DMN in an external editor.                     |
| **“Result null”**          | No rule matched & hit policy requires a single match | Check your rule conditions.                             |
| **`❌ Error:` …**           | Unhandled exception (stack trace shown)              | Copy details and open a GitHub issue / debug DMN & CSV. |

---

## 7. Keyboard Shortcuts

| Shortcut         | Action                    |
| ---------------- | ------------------------- |
| **Ctrl + Enter** | Run **Solve**             |
| **Ctrl + L**     | Select all in DMN pane    |
| **Ctrl + K**     | Select all in CSV pane    |
| **Ctrl + O**     | Select all in Output pane |

*(Shortcuts use the focused pane’s commands in addition to the global ones above.)*

---

## 8. Tips & Tricks

* **Copy/Paste friendly** – all panes accept plain text; no file dialogs keep the UI minimal.
* **GPU indicator** – if a CUDA-capable device is available, computations run there automatically; otherwise they fall back to CPU with no user action required.
* **Live editing** – change rules or data and click **Solve** again; everything is re-parsed each run, so no restart needed.
* For **string literals** in DMN, remember to wrap them in quotes, e.g. `""Approved""`.
* Use **“-”** (dash) in an `<inputEntry>` to mark “don’t care” conditions.

---

## 9. Troubleshooting

1. **Blank Output pane** → ensure headers in CSV exactly match DMN input expressions.
2. **Parsing succeeds but outputs are incorrect** → verify data types in DMN `typeRef` attributes; mismatched `number` vs `string` can silently mis-compare.
3. **Large tables feel slow on CPU** → install a CUDA-enabled GPU and the TorchSharp-cuda package already referenced by the project; the app will pick it up automatically.

---

Happy rule-crunching!
