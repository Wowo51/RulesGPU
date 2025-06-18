//Copyright Warren Harding 2025.
using RulesDMN.Models;
using System.Collections.Generic;

namespace RulesData
{
    public static class SyntheticInputGenerator
    {
        public static IReadOnlyDictionary<string, object> GenerateInputs(DecisionTable decisionTable)
        {
            Dictionary<string, object> inputs = new Dictionary<string, object>();

            foreach (InputClause inputClause in decisionTable.Inputs)
            {
                if (inputClause.Expression is LiteralExpression literalExpression)
                {
                    IDataTypeGenerator generator = DataTypeGeneratorFactory.GetGenerator(literalExpression.TypeRef);
                    // DMN input clauses have expressions with 'text' as the variable name.
                    string inputKey = literalExpression.Text;
                    if (string.IsNullOrWhiteSpace(inputKey))
                    {
                        inputKey = inputClause.Id ?? string.Empty; // Fallback to ID if text is empty
                    }

                    if (!string.IsNullOrWhiteSpace(inputKey))
                    {
                        inputs[inputKey] = generator.Generate();
                    }
                }
            }
            return inputs;
        }

        /// <summary>
        /// Generates multiple sets of synthetic input data for a given DecisionTable.
        /// </summary>
        /// <param name="decisionTable">The DMN DecisionTable to generate inputs for.</param>
        /// <param name="numberOfSets">The number of input sets to generate.</param>
        /// <returns>A list of dictionaries, where each dictionary represents one set of inputs.</returns>
        public static IReadOnlyList<IReadOnlyDictionary<string, object>> GenerateMultipleInputSets(DecisionTable decisionTable, int numberOfSets)
        {
            List<IReadOnlyDictionary<string, object>> allInputSets = new List<IReadOnlyDictionary<string, object>>();
            for (int i = 0; i < numberOfSets; i++)
            {
                allInputSets.Add(GenerateInputs(decisionTable));
            }
            return allInputSets;
        }
    }
}