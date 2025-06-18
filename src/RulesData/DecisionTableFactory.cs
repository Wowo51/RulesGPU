//Copyright Warren Harding 2025.
using RulesDMN.Models;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace RulesData
{
    public static class DecisionTableFactory
    {
        private static readonly Random _random = new Random();
        private static readonly string[] _availableTypes = { "string", "number", "integer", "boolean", "date", "datetime" };

        public static DecisionTable CreateRandomDecisionTable(
            int minInputs, int maxInputs,
            int minOutputs, int maxOutputs,
            int minRules, int maxRules)
        {
            DecisionTable decisionTable = new DecisionTable();
            decisionTable.HitPolicy = HitPolicy.Unique; // For simplicity in generation of synthetic data

            int numberOfInputs = _random.Next(minInputs, maxInputs + 1);
            int numberOfOutputs = _random.Next(minOutputs, maxOutputs + 1);
            int numberOfRules = _random.Next(minRules, maxRules + 1);

            // Generate Input Clauses
            for (int i = 0; i < numberOfInputs; i++)
            {
                string typeRef = _availableTypes[_random.Next(_availableTypes.Length)];
                InputClause inputClause = new InputClause
                {
                    Id = $"Input_{i + 1}",
                    Expression = new LiteralExpression
                    {
                        Text = $"input{i + 1}", // This will be the variable name
                        TypeRef = typeRef
                    }
                };
                decisionTable.Inputs.Add(inputClause);
            }

            // Generate Output Clauses
            for (int i = 0; i < numberOfOutputs; i++)
            {
                string typeRef = _availableTypes[_random.Next(_availableTypes.Length)];
                OutputClause outputClause = new OutputClause
                {
                    Id = $"Output_{i + 1}",
                    Name = $"output{i + 1}",
                    TypeRef = typeRef
                };
                decisionTable.Outputs.Add(outputClause);
            }

            // Generate Rules
            for (int r = 0; r < numberOfRules; r++)
            {
                Rule rule = new Rule { Id = $"Rule_{r + 1}" };

                // Generate Input Entries for the rule
                foreach (InputClause inputClause in decisionTable.Inputs)
                {
                    LiteralExpression? literalExpression = inputClause.Expression as LiteralExpression;
                    if (literalExpression is null)
                    {
                        // This should ideally not happen with internally generated tables.
                        rule.InputEntries.Add(new InputEntry { Text = string.Empty });
                        continue;
                    }

                    IDataTypeGenerator generator = DataTypeGeneratorFactory.GetGenerator(literalExpression.TypeRef);
                    object? generatedValue = generator.Generate();
                    
                    // Convert the generated value to its string representation for the DMN rule entry
                    string valueString = DataTypeConverter.ConvertValueToRuleString(generatedValue, literalExpression.TypeRef);
                    rule.InputEntries.Add(new InputEntry { Text = valueString });
                }

                // Generate Output Entries for the rule
                foreach (OutputClause outputClause in decisionTable.Outputs)
                {
                    IDataTypeGenerator generator = DataTypeGeneratorFactory.GetGenerator(outputClause.TypeRef);
                    object? generatedValue = generator.Generate();
                    
                    // Convert the generated value to its string representation for the DMN rule entry
                    string valueString = DataTypeConverter.ConvertValueToRuleString(generatedValue, outputClause.TypeRef);
                    rule.OutputEntries.Add(new OutputEntry { Text = valueString });
                }
                decisionTable.Rules.Add(rule);
            }

            return decisionTable;
        }
    }
}