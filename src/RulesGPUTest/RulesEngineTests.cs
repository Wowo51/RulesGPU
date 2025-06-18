//Copyright Warren Harding 2025.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RulesDMN;
using RulesDMN.Models;
using RulesGPU;
using RulesData; // Added for testing data generation
using System.Collections.Generic;
using TorchSharp;
using static TorchSharp.torch;
using System.Linq; // Added for .ToList(), .FirstOrDefault()
using System; // Added for Random, Math

namespace RulesGPUTest
{
    [TestClass]
    public class RulesEngineTests
    {
        private static readonly Device TestDevice = TestUtils.IsCudaAvailable() ? new Device(DeviceType.CUDA) : new Device(DeviceType.CPU);

        // Simple DMN XML for testing a basic decision table:
        // Input: Age (number), IsStudent (boolean)
        // Output: Discount (string)
        // Rules:
        //   1. Age >= 18 AND IsStudent = true => "StudentDiscount"
        //   2. Age < 18                     => "NoDiscount"
        //   3. Age >= 18 AND IsStudent = false => "AdultDiscount"
        private const string SimpleDmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<definitions id=""_0_"" name=""SimpleDiscountDecision"" xmlns=""https://www.omg.org/spec/DMN/20180521/MODEL/"">
  <decision id=""DiscountDecision"" name=""Discount Decision"">
    <decisionTable id=""_0_"" hitPolicy=""FIRST"">
      <input id=""InputClause_1"" name=""Age"">
        <inputExpression id=""InputExpression_1"" typeRef=""number"">
          <text>Age</text>
        </inputExpression>
      </input>
      <input id=""InputClause_2"" name=""IsStudent"">
        <inputExpression id=""InputExpression_2"" typeRef=""boolean"">
          <text>IsStudent</text>
        </inputExpression>
      </input>
      <output id=""OutputClause_1"" name=""Discount"" typeRef=""string"" />
      <rule id=""Rule_1"">
        <inputEntry id=""InputEntry_1"">
          <text>&gt;= 18</text>
        </inputEntry>
        <inputEntry id=""InputEntry_2"">
          <text>true</text>
        </inputEntry>
        <outputEntry id=""OutputEntry_1"">
          <text>""StudentDiscount""</text>
        </outputEntry>
      </rule>
      <rule id=""Rule_2"">
        <inputEntry id=""InputEntry_3"">
          <text>&lt; 18</text></inputEntry>
        <inputEntry id=""InputEntry_4"">
          <text>-</text>
        </inputEntry>
        <outputEntry id=""OutputEntry_2"">
          <text>""NoDiscount""</text>
        </outputEntry>
      </rule>
      <rule id=""Rule_3"">
        <inputEntry id=""InputEntry_5"">
          <text>&gt;= 18</text></inputEntry>
        <inputEntry id=""InputEntry_6"">
          <text>false</text>
        </inputEntry>
        <outputEntry id=""OutputEntry_3"">
          <text>""AdultDiscount""</text>
        </outputEntry>
      </rule>
    </decisionTable>
  </decision>
</definitions>";

        private const string LoanEligibilityDmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<definitions id=""loanEligibility"" name=""LoanEligibilityDecision"" xmlns=""https://www.omg.org/spec/DMN/20180521/MODEL/"">
  <decision id=""LoanDecision"" name=""Loan Decision"">
    <decisionTable id=""loanDecisionTable"" hitPolicy=""FIRST"">
      <input id=""inputAge"" name=""Applicant Age"">
        <inputExpression id=""inputExpressionAge"" typeRef=""number"">
          <text>Applicant Age</text>
        </inputExpression>
      </input>
      <input id=""inputIncome"" name=""Applicant Income"">
        <inputExpression id=""inputExpressionIncome"" typeRef=""number"">
          <text>Applicant Income</text>
        </inputExpression>
      </input>
      <input id=""inputCreditScore"" name=""Credit Score"">
        <inputExpression id=""inputExpressionCreditScore"" typeRef=""number"">
          <text>Credit Score</text>
        </inputExpression>
      </input>
      <output id=""outputDecision"" name=""Decision"" typeRef=""string"" />

      <!-- Rule 1: Underage -->
      <rule id=""rule1"">
        <inputEntry><text>&lt; 18</text></inputEntry>
        <inputEntry><text>-</text></inputEntry>
        <inputEntry><text>-</text></inputEntry>
        <outputEntry><text>""Declined""</text></outputEntry>
      </rule>
      <!-- Rule 2: Poor Credit -->
      <rule id=""rule2"">
        <inputEntry><text>&gt;= 18</text></inputEntry>
        <inputEntry><text>-</text></inputEntry>
        <inputEntry><text>&lt; 600</text></inputEntry>
        <outputEntry><text>""Declined""</text></outputEntry>
      </rule>
      <!-- Rule 3: Low Income but Acceptable Credit -->
      <rule id=""rule3"">
        <inputEntry><text>&gt;= 18</text></inputEntry>
        <inputEntry><text>&lt; 30000</text></inputEntry>
        <inputEntry><text>&gt;= 600</text></inputEntry>
        <outputEntry><text>""Manual Review""</text></outputEntry>
      </rule>
      <!-- Rule 4: Meets All Thresholds -->
      <rule id=""rule4"">
        <inputEntry><text>&gt;= 18</text></inputEntry>
        <inputEntry><text>&gt;= 30000</text></inputEntry>
        <inputEntry><text>&gt;= 600</text></inputEntry>
        <outputEntry><text>""Approved""</text></outputEntry>
      </rule>
    </decisionTable>
  </decision>
</definitions>";

        [TestMethod]
        public void Evaluate_SimpleRule_StudentDiscount()
        {
            // Arrange
            DmnModel? dmnModel = DmnParser.ParseDmn(SimpleDmnXml);
            Assert.IsNotNull(dmnModel, "DMN model should not be null.");

            Decision? decision = dmnModel.Decisions.Find(d => d.Id == "DiscountDecision");
            Assert.IsNotNull(decision, "Discount decision should be found.");
            Assert.IsInstanceOfType(decision.DecisionLogic, typeof(DecisionTable), "Decision logic should be a DecisionTable.");

            DecisionTable decisionTable = (DecisionTable)decision.DecisionLogic;
            using GpuDecisionTableRepresentation gpuData = DmnToGpuConverter.ConvertDecisionTableToGpuRepresentation(decisionTable, TestDevice);
            RulesGPUEngine engine = new RulesGPUEngine(TestDevice);

            IReadOnlyDictionary<string, object> inputs = new Dictionary<string, object>
            {
                { "Age", 20.0F },
                { "IsStudent", true }
            };

            // Act
            object? result = engine.Evaluate(gpuData, inputs);

            // Assert
            Assert.IsNotNull(result, "Result should not be null.");
            Assert.IsInstanceOfType(result, typeof(IReadOnlyDictionary<string, object>), "Result should be a dictionary.");
            
            IReadOnlyDictionary<string, object> output = (IReadOnlyDictionary<string, object>)result;
            string? discount = output["Discount"] as string;
            Assert.AreEqual("StudentDiscount", discount, "Expected 'StudentDiscount' for a student aged 20.");
        }

        [TestMethod]
        public void Evaluate_SimpleRule_NoDiscount()
        {
            // Arrange
            DmnModel? dmnModel = DmnParser.ParseDmn(SimpleDmnXml);
            Assert.IsNotNull(dmnModel, "DMN model should not be null.");

            Decision? decision = dmnModel.Decisions.Find(d => d.Id == "DiscountDecision");
            Assert.IsNotNull(decision, "Discount decision should be found.");
            Assert.IsInstanceOfType(decision.DecisionLogic, typeof(DecisionTable), "Decision logic should be a DecisionTable.");

            DecisionTable decisionTable = (DecisionTable)decision.DecisionLogic;
            using GpuDecisionTableRepresentation gpuData = DmnToGpuConverter.ConvertDecisionTableToGpuRepresentation(decisionTable, TestDevice);
            RulesGPUEngine engine = new RulesGPUEngine(TestDevice);

            IReadOnlyDictionary<string, object> inputs = new Dictionary<string, object>
            {
                { "Age", 15.0F },
                { "IsStudent", false }
            };

            // Act
            object? result = engine.Evaluate(gpuData, inputs);

            // Assert
            Assert.IsNotNull(result, "Result should not be null.");
            Assert.IsInstanceOfType(result, typeof(IReadOnlyDictionary<string, object>), "Result should be a dictionary.");
            
            IReadOnlyDictionary<string, object> output = (IReadOnlyDictionary<string, object>)result;
            string? discount = output["Discount"] as string;
            Assert.AreEqual("NoDiscount", discount, "Expected 'NoDiscount' for a non-student aged 15.");
        }

        [TestMethod]
        public void Evaluate_SimpleRule_AdultDiscount()
        {
            // Arrange
            DmnModel? dmnModel = DmnParser.ParseDmn(SimpleDmnXml);
            Assert.IsNotNull(dmnModel, "DMN model should not be null.");

            Decision? decision = dmnModel.Decisions.Find(d => d.Id == "DiscountDecision");
            Assert.IsNotNull(decision, "Discount decision should be found.");
            Assert.IsInstanceOfType(decision.DecisionLogic, typeof(DecisionTable), "Decision logic should be a DecisionTable.");

            DecisionTable decisionTable = (DecisionTable)decision.DecisionLogic;
            using GpuDecisionTableRepresentation gpuData = DmnToGpuConverter.ConvertDecisionTableToGpuRepresentation(decisionTable, TestDevice);
            RulesGPUEngine engine = new RulesGPUEngine(TestDevice);

            IReadOnlyDictionary<string, object> inputs = new Dictionary<string, object>
            {
                { "Age", 30.0F },
                { "IsStudent", false }
            };

            // Act
            object? result = engine.Evaluate(gpuData, inputs);

            // Assert
            Assert.IsNotNull(result, "Result should not be null.");
            Assert.IsInstanceOfType(result, typeof(IReadOnlyDictionary<string, object>), "Result should be a dictionary.");
            
            IReadOnlyDictionary<string, object> output = (IReadOnlyDictionary<string, object>)result;
            string? discount = output["Discount"] as string;
            Assert.AreEqual("AdultDiscount", discount, "Expected 'AdultDiscount' for a non-student aged 30.");
        }

        [TestMethod]
        public void Evaluate_LoanEligibility_UnderageDeclined()
        {
            DmnModel? dmnModel = DmnParser.ParseDmn(LoanEligibilityDmnXml);
            Assert.IsNotNull(dmnModel, "DMN model should not be null.");
            Decision? decision = dmnModel.Decisions.Find(d => d.Id == "LoanDecision");
            Assert.IsNotNull(decision, "LoanDecision should be found.");
            Assert.IsInstanceOfType(decision.DecisionLogic, typeof(DecisionTable), "Decision logic should be a DecisionTable.");

            DecisionTable decisionTable = (DecisionTable)decision.DecisionLogic;
            using GpuDecisionTableRepresentation gpuData = DmnToGpuConverter.ConvertDecisionTableToGpuRepresentation(decisionTable, TestDevice);
            RulesGPUEngine engine = new RulesGPUEngine(TestDevice);

            IReadOnlyDictionary<string, object> inputs = new Dictionary<string, object>
            {
                { "Applicant Age", 17.0F },
                { "Applicant Income", 50000.0F },
                { "Credit Score", 700.0F }
            };

            object? result = engine.Evaluate(gpuData, inputs);
            Assert.IsNotNull(result, "Result should not be null.");
            IReadOnlyDictionary<string, object> output = (IReadOnlyDictionary<string, object>)result;
            string? decisionResult = output["Decision"] as string;
            Assert.AreEqual("Declined", decisionResult, "Expected 'Declined' for underage applicant.");
        }

        [TestMethod]
        public void Evaluate_LoanEligibility_ManualReview()
        {
            DmnModel? dmnModel = DmnParser.ParseDmn(LoanEligibilityDmnXml);
            Assert.IsNotNull(dmnModel, "DMN model should not be null.");
            Decision? decision = dmnModel.Decisions.Find(d => d.Id == "LoanDecision");
            Assert.IsNotNull(decision, "LoanDecision should be found.");
            Assert.IsInstanceOfType(decision.DecisionLogic, typeof(DecisionTable), "Decision logic should be a DecisionTable.");

            DecisionTable decisionTable = (DecisionTable)decision.DecisionLogic;
            using GpuDecisionTableRepresentation gpuData = DmnToGpuConverter.ConvertDecisionTableToGpuRepresentation(decisionTable, TestDevice);
            RulesGPUEngine engine = new RulesGPUEngine(TestDevice);

            IReadOnlyDictionary<string, object> inputs = new Dictionary<string, object>
            {
                { "Applicant Age", 22.0F },
                { "Applicant Income", 25000.0F },
                { "Credit Score", 650.0F }
            };

            object? result = engine.Evaluate(gpuData, inputs);
            Assert.IsNotNull(result, "Result should not be null.");
            IReadOnlyDictionary<string, object> output = (IReadOnlyDictionary<string, object>)result;
            string? decisionResult = output["Decision"] as string;
            Assert.AreEqual("Manual Review", decisionResult, "Expected 'Manual Review' for low income but acceptable credit.");
        }

        [TestMethod]
        public void Evaluate_LoanEligibility_Approved()
        {
            DmnModel? dmnModel = DmnParser.ParseDmn(LoanEligibilityDmnXml);
            Assert.IsNotNull(dmnModel, "DMN model should not be null.");
            Decision? decision = dmnModel.Decisions.Find(d => d.Id == "LoanDecision");
            Assert.IsNotNull(decision, "LoanDecision should be found.");
            Assert.IsInstanceOfType(decision.DecisionLogic, typeof(DecisionTable), "Decision logic should be a DecisionTable.");

            DecisionTable decisionTable = (DecisionTable)decision.DecisionLogic;
            using GpuDecisionTableRepresentation gpuData = DmnToGpuConverter.ConvertDecisionTableToGpuRepresentation(decisionTable, TestDevice);
            RulesGPUEngine engine = new RulesGPUEngine(TestDevice);

            IReadOnlyDictionary<string, object> inputs = new Dictionary<string, object>
            {
                { "Applicant Age", 30.0F },
                { "Applicant Income", 50000.0F },
                { "Credit Score", 720.0F }
            };

            object? result = engine.Evaluate(gpuData, inputs);
            Assert.IsNotNull(result, "Result should not be null.");
            IReadOnlyDictionary<string, object> output = (IReadOnlyDictionary<string, object>)result;
            string? decisionResult = output["Decision"] as string;
            Assert.AreEqual("Approved", decisionResult, "Expected 'Approved' for meeting all thresholds.");
        }

        [TestMethod]
        public void Evaluate_LoanEligibility_PoorCreditDeclined()
        {
            DmnModel? dmnModel = DmnParser.ParseDmn(LoanEligibilityDmnXml);
            Assert.IsNotNull(dmnModel, "DMN model should not be null.");
            Decision? decision = dmnModel.Decisions.Find(d => d.Id == "LoanDecision");
            Assert.IsNotNull(decision, "LoanDecision should be found.");
            Assert.IsInstanceOfType(decision.DecisionLogic, typeof(DecisionTable), "Decision logic should be a DecisionTable.");

            DecisionTable decisionTable = (DecisionTable)decision.DecisionLogic;
            using GpuDecisionTableRepresentation gpuData = DmnToGpuConverter.ConvertDecisionTableToGpuRepresentation(decisionTable, TestDevice);
            RulesGPUEngine engine = new RulesGPUEngine(TestDevice);

            IReadOnlyDictionary<string, object> inputs = new Dictionary<string, object>
            {
                { "Applicant Age", 40.0F },
                { "Applicant Income", 80000.0F },
                { "Credit Score", 580.0F }
            };

            object? result = engine.Evaluate(gpuData, inputs);
            Assert.IsNotNull(result, "Result should not be null.");
            IReadOnlyDictionary<string, object> output = (IReadOnlyDictionary<string, object>)result;
            string? decisionResult = output["Decision"] as string;
            Assert.AreEqual("Declined", decisionResult, "Expected 'Declined' for poor credit.");
        }

        [TestMethod]
        public void Evaluate_LoanEligibility_BatchParallel()
        {
            // Arrange
            DmnModel? dmnModel = DmnParser.ParseDmn(LoanEligibilityDmnXml);
            Assert.IsNotNull(dmnModel, "DMN model should not be null.");

            Decision? decision = dmnModel.Decisions.Find(d => d.Id == "LoanDecision");
            Assert.IsNotNull(decision, "LoanDecision should be found.");
            Assert.IsInstanceOfType(decision.DecisionLogic, typeof(DecisionTable), "Decision logic should be a DecisionTable.");

            DecisionTable decisionTable = (DecisionTable)decision.DecisionLogic;
            using GpuDecisionTableRepresentation gpuData = DmnToGpuConverter.ConvertDecisionTableToGpuRepresentation(decisionTable, TestDevice);
            RulesGPUEngine engine = new RulesGPUEngine(TestDevice);

            List<IReadOnlyDictionary<string, object>> batchInputs = new List<IReadOnlyDictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    { "Applicant Age", 17.0F },
                    { "Applicant Income", 50000.0F },
                    { "Credit Score", 700.0F }
                },
                new Dictionary<string, object>
                {
                    { "Applicant Age", 22.0F },
                    { "Applicant Income", 25000.0F },
                    { "Credit Score", 650.0F }
                },
                new Dictionary<string, object>
                {
                    { "Applicant Age", 30.0F },
                    { "Applicant Income", 50000.0F },
                    { "Credit Score", 720.0F }
                },
                new Dictionary<string, object>
                {
                    { "Applicant Age", 40.0F },
                    { "Applicant Income", 80000.0F },
                    { "Credit Score", 580.0F }
                }
            };

            List<string> expectedResults = new List<string>
            {
                "Declined",
                "Manual Review",
                "Approved",
                "Declined"
            };

            // Act
            IReadOnlyList<object?> actualResults = engine.Evaluate(gpuData, batchInputs);

            // Assert
            Assert.IsNotNull(actualResults, "Actual results list should not be null.");
            Assert.AreEqual(expectedResults.Count, actualResults.Count, "Number of actual results should match number of expected results.");

            for (int i = 0; i < expectedResults.Count; i++)
            {
                object? result = actualResults[i];
                Assert.IsNotNull(result, $"Result for input {i} should not be null.");
                Assert.IsInstanceOfType(result, typeof(IReadOnlyDictionary<string, object>), $"Result for input {i} should be a dictionary.");

                IReadOnlyDictionary<string, object> output = (IReadOnlyDictionary<string, object>)result;
                string? decisionResult = output["Decision"] as string;
                Assert.AreEqual(expectedResults[i], decisionResult, $"Expected '{expectedResults[i]}' for input {i}.");
            }
        }

        [TestMethod]
        public void Test_RandomRuleSets_WithInverseProblemGeneration()
        {
            List<string> allErrors = new List<string>();
            int numRuleSets = 100; // As per specification
            int numTestPairsPerSet = 100; // As per specification

            for (int i = 0; i < numRuleSets; i++)
            {
                // 1. Generate a random DMN Decision Table
                DecisionTable randomDecisionTable = DecisionTableFactory.CreateRandomDecisionTable(
                    minInputs: 2, maxInputs: 4,
                    minOutputs: 1, maxOutputs: 2,
                    minRules: 3, maxRules: 6
                );
                DmnModel randomDmnModel = new DmnModel();
                randomDmnModel.Decisions.Add(new Decision { Id = $"RandomDecision_{i}", Name = $"Random Decision {i}", DecisionLogic = randomDecisionTable });

                DecisionTable? decisionTable = randomDmnModel.Decisions[0].DecisionLogic as DecisionTable;

                if (decisionTable == null)
                {
                    continue;
                }

                // Convert to GPU representation
                using (GpuDecisionTableRepresentation gpuData = DmnToGpuConverter.ConvertDecisionTableToGpuRepresentation(decisionTable, TestDevice))
                {
                    RulesGPUEngine engine = new RulesGPUEngine(TestDevice);

                    List<IReadOnlyDictionary<string, object>> batchProblems = new List<IReadOnlyDictionary<string, object>>();
                    List<IReadOnlyDictionary<string, object>> batchExpectedSolutions = new List<IReadOnlyDictionary<string, object>>();

                    for (int j = 0; j < numTestPairsPerSet; j++)
                    {
                        (IReadOnlyDictionary<string, object> Problem, IReadOnlyDictionary<string, object> Solution)? pair =
                            SyntheticProblemSolutionPairGenerator.GeneratePair(randomDecisionTable);

                        if (pair.HasValue)
                        {
                            batchProblems.Add(pair.Value.Problem);
                            batchExpectedSolutions.Add(pair.Value.Solution);
                        }
                        else
                        {
                            allErrors.Add($"Rule set {i + 1}, Test {j + 1}: Failed to generate problem/solution pair for the random DMN.");
                        }
                    }

                    if (batchProblems.Count > 0)
                    {
                        // Act: Evaluate the batch
                        IReadOnlyList<object?> actualResults = engine.Evaluate(gpuData, batchProblems);

                        // Assert and collect errors for the batch
                        for (int k = 0; k < batchProblems.Count; k++)
                        {
                            IReadOnlyDictionary<string, object> expected = batchExpectedSolutions[k];
                            object? actualResult = actualResults[k];

                            if (actualResult is not IReadOnlyDictionary<string, object> actualOutput)
                            {
                                allErrors.Add($"Rule set {i + 1}, Test {k + 1}: Expected dictionary output, got '{actualResult?.GetType().Name ?? "null"}'. Problem: {string.Join(", ", batchProblems[k].Select(kv => $"{kv.Key}={kv.Value}"))}.");
                                continue;
                            }

                            foreach (KeyValuePair<string, object> expectedKvp in expected)
                            {
                                if (actualOutput.TryGetValue(expectedKvp.Key, out object? actualValue))
                                {
                                    // Robust comparison for different types
                                    string? outputKey = expectedKvp.Key;
                                    string expectedTypeRef = string.Empty;
                                    OutputClause? correspondingOutputClause = randomDecisionTable.Outputs.FirstOrDefault(oc => oc.Name == outputKey || oc.Id == outputKey);
                                    if (correspondingOutputClause != null)
                                    {
                                        expectedTypeRef = correspondingOutputClause.TypeRef; // Get the original typeRef for comparison
                                    }

                                    // Handle numeric types (number and integer) using double comparison for robustness
                                    if (expectedTypeRef.Equals("number", StringComparison.OrdinalIgnoreCase) || expectedTypeRef.Equals("integer", StringComparison.OrdinalIgnoreCase))
                                    {
                                        double expectedNumeric = double.NaN;
                                        double actualNumeric = double.NaN;

                                        // Convert expected value to double
                                        if (expectedKvp.Value is int iVal) expectedNumeric = iVal;
                                        else if (expectedKvp.Value is float fVal) expectedNumeric = fVal;
                                        else if (expectedKvp.Value is double dVal) expectedNumeric = dVal;
                                        else if (expectedKvp.Value is decimal decVal) expectedNumeric = (double)decVal;

                                        // Convert actual value to double (it usually resolves to double, type-check for safety)
                                        if (actualValue is double dActVal) actualNumeric = dActVal;
                                        else if (actualValue is float fActVal) actualNumeric = fActVal;
                                        else if (actualValue is int iActVal) actualNumeric = iActVal;

                                        if (double.IsNaN(expectedNumeric) || double.IsNaN(actualNumeric) || System.Math.Abs(expectedNumeric - actualNumeric) > 0.01)
                                        {
                                            allErrors.Add($"Rule set {i + 1}, Test {k + 1}, Output '{expectedKvp.Key}': Numeric mismatch. Expected {expectedKvp.Value?.GetType().Name} {expectedKvp.Value}, numerically {expectedNumeric}; Got {actualValue?.GetType().Name} {actualValue}, numerically {actualNumeric}. Problem: {string.Join(", ", batchProblems[k].Select(kv => $"{kv.Key}={kv.Value}"))}.");
                                        }
                                    }
                                    // Handle Date/DateTime types
                                    else if (expectedTypeRef.Equals("date", StringComparison.OrdinalIgnoreCase) || expectedTypeRef.Equals("datetime", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (expectedKvp.Value is DateTime expectedDateTime && actualValue is DateTime actualDateTime)
                                        {
                                            if (expectedDateTime != actualDateTime)
                                            {
                                                allErrors.Add($"Rule set {i + 1}, Test {k + 1}, Output '{expectedKvp.Key}': Date/DateTime mismatch. Expected '{expectedDateTime}', Got '{actualDateTime}'. Problem: {string.Join(", ", batchProblems[k].Select(kv => $"{kv.Key}={kv.Value}"))}.");
                                            }
                                        }
                                        else
                                        {
                                             allErrors.Add($"Rule set {i + 1}, Test {k + 1}, Output '{expectedKvp.Key}': Date/DateTime unexpected types. Expected value type was {expectedKvp.Value?.GetType().Name}, Actual value type was {actualValue?.GetType().Name}. Problem: {string.Join(", ", batchProblems[k].Select(kv => $"{kv.Key}={kv.Value}"))}.");
                                        }
                                    }
                                    // Handle String types
                                    else if (expectedKvp.Value is string expectedString && actualValue is string actualString)
                                    {
                                        if (!System.String.Equals(expectedString, actualString, StringComparison.Ordinal))
                                        {
                                            allErrors.Add($"Rule set {i + 1}, Test {k + 1}, Output '{expectedKvp.Key}': String mismatch. Expected '{expectedString}', Got '{actualString}'. Problem: {string.Join(", ", batchProblems[k].Select(kv => $"{kv.Key}={kv.Value}"))}.");
                                        }
                                    }
                                    // Handle Boolean types
                                    else if (expectedKvp.Value is bool expectedBool && actualValue is bool actualBool)
                                    {
                                        if (expectedBool != actualBool)
                                        {
                                            allErrors.Add($"Rule set {i + 1}, Test {k + 1}, Output '{expectedKvp.Key}': Boolean mismatch. Expected {expectedBool}, Got {actualBool}. Problem: {string.Join(", ", batchProblems[k].Select(kv => $"{kv.Key}={kv.Value}"))}.");
                                        }
                                    }
                                    // Fallback for unexpected type combinations or general Object.Equals
                                    else if (!System.Object.Equals(expectedKvp.Value, actualValue))
                                    {
                                        allErrors.Add($"Rule set {i + 1}, Test {k + 1}, Output '{expectedKvp.Key}': Type or Value mismatch. Expected type '{expectedKvp.Value?.GetType().Name}', value '{expectedKvp.Value}'; Got type '{actualValue?.GetType().Name}', value '{actualValue}'. Problem: {string.Join(", ", batchProblems[k].Select(kv => $"{kv.Key}={kv.Value}"))}.");
                                    }
                                }
                                else
                                {
                                    allErrors.Add($"Rule set {i + 1}, Test {k + 1}, Output '{expectedKvp.Key}': Expected key not found in actual output. Problem: {string.Join(", ", batchProblems[k].Select(kv => $"{kv.Key}={kv.Value}"))}.");
                                }
                            }
                        }
                    }
                }
            }

            // Output all errors for the agent to capture
            if (allErrors.Count > 0)
            {
                Console.WriteLine($"--- Errors from Random Rule Set Tests ({allErrors.Count} total) ---");
                foreach (string error in allErrors)
                {
                    Console.WriteLine(error);
                }
            }
            Assert.IsTrue(allErrors.Count == 0, $"Random rule set tests failed with {allErrors.Count} errors. See console output for details.");
        }
    }
}