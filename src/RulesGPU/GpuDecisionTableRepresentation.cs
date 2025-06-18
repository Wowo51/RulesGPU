//Copyright Warren Harding 2025.
using System;
using System.Collections.Generic;
using RulesDMN.Models;
using TorchSharp;
using static TorchSharp.torch;

namespace RulesGPU
{
    /// <summary>
    /// Represents a DMN DecisionTable compiled into TorchSharp tensors for GPU-accelerated evaluation.
    /// </summary>
    public sealed class GpuDecisionTableRepresentation : IDisposable
    {
        /// <summary>
        /// Maps DMN input variable names to their corresponding column indices in the input tensors.
        /// </summary>
        public IReadOnlyDictionary<string, int> InputMapping { get; private set; }

        /// <summary>
        /// Maps DMN output variable names to their corresponding column indices in the output tensors.
        /// </summary>
        public IReadOnlyDictionary<string, int> OutputMapping { get; private set; }

        /// <summary>
        /// Maps DMN output variable names to their DMN type references (e.g., "string", "number").
        /// </summary>
        public IReadOnlyDictionary<string, string> OutputTypeRefs { get; private set; }

        /// <summary>
        /// A tensor (num_rules, num_inputs) containing the numerical values for rule input conditions.
        /// String values are encoded to integers, booleans to 0/1, numbers as floats.
        /// </summary>
        public Tensor InputConditionValues { get; private set; }

        /// <summary>
        /// A tensor (num_rules, num_inputs) storing the integer IDs of the ComparisonOperator enum,
        /// indicating the type of comparison to perform for each rule's input condition.
        /// </summary>
        public Tensor InputConditionComparisonOperators { get; private set; }

        /// <summary>
        /// A boolean tensor (num_rules, num_inputs) indicating whether a condition exists for a given
        /// rule and input (true) or if it's a "don't care" (false).
        /// </summary>
        public Tensor InputConditionMask { get; private set; }

        /// <summary>
        /// A tensor (num_rules, num_outputs) containing the numerical values for rule outputs.
        /// String values are encoded to integers, booleans to 0/1, numbers as floats.
        /// </summary>
        public Tensor OutputValues { get; private set; }

        /// <summary>
        /// The hit policy specified for the original DMN DecisionTable.
        /// </summary>
        public HitPolicy HitPolicy { get; private set; }

        /// <summary>
        /// The StringValueEncoder used to map string literals to numerical IDs and vice-versa.
        /// </summary>
        public StringValueEncoder StringValueEncoder { get; private set; }

        public GpuDecisionTableRepresentation(
            IReadOnlyDictionary<string, int> inputMapping,
            IReadOnlyDictionary<string, int> outputMapping,
            IReadOnlyDictionary<string, string> outputTypeRefs,
            Tensor inputConditionValues,
            Tensor inputConditionComparisonOperators,
            Tensor inputConditionMask,
            Tensor outputValues,
            HitPolicy hitPolicy,
            StringValueEncoder stringValueEncoder)
        {
            InputMapping = inputMapping;
            OutputMapping = outputMapping;
            OutputTypeRefs = outputTypeRefs;
            InputConditionValues = inputConditionValues;
            InputConditionComparisonOperators = inputConditionComparisonOperators;
            InputConditionMask = inputConditionMask;
            OutputValues = outputValues;
            HitPolicy = hitPolicy;
            StringValueEncoder = stringValueEncoder;
        }

        public void Dispose()
        {
            InputConditionValues.Dispose();
            InputConditionComparisonOperators.Dispose();
            InputConditionMask.Dispose();
            OutputValues.Dispose();
        }
    }
}