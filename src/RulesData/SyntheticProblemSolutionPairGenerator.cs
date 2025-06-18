//Copyright Warren Harding 2025.
using RulesDMN.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RulesData
{
    public static class SyntheticProblemSolutionPairGenerator
    {
        private static readonly Random _random = new Random();

        public static (IReadOnlyDictionary<string, object> Problem, IReadOnlyDictionary<string, object> Solution)? GeneratePair(DecisionTable decisionTable)
        {
            if (decisionTable is null || !decisionTable.Rules.Any())
            {
                return null;
            }

            // 1. Randomly select a rule
            int ruleIndex = _random.Next(decisionTable.Rules.Count);
            Rule selectedRule = decisionTable.Rules.ElementAt(ruleIndex);

            // Ensure the selected rule has input and output entries matching the table's clauses
            // This is a sanity check for internally generated tables from DecisionTableFactory
            // In a well-formed DMN, these counts should match.
            // If they don't, it implies malformed rule or DMN structure, which this generator doesn't aim to fix.
            if (selectedRule.InputEntries.Count != decisionTable.Inputs.Count ||
                selectedRule.OutputEntries.Count != decisionTable.Outputs.Count)
            {
                return null; 
            }

            // 2. Generate the Solution (outputs from the selected rule)
            Dictionary<string, object> solution = new Dictionary<string, object>();
            for (int i = 0; i < selectedRule.OutputEntries.Count; i++)
            {
                OutputClause outputClause = decisionTable.Outputs[i];
                OutputEntry outputEntry = selectedRule.OutputEntries[i];
                
                string outputKey = !string.IsNullOrWhiteSpace(outputClause.Name) ? outputClause.Name : outputClause.Id ?? $"Output_{i}";
                object? convertedValue = DataTypeConverter.ConvertStringToType(outputEntry.Text, outputClause.TypeRef);
                
                // If conversion yields null, store null. This indicates a problem in the DMN definition or converter.
                // For synthetic data, we expect valid conversions.
                solution[outputKey] = convertedValue!; // Null-forgiving operator as we expect valid data from conversion
            }

            // 3. Generate the Problem (inputs that would trigger the selected rule)
            Dictionary<string, object> problem = new Dictionary<string, object>();
            for (int i = 0; i < selectedRule.InputEntries.Count; i++)
            {
                InputClause inputClause = decisionTable.Inputs[i];
                InputEntry inputEntry = selectedRule.InputEntries[i];

                LiteralExpression? literalExpression = inputClause.Expression as LiteralExpression;
                if (literalExpression is null)
                {
                    // This input clause doesn't have a literal expression, cannot derive input key/type
                    // This should not happen with DecisionTableFactory generated DMNs.
                    continue;
                }
                
                string inputKey = literalExpression.Text; // The variable name for the input
                // Generate a value that satisfies the rule's input condition.
                object generatedProblemValue = GenerateValueSatisfyingCondition(inputEntry.Text, literalExpression.TypeRef);
                
                problem[inputKey] = generatedProblemValue;
            }

            return (problem, solution);
        }

        /// <summary>
        /// Generates a concrete input value that satisfies a given DMN input condition string.
        /// </summary>
        /// <param name="conditionText">The DMN condition text (e.g., "> 18", "true", ""Student"").</param>
        /// <param name="typeRef">The DMN type reference (e.g., "number", "boolean", "string", "date").</param>
        /// <returns>An object representing a concrete value satisfying the condition.</returns>
        private static object GenerateValueSatisfyingCondition(string conditionText, string typeRef)
        {
            string cleanedText = conditionText.Trim();
            string valueOnlyText = cleanedText; 
            
            // "Don't care" condition
            if (valueOnlyText == "-")
            {
                return DataTypeGeneratorFactory.GetGenerator(typeRef).Generate();
            }

            // Numeric types with comparison operators
            if (typeRef.Equals("number", StringComparison.OrdinalIgnoreCase) || typeRef.Equals("integer", StringComparison.OrdinalIgnoreCase))
            {
                double baseValue;

                if (valueOnlyText.StartsWith(">=", StringComparison.Ordinal))
                {
                    valueOnlyText = valueOnlyText.Substring(2).Trim();
                    baseValue = (double)DataTypeConverter.ConvertStringToType(valueOnlyText, typeRef)!;
                    return (object)(baseValue + _random.NextDouble() * 10.0 + 0.01); 
                }
                else if (valueOnlyText.StartsWith("<=", StringComparison.Ordinal))
                {
                    valueOnlyText = valueOnlyText.Substring(2).Trim();
                    baseValue = (double)DataTypeConverter.ConvertStringToType(valueOnlyText, typeRef)!;
                    return (object)(baseValue - _random.NextDouble() * 10.0 - 0.01); 
                }
                else if (valueOnlyText.StartsWith(">", StringComparison.Ordinal))
                {
                    valueOnlyText = valueOnlyText.Substring(1).Trim();
                    baseValue = (double)DataTypeConverter.ConvertStringToType(valueOnlyText, typeRef)!;
                    return (object)(baseValue + _random.NextDouble() * 10.0 + 0.01); 
                }
                else if (valueOnlyText.StartsWith("<", StringComparison.Ordinal))
                {
                    valueOnlyText = valueOnlyText.Substring(1).Trim();
                    baseValue = (double)DataTypeConverter.ConvertStringToType(valueOnlyText, typeRef)!;
                    return (object)(baseValue - _random.NextDouble() * 10.0 - 0.01); 
                }
                else if (valueOnlyText.StartsWith("=", StringComparison.Ordinal))
                {
                     valueOnlyText = valueOnlyText.Substring(1).Trim();
                }
                // If it's just a number without explicit operator, it implies equality
                // Fall through to general conversion.
            }
            // For boolean, string, date, datetime types or exact numeric matches:
            // DataTypeConverter handles stripping quotes for strings and parsing DMN date/datetime functions.
            object? exactValue = DataTypeConverter.ConvertStringToType(valueOnlyText, typeRef);
            
            // If conversion results in null (e.g., malformed condition or unsupported DMN expression syntax),
            // or if the condition implies a specific value (exactValue is not null), use it.
            if (exactValue is null)
            {
                // Fallback: if the condition couldn't be parsed well, generate a random typical value for the type.
                // This prevents test generation from crashing, but might not perfectly satisfy a complex DMN rule expression.
                return DataTypeGeneratorFactory.GetGenerator(typeRef).Generate();
            }
            return exactValue;
        }
    }
}