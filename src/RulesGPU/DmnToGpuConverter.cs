//Copyright Warren Harding 2025.
using RulesDMN.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TorchSharp;
using static TorchSharp.torch;

namespace RulesGPU
{
    /// <summary>
    /// Converts a DMN DecisionTable into a GPU-accelerated representation using TorchSharp tensors.
    /// This converter parses simple equality and range comparisons for input entries and literal output values.
    /// </summary>
    public static class DmnToGpuConverter
    {
        public static GpuDecisionTableRepresentation ConvertDecisionTableToGpuRepresentation(DecisionTable decisionTable, Device device)
        {
            if (decisionTable is null)
            {
                // Return an empty representation if the decision table is null, instead of throwing.
                return new GpuDecisionTableRepresentation(
                    new Dictionary<string, int>(),
                    new Dictionary<string, int>(),
                    new Dictionary<string, string>(), // Add empty outputTypeRefs
                    zeros(0, 0, dtype: float64, device: device),
                    zeros(0, 0, dtype: int64, device: device),
                    zeros(0, 0, dtype: ScalarType.Bool, device: device),
                    zeros(0, 0, dtype: float64, device: device),
                    HitPolicy.Unique, // Default hit policy for an empty table
                    new StringValueEncoder());
            }

            StringValueEncoder stringEncoder = new StringValueEncoder();

            // 1. Map input and output names to indices
            Dictionary<string, int> inputMapping = new Dictionary<string, int>();
            Dictionary<string, int> outputMapping = new Dictionary<string, int>();
            Dictionary<string, string> inputTypeRefs = new Dictionary<string, string>(); // input variable name -> typeRef
            Dictionary<string, string> outputTypeRefs = new Dictionary<string, string>(); // output variable name -> typeRef

            int inputIdx = 0;
            foreach (InputClause inputClause in decisionTable.Inputs)
            {
                if (inputClause.Expression is LiteralExpression le && !string.IsNullOrEmpty(le.Text))
                {
                    inputMapping[le.Text] = inputIdx++;
                    inputTypeRefs[le.Text] = le.TypeRef;
                }
            }

            int outputIdx = 0;
            foreach (OutputClause outputClause in decisionTable.Outputs)
            {
                if (!string.IsNullOrEmpty(outputClause.Name))
                {
                    outputMapping[outputClause.Name] = outputIdx++;
                    outputTypeRefs[outputClause.Name] = outputClause.TypeRef;
                }
            }

            int numRules = decisionTable.Rules.Count;
            int numInputs = inputMapping.Count;
            int numOutputs = outputMapping.Count;

            // Handle edge case: no inputs or no rules
            if (numInputs == 0 || numRules == 0)
            {
                return new GpuDecisionTableRepresentation(
                    inputMapping,
                    outputMapping,
                    outputTypeRefs, // Add outputTypeRefs here too
                    zeros(0, 0, dtype: float64, device: device), // Empty inputConditionValues
                    zeros(0, 0, dtype: int64, device: device),   // Empty inputConditionComparisonOperators
                    zeros(0, 0, dtype: ScalarType.Bool, device: device), // Empty inputConditionMask
                    zeros(0, 0, dtype: float64, device: device), // Empty outputValues
                    decisionTable.HitPolicy,
                    stringEncoder);
            }

            // 2. Initialize tensors
            Tensor inputConditionValues = zeros(numRules, numInputs, dtype: float64, device: device);
            // Comparison operator enum values will be stored as int64
            Tensor inputConditionOperators = zeros(numRules, numInputs, dtype: int64, device: device);
            // True if a condition exists and is parsed, false for "don't care" or unparseable expressions
            Tensor inputConditionMask = zeros(numRules, numInputs, dtype: ScalarType.Bool, device: device); 
            Tensor outputValues = zeros(numRules, numOutputs, dtype: float64, device: device);

            // Create reverse lookups for easy access by index
            // Ensure inputs are ordered by their mapped index
            string[] inputNamesByIndex = inputMapping.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToArray();
            string[] outputNamesByIndex = outputMapping.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToArray();

            // 3. Populate tensors with rule data
            for (int r = 0; r < numRules; r++)
            {
                Rule rule = decisionTable.Rules[r];

                // Input Entries
                for (int i = 0; i < rule.InputEntries.Count; i++)
                {
                    InputEntry inputEntry = rule.InputEntries[i]; // This corresponds to the InputClause order
                    if (i >= numInputs) continue; // Safety check in case inputEntry count is more than mapped inputs

                    string inputVariableName = inputNamesByIndex[i]; // Get the variable name for this input column
                    string typeRef = inputTypeRefs.TryGetValue(inputVariableName, out string? type) ? type : "string"; // Default to string if typeRef not found

                    if (string.IsNullOrWhiteSpace(inputEntry.Text) || inputEntry.Text.Trim() == "-")
                    {
                        // This input entry is "don't care", mask it out. Mask is false.
                        inputConditionMask[r, i] = tensor(false, dtype: ScalarType.Bool, device: device);
                        // Default values for 'don't care' are not used, but set to something consistent
                        inputConditionValues[r, i] = tensor(0.0, dtype: float64, device: device);
                        inputConditionOperators[r, i] = tensor((long)ComparisonOperator.Equal, dtype: int64, device: device);
                    }
                    else
                    {
                        (double? parsedVal, ComparisonOperator op) = ParseInputLiteralAndOperator(inputEntry.Text, typeRef, stringEncoder);
                        if (parsedVal.HasValue)
                        {
                            inputConditionValues[r, i] = tensor(parsedVal.Value, dtype: float64, device: device);
                            inputConditionOperators[r, i] = tensor((long)op, dtype: int64, device: device);
                            inputConditionMask[r, i] = tensor(true, dtype: ScalarType.Bool, device: device);
                        }
                        else
                        {
                            // Parsing failed or unsupported type/expression, treat as "don't care"
                            inputConditionMask[r, i] = tensor(false, dtype: ScalarType.Bool, device: device);
                            inputConditionValues[r, i] = tensor(0.0, dtype: float64, device: device);
                            inputConditionOperators[r, i] = tensor((long)ComparisonOperator.Equal, dtype: int64, device: device);
                        }
                    }
                }

                // Output Entries
                for (int o = 0; o < rule.OutputEntries.Count; o++)
                {
                    OutputEntry outputEntry = rule.OutputEntries[o];
                    if (o >= numOutputs) continue; // Safety check

                    string outputVariableName;
                    if (o < outputNamesByIndex.Length)
                    {
                        outputVariableName = outputNamesByIndex[o];
                    }
                    else
                    {
                        continue; // Invalid output index, skip this output entry
                    }

                    string outputVariableTypeRef = outputTypeRefs.TryGetValue(outputVariableName, out string? type) ? type : "string";
                    // Output parsing is simpler, no operators, just literal values
                    double outputValue = ParseOutputLiteral(outputEntry.Text, outputVariableTypeRef, stringEncoder);
                    outputValues[r, o] = tensor(outputValue, dtype: float64, device: device);
                }
            }

            return new GpuDecisionTableRepresentation(
                inputMapping,
                outputMapping,
                outputTypeRefs, // Pass outputTypeRefs here
                inputConditionValues,
                inputConditionOperators,
                inputConditionMask,
                outputValues,
                decisionTable.HitPolicy,
                stringEncoder);
        }

        /// <summary>
        /// Parses a string literal value including potential comparison operators for input conditions.
        /// </summary>
        /// <param name="text">The string to parse (e.g., "> 10", "true", ""Value"").</param>
        /// <param name="typeRef">The expected type reference (e.g., "number", "boolean", "string").</param>
        /// <param name="encoder">The StringValueEncoder for string types.</param>
        /// <returns>A tuple containing the parsed double value (or null if unparseable) and the detected ComparisonOperator.</returns>
        private static (double? parsedValue, ComparisonOperator op) ParseInputLiteralAndOperator(
            string text, string typeRef, StringValueEncoder encoder)
        {
            string cleanedText = text.Trim();
            ComparisonOperator op = ComparisonOperator.Equal; // Default operator
            string valueOnlyText = cleanedText;

            // Handle NotEqual operator for all types first, as it's a general comparison
            if (valueOnlyText.StartsWith("!=", StringComparison.Ordinal))
            {
                op = ComparisonOperator.NotEqual;
                valueOnlyText = valueOnlyText.Substring(2).Trim();
            }
            else if (valueOnlyText.StartsWith("<>", StringComparison.Ordinal)) // Another common "not equal" syntax
            {
                op = ComparisonOperator.NotEqual;
                valueOnlyText = valueOnlyText.Substring(2).Trim();
            }
            // Handle numeric/date comparison operators, these are type-specific for ordering
            else if (typeRef.Equals("number", StringComparison.OrdinalIgnoreCase) ||
                     typeRef.Equals("integer", StringComparison.OrdinalIgnoreCase) ||
                     typeRef.Equals("date", StringComparison.OrdinalIgnoreCase) ||
                     typeRef.Equals("datetime", StringComparison.OrdinalIgnoreCase))
            {
                if (valueOnlyText.StartsWith(">=", StringComparison.Ordinal)) { op = ComparisonOperator.GreaterThanOrEqual; valueOnlyText = valueOnlyText.Substring(2).Trim(); }
                else if (valueOnlyText.StartsWith("<=", StringComparison.Ordinal)) { op = ComparisonOperator.LessThanOrEqual; valueOnlyText = valueOnlyText.Substring(2).Trim(); }
                else if (valueOnlyText.StartsWith(">", StringComparison.Ordinal)) { op = ComparisonOperator.GreaterThan; valueOnlyText = valueOnlyText.Substring(1).Trim(); }
                else if (valueOnlyText.StartsWith("<", StringComparison.Ordinal)) { op = ComparisonOperator.LessThan; valueOnlyText = valueOnlyText.Substring(1).Trim(); }
                else if (valueOnlyText.StartsWith("=", StringComparison.Ordinal)) { op = ComparisonOperator.Equal; valueOnlyText = valueOnlyText.Substring(1).Trim(); }
            }

            // Remove DMN function wrappers and quotes AFTER operator parsing
            if (typeRef.Equals("string", StringComparison.OrdinalIgnoreCase) && valueOnlyText.StartsWith("\"") && valueOnlyText.EndsWith("\"") && valueOnlyText.Length > 1)
            {
                valueOnlyText = valueOnlyText.Substring(1, valueOnlyText.Length - 2);
            }
            else if (typeRef.Equals("date", StringComparison.OrdinalIgnoreCase) && valueOnlyText.StartsWith("date(\"") && valueOnlyText.EndsWith("\")"))
            {
                 valueOnlyText = valueOnlyText.Substring("date(\"".Length, valueOnlyText.Length - "date(\"".Length - "\")".Length);
            }
            else if (typeRef.Equals("datetime", StringComparison.OrdinalIgnoreCase) && valueOnlyText.StartsWith("date and time(\"") && valueOnlyText.EndsWith("\")"))
            {
                 valueOnlyText = valueOnlyText.Substring("date and time(\"".Length, valueOnlyText.Length - "date and time(\"".Length - "\")".Length);
            }

            switch (typeRef.ToLowerInvariant())
            {
                case "number":
                case "integer":
                    if (double.TryParse(valueOnlyText, NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleVal))
                    {
                        return (doubleVal, op);
                    }
                    break;
                case "boolean":
                    if (bool.TryParse(valueOnlyText, out bool boolVal))
                    {
                        // For boolean, ComparisonOperator.Equal or NotEqual is derived purely from original text prefixes.
                        return (boolVal ? 1.0 : 0.0, op);
                    }
                    break;
                case "date":
                case "datetime":
                    if (DateTime.TryParse(valueOnlyText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dtValue))
                    {
                        // For dates, ComparisonOperator.Equal, NotEqual, GreaterThan/Equal, LessThan/Equal derived from prefixes.
                        return (dtValue.ToOADate(), op); // Use ToOADate() as a consistent double representation
                    }
                    break;
                case "string":
                    // For string, ComparisonOperator.Equal or NotEqual is derived purely from original text prefixes.
                    return ((double)encoder.AddAndEncode(valueOnlyText), op);
            }

            return (null, ComparisonOperator.Equal); // Fallback for unparseable or unsupported cases
        }

        /// <summary>
        /// Parses a string literal value for output clauses (no operators).
        /// </summary>
        /// <param name="text">The string to parse.</param>
        /// <param name="typeRef">The expected type reference of the output.</param>
        /// <param name="encoder">The StringValueEncoder for string types.</param>
        /// <returns>A double representing the numerical or encoded string value.</returns>
        private static double ParseOutputLiteral(string text, string typeRef, StringValueEncoder encoder)
        {
            string cleanedText = text.Trim();
            string normalizedTypeRef = typeRef.ToLowerInvariant();

            // Remove DMN function wrappers and quotes
            if (normalizedTypeRef == "string" && cleanedText.StartsWith("\"") && cleanedText.EndsWith("\"") && cleanedText.Length > 1)
            {
                cleanedText = cleanedText.Substring(1, cleanedText.Length - 2);
            }
            else if ((normalizedTypeRef == "date" || normalizedTypeRef == "datetime")
                     && cleanedText.StartsWith("date(\"") && cleanedText.EndsWith("\")"))
            {
                cleanedText = cleanedText.Substring("date(\"".Length, cleanedText.Length - "date(\"".Length - "\")".Length);
            }
            else if (normalizedTypeRef == "datetime" && cleanedText.StartsWith("date and time(\"") && cleanedText.EndsWith("\")"))
            {
                cleanedText = cleanedText.Substring("date and time(\"".Length, cleanedText.Length - "date and time(\"".Length - "\")".Length);
            }

            switch (normalizedTypeRef)
            {
                case "number":
                case "integer":
                    if (double.TryParse(cleanedText, NumberStyles.Any, CultureInfo.InvariantCulture, out double numVal))
                    {
                        return numVal;
                    }
                    break;
                case "boolean":
                    if (bool.TryParse(cleanedText, out bool boolVal))
                    {
                        return boolVal ? 1.0 : 0.0;
                    }
                    break;
                case "date":
                case "datetime":
                    if (DateTime.TryParse(cleanedText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dtValue))
                    {
                        return dtValue.ToOADate();
                    }
                    break;
                case "string":
                default:
                    // If it's a string, or parsing failed for a specific type, encode it.
                    return (double)encoder.AddAndEncode(cleanedText);
            }
            return double.NaN; // Indicate parsing failure
        }
    }
}