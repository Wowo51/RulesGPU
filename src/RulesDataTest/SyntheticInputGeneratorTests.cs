//Copyright Warren Harding 2025.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RulesData;
using RulesDMN.Models;
using System.Collections.Generic;
using System;

namespace RulesDataTest
{
    [TestClass]
    public sealed class SyntheticInputGeneratorTests
    {
        [TestMethod]
        public void GenerateInputs_ValidDecisionTable_GeneratesCorrectTypesAndStructure()
        {
            // Arrange
            DecisionTable mockDecisionTable = new DecisionTable();
            mockDecisionTable.Inputs.Add(new InputClause { Id = "input1", Expression = new LiteralExpression { Text = "name", TypeRef = "string" } });
            mockDecisionTable.Inputs.Add(new InputClause { Id = "input2", Expression = new LiteralExpression { Text = "age", TypeRef = "integer" } });
            mockDecisionTable.Inputs.Add(new InputClause { Id = "input3", Expression = new LiteralExpression { Text = "amount", TypeRef = "number" } });
            mockDecisionTable.Inputs.Add(new InputClause { Id = "input4", Expression = new LiteralExpression { Text = "isActive", TypeRef = "boolean" } });
            mockDecisionTable.Inputs.Add(new InputClause { Id = "input5", Expression = new LiteralExpression { Text = "birthDate", TypeRef = "date" } });

            // Act
            IReadOnlyDictionary<string, object> inputs = SyntheticInputGenerator.GenerateInputs(mockDecisionTable);

            // Assert
            Assert.IsNotNull(inputs);
            Assert.AreEqual(5, inputs.Count);

            Assert.IsTrue(inputs.ContainsKey("name"));
            Assert.IsInstanceOfType(inputs["name"], typeof(string));

            Assert.IsTrue(inputs.ContainsKey("age"));
            Assert.IsInstanceOfType(inputs["age"], typeof(int));

            Assert.IsTrue(inputs.ContainsKey("amount"));
            Assert.IsInstanceOfType(inputs["amount"], typeof(double));

            Assert.IsTrue(inputs.ContainsKey("isActive"));
            Assert.IsInstanceOfType(inputs["isActive"], typeof(bool));

            Assert.IsTrue(inputs.ContainsKey("birthDate"));
            Assert.IsInstanceOfType(inputs["birthDate"], typeof(DateTime));
        }

        [TestMethod]
        public void GenerateInputs_InputClauseWithNullExpression_NoInputGeneratedForIt()
        {
            // Arrange
            DecisionTable mockDecisionTable = new DecisionTable();
            mockDecisionTable.Inputs.Add(new InputClause { Id = "input1", Expression = new LiteralExpression { Text = "name", TypeRef = "string" } });
            mockDecisionTable.Inputs.Add(new InputClause { Id = "input2", Expression = null });

            // Act
            IReadOnlyDictionary<string, object> inputs = SyntheticInputGenerator.GenerateInputs(mockDecisionTable);

            // Assert
            Assert.IsNotNull(inputs);
            Assert.AreEqual(1, inputs.Count);
            Assert.IsTrue(inputs.ContainsKey("name"));
            Assert.IsFalse(inputs.ContainsKey("input2")); // No input generated for null expression
        }

        [TestMethod]
        public void GenerateInputs_LiteralExpressionWithEmptyTypeRef_DefaultsToString()
        {
            // Arrange
            DecisionTable mockDecisionTable = new DecisionTable();
            mockDecisionTable.Inputs.Add(new InputClause { Id = "input1", Expression = new LiteralExpression { Text = "item", TypeRef = "" } });

            // Act
            IReadOnlyDictionary<string, object> inputs = SyntheticInputGenerator.GenerateInputs(mockDecisionTable);

            // Assert
            Assert.IsNotNull(inputs);
            Assert.AreEqual(1, inputs.Count);
            Assert.IsTrue(inputs.ContainsKey("item"));
            Assert.IsInstanceOfType(inputs["item"], typeof(string));
        }

        [TestMethod]
        public void GenerateInputs_LiteralExpressionWithWhitespaceTypeRef_DefaultsToString()
        {
            // Arrange
            DecisionTable mockDecisionTable = new DecisionTable();
            mockDecisionTable.Inputs.Add(new InputClause { Id = "input1", Expression = new LiteralExpression { Text = "item", TypeRef = " " } });

            // Act
            IReadOnlyDictionary<string, object> inputs = SyntheticInputGenerator.GenerateInputs(mockDecisionTable);

            // Assert
            Assert.IsNotNull(inputs);
            Assert.AreEqual(1, inputs.Count);
            Assert.IsTrue(inputs.ContainsKey("item"));
            Assert.IsInstanceOfType(inputs["item"], typeof(string));
        }

        [TestMethod]
        public void GenerateInputs_LiteralExpressionWithInvalidTypeRef_DefaultsToString()
        {
            // Arrange
            DecisionTable mockDecisionTable = new DecisionTable();
            mockDecisionTable.Inputs.Add(new InputClause { Id = "input1", Expression = new LiteralExpression { Text = "unknown", TypeRef = "unknownType" } });

            // Act
            IReadOnlyDictionary<string, object> inputs = SyntheticInputGenerator.GenerateInputs(mockDecisionTable);

            // Assert
            Assert.IsNotNull(inputs);
            Assert.AreEqual(1, inputs.Count);
            Assert.IsTrue(inputs.ContainsKey("unknown"));
            Assert.IsInstanceOfType(inputs["unknown"], typeof(string));
        }

        [TestMethod]
        public void GenerateInputs_LiteralExpressionWithEmptyText_UsesInputIdAsKey()
        {
            // Arrange
            DecisionTable mockDecisionTable = new DecisionTable();
            mockDecisionTable.Inputs.Add(new InputClause { Id = "myInputId", Expression = new LiteralExpression { Text = "", TypeRef = "string" } });

            // Act
            IReadOnlyDictionary<string, object> inputs = SyntheticInputGenerator.GenerateInputs(mockDecisionTable);

            // Assert
            Assert.IsNotNull(inputs);
            Assert.AreEqual(1, inputs.Count);
            Assert.IsTrue(inputs.ContainsKey("myInputId"));
            Assert.IsInstanceOfType(inputs["myInputId"], typeof(string));
        }

        [TestMethod]
        public void GenerateInputs_LiteralExpressionWithNullText_UsesInputIdAsKey()
        {
            // Arrange
            DecisionTable mockDecisionTable = new DecisionTable();
            mockDecisionTable.Inputs.Add(new InputClause { Id = "myInputId", Expression = new LiteralExpression { Text = null!, TypeRef = "string" } }); // Using null! to simulate potential null value for test coverage

            // Act
            IReadOnlyDictionary<string, object> inputs = SyntheticInputGenerator.GenerateInputs(mockDecisionTable);

            // Assert
            Assert.IsNotNull(inputs);
            Assert.AreEqual(1, inputs.Count);
            Assert.IsTrue(inputs.ContainsKey("myInputId"));
            Assert.IsInstanceOfType(inputs["myInputId"], typeof(string));
        }

        [TestMethod]
        public void GenerateInputs_InputClauseWithNullIdAndEmptyText_DoesNotGenerateInput()
        {
            // Arrange
            DecisionTable mockDecisionTable = new DecisionTable();
            mockDecisionTable.Inputs.Add(new InputClause { Id = null, Expression = new LiteralExpression { Text = "", TypeRef = "string" } });

            // Act
            IReadOnlyDictionary<string, object> inputs = SyntheticInputGenerator.GenerateInputs(mockDecisionTable);

            // Assert
            Assert.IsNotNull(inputs);
            Assert.AreEqual(0, inputs.Count);
        }

        [TestMethod]
        public void GenerateInputs_DecisionTableWithNoInputs_ReturnsEmptyDictionary()
        {
            // Arrange
            DecisionTable mockDecisionTable = new DecisionTable(); // No inputs added

            // Act
            IReadOnlyDictionary<string, object> inputs = SyntheticInputGenerator.GenerateInputs(mockDecisionTable);

            // Assert
            Assert.IsNotNull(inputs);
            Assert.AreEqual(0, inputs.Count);
        }

        [TestMethod]
        public void GenerateMultipleInputSets_ZeroSets_ReturnsEmptyList()
        {
            // Arrange
            DecisionTable mockDecisionTable = new DecisionTable();
            mockDecisionTable.Inputs.Add(new InputClause { Id = "input1", Expression = new LiteralExpression { Text = "name", TypeRef = "string" } });

            // Act
            IReadOnlyList<IReadOnlyDictionary<string, object>> allInputSets = SyntheticInputGenerator.GenerateMultipleInputSets(mockDecisionTable, 0);

            // Assert
            Assert.IsNotNull(allInputSets);
            Assert.AreEqual(0, allInputSets.Count);
        }

        [TestMethod]
        public void GenerateMultipleInputSets_OneSet_ReturnsListOfOneDictionary()
        {
            // Arrange
            DecisionTable mockDecisionTable = new DecisionTable();
            mockDecisionTable.Inputs.Add(new InputClause { Id = "input1", Expression = new LiteralExpression { Text = "name", TypeRef = "string" } });

            // Act
            IReadOnlyList<IReadOnlyDictionary<string, object>> allInputSets = SyntheticInputGenerator.GenerateMultipleInputSets(mockDecisionTable, 1);

            // Assert
            Assert.IsNotNull(allInputSets);
            Assert.AreEqual(1, allInputSets.Count);
            Assert.IsNotNull(allInputSets[0]);
            Assert.AreEqual(1, allInputSets[0].Count);
            Assert.IsTrue(allInputSets[0].ContainsKey("name"));
        }

        [TestMethod]
        public void GenerateMultipleInputSets_MultipleSets_ReturnsListOfCorrectCountAndDistinctValues()
        {
            // Arrange
            DecisionTable mockDecisionTable = new DecisionTable();
            mockDecisionTable.Inputs.Add(new InputClause { Id = "input1", Expression = new LiteralExpression { Text = "name", TypeRef = "string" } });
            mockDecisionTable.Inputs.Add(new InputClause { Id = "input2", Expression = new LiteralExpression { Text = "age", TypeRef = "integer" } });

            int numberOfSets = 5;

            // Act
            IReadOnlyList<IReadOnlyDictionary<string, object>> allInputSets = SyntheticInputGenerator.GenerateMultipleInputSets(mockDecisionTable, numberOfSets);

            // Assert
            Assert.IsNotNull(allInputSets);
            Assert.AreEqual(numberOfSets, allInputSets.Count);

            // Check that each set is non-empty and contains the expected keys and types
            foreach (IReadOnlyDictionary<string, object> inputs in allInputSets)
            {
                Assert.AreEqual(2, inputs.Count);
                Assert.IsTrue(inputs.ContainsKey("name"));
                Assert.IsInstanceOfType(inputs["name"], typeof(string));
                Assert.IsTrue(inputs.ContainsKey("age"));
                Assert.IsInstanceOfType(inputs["age"], typeof(int));
            }

            // Optional: Check for distinctness (probabilistic, given random generation)
            bool namesAreDifferent = false;
            string? firstName = (string?)allInputSets[0]["name"];
            for (int i = 1; i < numberOfSets; i++)
            {
                if (!string.Equals(firstName, (string?)allInputSets[i]["name"], StringComparison.Ordinal))
                {
                    namesAreDifferent = true;
                    break;
                }
            }
            Assert.IsTrue(namesAreDifferent, "Expected names to be different across sets (probabilistic check).");

            bool agesAreDifferent = false;
            int? firstAge = (int?)allInputSets[0]["age"];
            for (int i = 1; i < numberOfSets; i++)
            {
                if (firstAge.HasValue && (int?)allInputSets[i]["age"] != firstAge)
                {
                    agesAreDifferent = true;
                    break;
                }
            }
            Assert.IsTrue(agesAreDifferent, "Expected ages to be different across sets (probabilistic check).");
        }
    }
}