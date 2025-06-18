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
    /// A rules engine that utilizes TorchSharp for GPU-accelerated evaluation of DMN Decision Tables.
    /// </summary>
    public sealed class RulesGPUEngine
    {
        private readonly Device _device;

        public RulesGPUEngine(Device device)
        {
            _device = device;
        }

        /// <summary>
        /// Evaluates a DMN DecisionTable represented by <see cref="GpuDecisionTableRepresentation"/>
        /// against a single set of input data.
        /// </summary>
        /// <param name="gpuData">The GPU-accelerated representation of the decision table.</param>
        /// <param name="inputs">A dictionary of input variable names and their corresponding values.</param>
        /// <returns>
        /// A dictionary of output variable names and their values if a single output is expected (e.g., Unique, First hit policies),
        /// or a list of such dictionaries if multiple outputs are aggregated (e.g., Collect hit policy).
        /// Returns null if no rule matches and hit policy requires a single match (Unique/First), or if evaluation fails.
        /// </returns>
        public object? Evaluate(GpuDecisionTableRepresentation? gpuData, IReadOnlyDictionary<string, object>? inputs)
        {
            // Handle null gpuData or inputs gracefully based on hit policy.
            if (gpuData is null || inputs is null)
            {
                HitPolicy effectiveHitPolicy = gpuData?.HitPolicy ?? HitPolicy.Unique;
                return effectiveHitPolicy == HitPolicy.Collect ? (object)new List<IReadOnlyDictionary<string, object?>>() : null;
            }

            // At this point, gpuData and inputs are guaranteed to be non-null.
            GpuDecisionTableRepresentation nonNullableGpuData = gpuData;
            IReadOnlyDictionary<string, object> nonNullableInputs = inputs;

            // Wrap the single input into a list and call the batch evaluation method.
            IReadOnlyList<IReadOnlyDictionary<string, object>> singleInputList = new List<IReadOnlyDictionary<string, object>> { nonNullableInputs };
            IReadOnlyList<object?> batchResult = Evaluate(nonNullableGpuData, singleInputList); 
            
            // The batchResult will contain exactly one item corresponding to the single input.
            return batchResult[0];
        }

        /// <summary>
        /// Evaluates a DMN DecisionTable represented by <see cref="GpuDecisionTableRepresentation"/>
        /// against a list of input records in parallel.
        /// </summary>
        /// <param name="gpuData">The GPU-accelerated representation of the decision table.</param>
        /// <param name="inputRecords">A list of dictionaries, where each dictionary represents one set of input data.</param>
        /// <returns>
        /// A list of objects, where each object is the result for the corresponding input record.
        /// An object can be a dictionary of output values (for Unique, First) or a list of dictionaries (for Collect).
        /// Returns null for a given record if no rule matches or if evaluation fails for that record, or if no DMN is loaded.
        /// </returns>
        public IReadOnlyList<object?> Evaluate(GpuDecisionTableRepresentation? gpuData, IReadOnlyList<IReadOnlyDictionary<string, object>>? inputRecords)
        {
            if (gpuData is null || inputRecords is null || inputRecords.Count == 0)
            {
                HitPolicy effectiveHitPolicy = gpuData?.HitPolicy ?? HitPolicy.Unique;
                List<object?> emptyResults = new List<object?>();
                for (int i = 0; i < (inputRecords?.Count ?? 0); i++)
                {
                    emptyResults.Add(effectiveHitPolicy == HitPolicy.Collect ? (object)new List<IReadOnlyDictionary<string, object?>>() : null);
                }
                return emptyResults;
            }

            // At this point, gpuData and inputRecords are guaranteed to be non-null.
            GpuDecisionTableRepresentation nonNullableGpuData = gpuData;
            IReadOnlyList<IReadOnlyDictionary<string, object>> nonNullableInputRecords = inputRecords;

            // If there are no rules or no inputs/outputs, return empty or null based on hit policy for each record.
            if (nonNullableGpuData.InputConditionValues.numel() == 0 || nonNullableGpuData.OutputValues.numel() == 0)
            {
                List<object?> emptyResultsForTable = new List<object?>();
                for (int i = 0; i < nonNullableInputRecords.Count; i++)
                {
                    emptyResultsForTable.Add(nonNullableGpuData.HitPolicy == HitPolicy.Collect ? (object)new List<IReadOnlyDictionary<string, object?>>() : null);
                }
                return emptyResultsForTable;
            }

            int numInputs = nonNullableGpuData.InputMapping.Count;
            int numRules = (int)nonNullableGpuData.InputConditionValues.shape[0];
            // int numOutputs = (int)nonNullableGpuData.OutputValues.shape[1]; // Not used directly in this method
            int batchSize = nonNullableInputRecords.Count;

            // 1. Prepare batched input tensor from the provided inputRecords list
            using (Tensor batchedInputTensor = zeros(batchSize, numInputs, dtype: float64, device: _device))
            {
                for (int b = 0; b < batchSize; b++)
                {
                    IReadOnlyDictionary<string, object> currentInput = nonNullableInputRecords[b];
                    foreach (KeyValuePair<string, object> entry in currentInput)
                    {
                        if (nonNullableGpuData.InputMapping.TryGetValue(entry.Key, out int inputIdx))
                        {
                            double valueToStore = ConvertToDoubleForTensor(entry.Value, nonNullableGpuData.StringValueEncoder);
                            batchedInputTensor[b, inputIdx] = tensor(valueToStore, dtype: float64, device: _device);
                        }
                    }
                }

                // Explicitly expand dimensions for proper broadcasting
                using (Tensor expandedBatchedInput = batchedInputTensor.unsqueeze(1)) // Shape (batch_size, 1, num_inputs)
                using (Tensor expandedInputConditionValues = nonNullableGpuData.InputConditionValues.unsqueeze(0)) // Shape (1, num_rules, num_inputs)
                using (Tensor expandedOpEqMask = (nonNullableGpuData.InputConditionComparisonOperators == (long)ComparisonOperator.Equal).to(ScalarType.Bool).unsqueeze(0))
                using (Tensor expandedOpGtMask = (nonNullableGpuData.InputConditionComparisonOperators == (long)ComparisonOperator.GreaterThan).to(ScalarType.Bool).unsqueeze(0))
                using (Tensor expandedOpGeMask = (nonNullableGpuData.InputConditionComparisonOperators == (long)ComparisonOperator.GreaterThanOrEqual).to(ScalarType.Bool).unsqueeze(0))
                using (Tensor expandedOpLtMask = (nonNullableGpuData.InputConditionComparisonOperators == (long)ComparisonOperator.LessThan).to(ScalarType.Bool).unsqueeze(0))
                using (Tensor expandedOpLeMask = (nonNullableGpuData.InputConditionComparisonOperators == (long)ComparisonOperator.LessThanOrEqual).to(ScalarType.Bool).unsqueeze(0))
                using (Tensor expandedOpNeMask = (nonNullableGpuData.InputConditionComparisonOperators == (long)ComparisonOperator.NotEqual).to(ScalarType.Bool).unsqueeze(0)) // NotEqual mask
                using (Tensor expandedInputConditionMask = nonNullableGpuData.InputConditionMask.unsqueeze(0)) // Shape (1, num_rules, num_inputs)
                {
                    // 2. Initialize a tensor to accumulate comparison results for all conditions.
                    using (Tensor comparisonResultsAcrossAllInputs = zeros(batchSize, numRules, numInputs, dtype: ScalarType.Bool, device: _device))
                    {
                        // Determine NaN presence for both input and condition value
                        using (Tensor isInputNaN = torch.isnan(expandedBatchedInput)) // (batch_size, 1, num_inputs)
                        using (Tensor isConditionNaN = torch.isnan(expandedInputConditionValues)) // (1, num_rules, num_inputs)
                        {
                            // A flag if *any* of the two values in a comparison is NaN
                            using (Tensor anyNaN = isInputNaN.logical_or(isConditionNaN))
                            // A flag if *neither* of the two values is NaN
                            using (Tensor notAnyNaN = anyNaN.logical_not())
                            {
                                // Equality (==): True only if (standard equality) AND (neither operand is NaN).
                                // This makes `NaN == NaN` -> false, and `NaN == X` -> false (consistent with DMN NULL behavior).
                                using (Tensor standardEq = expandedBatchedInput.eq(expandedInputConditionValues))
                                using (Tensor eqResult = standardEq.logical_and(expandedOpEqMask).logical_and(notAnyNaN))
                                {
                                    comparisonResultsAcrossAllInputs.logical_or_(eqResult);
                                }

                                // Not-Equality (!=): True only if (standard not-equality) AND (neither operand is NaN).
                                // This ensures `NaN != NaN` -> false, and `NaN != X` -> false (consistent with DMN NULL behavior).
                                using (Tensor standardNe = expandedBatchedInput.ne(expandedInputConditionValues))
                                using (Tensor neResult = standardNe.logical_and(expandedOpNeMask).logical_and(notAnyNaN))
                                {
                                    comparisonResultsAcrossAllInputs.logical_or_(neResult);
                                }

                                // For other operators (GT, GE, LT, LE): False if any operand is NaN. Otherwise standard comparison.
                                // Standard TorchSharp comparison ops (gt, ge, lt, le) already return false if any operand is NaN.
                                // We explicitly apply 'notAnyNaN' to ensure this behavior is consistent and clear.
                                using (Tensor compGt = expandedBatchedInput.gt(expandedInputConditionValues))
                                using (Tensor gtPasses = compGt.logical_and(expandedOpGtMask).logical_and(notAnyNaN))
                                {
                                    comparisonResultsAcrossAllInputs.logical_or_(gtPasses);
                                }

                                using (Tensor compGe = expandedBatchedInput.ge(expandedInputConditionValues))
                                using (Tensor gePasses = compGe.logical_and(expandedOpGeMask).logical_and(notAnyNaN))
                                {
                                    comparisonResultsAcrossAllInputs.logical_or_(gePasses);
                                }

                                using (Tensor compLt = expandedBatchedInput.lt(expandedInputConditionValues))
                                using (Tensor ltPasses = compLt.logical_and(expandedOpLtMask).logical_and(notAnyNaN))
                                {
                                    comparisonResultsAcrossAllInputs.logical_or_(ltPasses);
                                }

                                using (Tensor compLe = expandedBatchedInput.le(expandedInputConditionValues))
                                using (Tensor lePasses = compLe.logical_and(expandedOpLeMask).logical_and(notAnyNaN))
                                {
                                    comparisonResultsAcrossAllInputs.logical_or_(lePasses);
                                }
                            }
                        }
                        
                        // 3. Apply the "don't care" mask: if original mask is false (don't care), then this condition always passes.
                        using (Tensor maskNot = expandedInputConditionMask.logical_not())
                        using (Tensor finalMatches = comparisonResultsAcrossAllInputs.logical_or(maskNot))
                        {
                            // 4. Determine which rules match for each record in the batch (all inputs for a rule must match)
                            using (Tensor batchRuleFired = finalMatches.all(dim: 2)) 
                            {
                                // 5. Apply Hit Policy for each item in the batch
                                return ApplyHitPolicyBatch(nonNullableGpuData, batchRuleFired);
                            }
                        }
                    } // comparisonResultsAcrossAllInputs.Dispose() is called here
                }
            }
        }

        private double ConvertToDoubleForTensor(object? value, StringValueEncoder encoder)
        {
            if (value is null)
            {
                return double.NaN; // Explicitly handle null inputs as NaN
            }
            else if (value is string stringValue)
            {
                int encodedValue = encoder.Encode(stringValue);
                if (encodedValue == -1) // -1 indicates string not found in encoder vocabulary
                {
                    return double.NaN; // Treat as NaN, will fail all comparisons
                }
                return (double)encodedValue;
            }
            else if (value is bool boolValue)
            {
                return boolValue ? 1.0 : 0.0;
            }
            else if (value is DateTime dateTimeValue)
            {
                // ToOADate returns a double; this is good for double precision.
                return dateTimeValue.ToOADate();
            }
            else if (value is sbyte sbyteValue)
            {
                return (double)sbyteValue;
            }
            else if (value is byte byteValue)
            {
                return (double)byteValue;
            }
            else if (value is short shortValue)
            {
                return (double)shortValue;
            }
            else if (value is ushort ushortValue)
            {
                return (double)ushortValue;
            }
            else if (value is int intValue)
            {
                return (double)intValue;
            }
            else if (value is uint uintValue)
            {
                return (double)uintValue;
            }
            else if (value is long longValue)
            {
                return (double)longValue;
            }
            else if (value is ulong ulongValue)
            {
                return (double)ulongValue;
            }
            else if (value is float floatValue)
            {
                // Convert float to double
                return (double)floatValue;
            }
            else if (value is double doubleValue)
            {
                return doubleValue;
            }
            else if (value is decimal decimalValue)
            {
                // Convert decimal to double, potential precision loss for very large/small numbers.
                return (double)decimalValue;
            }
            // For unsupported input object types, assign NaN. Comparisons with NaN will behave according to DMN logic.
            return double.NaN;
        }
        
        /// <summary>
        /// Applies the DMN hit policy for a single set of matched rules.
        /// </summary>
        /// <param name="gpuData">The GPU-accelerated representation of the decision table.</param>
        /// <param name="ruleFired">A boolean tensor (num_rules) indicating which rules matched for a single input.</param>
        /// <returns>
        /// A dictionary of output values (for Unique, First) or a list of dictionaries (for Collect).
        /// Returns null if no rules fired or the hit policy is not supported.
        /// </returns>
        private object? ApplyHitPolicy(GpuDecisionTableRepresentation gpuData, Tensor ruleFired)
        {
            if (gpuData.HitPolicy == HitPolicy.Unique || gpuData.HitPolicy == HitPolicy.First)
            {
                using (Tensor matchingRuleIndices = nonzero(ruleFired)) // Returns (N, 1) or (0, 1)
                {
                    if (matchingRuleIndices.numel() == 0)
                    {
                        return null; // No rule fired
                    }

                    if (gpuData.HitPolicy == HitPolicy.Unique && matchingRuleIndices.numel() > 1)
                    {
                        // For Unique hit policy, if more than one rule matches, no decision is applicable.
                        // As per DMN spec, multiple matches for Unique policy should result in no match.
                        return null; 
                    }

                    // Extract the index of the first (or unique) matching rule.
                    long selectedRuleIndex = matchingRuleIndices[0, 0].item<long>();
                    
                    // Create a 1-dimensional tensor index for index_select.
                    using (Tensor indexTensor = tensor(new long[] { selectedRuleIndex }, device: _device))
                    // Use index_select to explicitly select the row(s). This guarantees resultTensor has shape (1, numOutputs).
                    using (Tensor resultTensor = gpuData.OutputValues.index_select(0, indexTensor))
                    {
                        // Squeeze dimension 0 to convert from (1, numOutputs) to (numOutputs,).
                        Tensor finalOutputTensor = resultTensor.squeeze(0);
                        IReadOnlyDictionary<string, object?> convertedDictionary = ConvertTensorToOutputDictionary(gpuData, finalOutputTensor);
                        finalOutputTensor.Dispose(); // Dispose after use
                        return convertedDictionary;
                    }
                }
            }
            else if (gpuData.HitPolicy == HitPolicy.Collect)
            {
                // For Collect, we return outputs of ALL fired rules.
                using (Tensor collectiveOutputIndicesRaw = nonzero(ruleFired)) // Returns (N, 1) or (0, 1)
                {
                    if (collectiveOutputIndicesRaw.numel() == 0)
                    {
                        return new List<IReadOnlyDictionary<string, object?>>(); // No rules fired, return empty list
                    }

                    // Squeeze the second dimension (of size 1) to get a 1D tensor of indices (num_matches,).
                    using (Tensor indicesToUse = collectiveOutputIndicesRaw.squeeze(1))
                    {
                        using (Tensor resultsTensor = gpuData.OutputValues.index_select(0, indicesToUse)) // Select all rows for fired rules
                        {
                            List<IReadOnlyDictionary<string, object?>> collectedOutputs = new List<IReadOnlyDictionary<string, object?>>();
                            for (long i = 0; i < resultsTensor.shape[0]; i++)
                            {
                                // Select each row and squeeze to (numOutputs,) for conversion.
                                Tensor selectedRow = resultsTensor[i]; // selectedRow already has shape (numOutputs,)
                                collectedOutputs.Add(ConvertTensorToOutputDictionary(gpuData, selectedRow));
                                selectedRow.Dispose(); // Dispose after use
                            }
                            return collectedOutputs;
                        }
                    }
                }
            }
            // Other hit policies (Any, Priority, RuleOrder, OutputOrder) are not explicitly supported by this engine's
            // current tensor-based output aggregation.
            return null; // Indicate unsupported hit policy by returning null.
        }

        /// <summary>
        /// Applies the DMN hit policy for a batch of matched rules.
        /// </summary>
        /// <param name="gpuData">The GPU-accelerated representation of the decision table.</param>
        /// <param name="batchRuleFired">A boolean tensor (batch_size, num_rules) indicating which rules matched for each input in the batch.</param>
        /// <returns>A list of objects, where each object is the result for the corresponding input record.</returns>
        private IReadOnlyList<object?> ApplyHitPolicyBatch(GpuDecisionTableRepresentation gpuData, Tensor batchRuleFired)
        {
            long batchSize = batchRuleFired.shape[0];
            List<object?> allResults = new List<object?>(capacity: (int)batchSize);

            for (long b = 0; b < batchSize; b++)
            {
                using (Tensor currentRuleFired = batchRuleFired[b]) // This tensor is for a single item in the batch, shape (num_rules)
                {
                    // Reuse the existing single-input hit policy application logic
                    allResults.Add(ApplyHitPolicy(gpuData, currentRuleFired));
                }
            }
            return allResults;
        }

        private IReadOnlyDictionary<string, object?> ConvertTensorToOutputDictionary(GpuDecisionTableRepresentation gpuData, Tensor outputTensor)
        {
            Dictionary<string, object?> result = new Dictionary<string, object?>();
            foreach (KeyValuePair<string, int> kvp in gpuData.OutputMapping)
            {
                int outputIdx = kvp.Value;
                string outputName = kvp.Key;

                if (outputIdx < outputTensor.numel()) // Ensure index is within bounds of the tensor
                {
                    double rawValue = outputTensor[outputIdx].item<double>();
                    string normalizedOutputTypeRef = "string"; // Default
                    if (gpuData.OutputTypeRefs.TryGetValue(outputName, out string? typeRef))
                    {
                        normalizedOutputTypeRef = typeRef.ToLowerInvariant();
                    }

                    object? decodedValue;
                    switch (normalizedOutputTypeRef)
                    {
                        case "string":
                            if (double.IsNaN(rawValue) || double.IsInfinity(rawValue))
                            {
                                decodedValue = (object?)string.Empty; // Represent NaN/Infinity as empty string for string type
                            }
                            else
                            {
                                // Encode/Decode string IDs should be within int range, but use long for Math.Round result
                                long roundedValue = (long)Math.Round(rawValue);
                                decodedValue = gpuData.StringValueEncoder.Decode((int)roundedValue); // Cast to int for Decode
                            }
                            break;
                        case "number":
                            decodedValue = rawValue; // Keep as double, NaN is acceptable for 'number'
                            break;
                        case "integer":
                            if (double.IsNaN(rawValue) || double.IsInfinity(rawValue))
                            {
                                decodedValue = 0; // Represent NaN/Infinity as 0 for integer type for consistency
                            }
                            else
                            {
                                decodedValue = (int)Math.Round(rawValue);
                            }
                            break;
                        case "boolean":
                            // DMN boolean: 1.0 for true, 0.0 for false. NaN or other values generally evaluate to false.
                            decodedValue = (!double.IsNaN(rawValue) && rawValue == 1.0); 
                            break;
                        case "date":
                        case "datetime":
                            // FromOADate throws if rawValue is NaN or outside valid Date range.
                            if (double.IsNaN(rawValue) || double.IsInfinity(rawValue) || rawValue < DateTime.MinValue.ToOADate() || rawValue > DateTime.MaxValue.ToOADate())
                            {
                                decodedValue = null; // Represent unrepresentable dates as null
                            }
                            else
                            {
                                decodedValue = DateTime.FromOADate(rawValue);
                            }
                            break;
                        default:
                            // Fallback for unknown typeRef or parsing issues
                            if (double.IsNaN(rawValue) || double.IsInfinity(rawValue))
                            {
                                decodedValue = null; // Default to null for unrepresentable values of unknown types
                            }
                            else
                            {
                                decodedValue = rawValue; // Return raw double value
                            }
                            break;
                    }
                    result[outputName] = decodedValue;
                }
                else
                {
                    // If output index is out of bounds, this suggests a mismatch between schema and tensor size.
                    // Provide a default value, ideally null or type-appropriate default.
                    result[outputName] = null; 
                }
            }
            return result;
        }
    }
}