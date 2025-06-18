//Copyright Warren Harding 2025.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RulesDMN.Models;
using RulesDMN;

namespace RulesDMNTest
{
    [TestClass]
    public class DmnParserTests
    {
        [TestMethod]
        public void ParseDmn_ValidXml_ReturnsDmnModel()
        {
            // Sample DMN XML with a simple decision table
            string dmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                <definitions id=""definitions_1"" name=""MyDMN"" xmlns=""https://www.omg.org/spec/DMN/20180521/MODEL/"">
                    <decision id=""decision_1"" name=""My Decision"">
                        <decisionTable id=""decisionTable_1"" hitPolicy=""UNIQUE"">
                            <input id=""input_1"">
                                <inputExpression id=""inputExpression_1"" typeRef=""string"">
                                    <text>inputVariable</text>
                                </inputExpression>
                            </input>
                            <output id=""output_1"" name=""outputVariable"" typeRef=""string""/>
                            <rule id=""rule_1"">
                                <inputEntry id=""inputEntry_1"">
                                    <text>""TestValue""</text>
                                </inputEntry>
                                <outputEntry id=""outputEntry_1"">
                                    <text>""ResultValue""</text>
                                </outputEntry>
                            </rule>
                        </decisionTable>
                    </decision>
                </definitions>";

            DmnModel? dmnModel = DmnParser.ParseDmn(dmnXml);

            Assert.IsNotNull(dmnModel, "DMN model should not be null.");
            Assert.AreEqual("definitions_1", dmnModel.Id);
            Assert.AreEqual("MyDMN", dmnModel.Name);
            Assert.IsNotNull(dmnModel.Decisions, "Decisions list should not be null.");
            Assert.AreEqual(1, dmnModel.Decisions.Count, "Expected one decision.");

            Decision decision = dmnModel.Decisions[0];
            Assert.AreEqual("decision_1", decision.Id);
            Assert.AreEqual("My Decision", decision.Name);
            Assert.IsNotNull(decision.DecisionLogic, "Decision logic should not be null.");
            Assert.IsInstanceOfType(decision.DecisionLogic, typeof(DecisionTable), "Decision logic should be a DecisionTable.");

            DecisionTable decisionTable = (DecisionTable)decision.DecisionLogic;
            Assert.AreEqual(HitPolicy.Unique, decisionTable.HitPolicy);
            Assert.AreEqual(1, decisionTable.Inputs.Count, "Expected one input clause.");
            Assert.AreEqual(1, decisionTable.Outputs.Count, "Expected one output clause.");
            Assert.AreEqual(1, decisionTable.Rules.Count, "Expected one rule.");

            InputClause inputClause = decisionTable.Inputs[0];
            Assert.AreEqual("input_1", inputClause.Id);
            Assert.IsNotNull(inputClause.Expression, "Input expression should not be null.");
            Assert.AreEqual("inputVariable", inputClause.Expression.Text);
            Assert.AreEqual("string", inputClause.Expression.TypeRef);

            OutputClause outputClause = decisionTable.Outputs[0];
            Assert.AreEqual("output_1", outputClause.Id);
            Assert.AreEqual("outputVariable", outputClause.Name);
            Assert.AreEqual("string", outputClause.TypeRef);

            Rule rule = decisionTable.Rules[0];
            Assert.AreEqual("rule_1", rule.Id);
            Assert.AreEqual(1, rule.InputEntries.Count, "Expected one input entry.");
            Assert.AreEqual(1, rule.OutputEntries.Count, "Expected one output entry.");
            Assert.AreEqual(@"""TestValue""", rule.InputEntries[0].Text);
            Assert.AreEqual(@"""ResultValue""", rule.OutputEntries[0].Text);
        }

        [TestMethod]
        public void ParseDmn_EmptyOrWhitespaceXml_ReturnsNull()
        {
            Assert.IsNull(DmnParser.ParseDmn(""), "Empty XML should return null.");
            Assert.IsNull(DmnParser.ParseDmn("   "), "Whitespace XML should return null.");
        }

        [TestMethod]
        public void ParseDmn_MalformedXml_ReturnsNull()
        {
            string malformedXml = "<definitions><decision></invalid>"; // Malformed XML

            DmnModel? dmnModel = DmnParser.ParseDmn(malformedXml);

            Assert.IsNull(dmnModel, "Malformed XML should return null.");
        }

        [TestMethod]
        public void ParseDmn_NoDefinitionsElement_ReturnsNull()
        {
            string xmlWithoutDefinitions = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                <root>
                    <decision id=""decision_1"" name=""My Decision""/>
                </root>";

            DmnModel? dmnModel = DmnParser.ParseDmn(xmlWithoutDefinitions);

            Assert.IsNull(dmnModel, "XML without <definitions> element should return null.");
        }

        [TestMethod]
        public void ParseDmn_DefinitionsWithoutIdOrName_ParsesCorrectlyWithDefaults()
        {
            string dmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                <definitions xmlns=""https://www.omg.org/spec/DMN/20180521/MODEL/"">
                    <decision id=""decision_1"" name=""My Decision""/>
                </definitions>";

            DmnModel? dmnModel = DmnParser.ParseDmn(dmnXml);

            Assert.IsNotNull(dmnModel);
            Assert.AreEqual(string.Empty, dmnModel.Id);
            Assert.AreEqual(string.Empty, dmnModel.Name);
        }

        [TestMethod]
        public void ParseDmn_DecisionWithoutIdOrName_NotAdded()
        {
            string dmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                <definitions id=""def1"" name=""defName"" xmlns=""https://www.omg.org/spec/DMN/20180521/MODEL/"">
                    <decision>
                        <literalExpression id=""literalExpression_1"" typeRef=""string"">
                            <text>""Hello""</text>
                        </literalExpression>
                    </decision>
                    <decision name=""DecisionOnlyName"">
                        <literalExpression id=""literalExpression_2"" typeRef=""string"">
                            <text>""Hello""</text>
                        </literalExpression>
                    </decision>
                     <decision id=""DecisionOnlyId"">
                        <literalExpression id=""literalExpression_3"" typeRef=""string"">
                            <text>""Hello""</text>
                        </literalExpression>
                    </decision>
                     <decision id=""decision_valid"" name=""ValidDecision"">
                        <literalExpression id=""literalExpression_4"" typeRef=""string"">
                            <text>""Hello""</text>
                        </literalExpression>
                    </decision>
                </definitions>";

            DmnModel? dmnModel = DmnParser.ParseDmn(dmnXml);

            Assert.IsNotNull(dmnModel);
            Assert.AreEqual(1, dmnModel.Decisions.Count, "Only the decision with both ID and Name should be added.");
            Assert.AreEqual("decision_valid", dmnModel.Decisions[0].Id);
            Assert.AreEqual("ValidDecision", dmnModel.Decisions[0].Name);
        }


        [TestMethod]
        public void ParseDmn_WithLiteralExpression_ParsesCorrectly()
        {
            string dmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                <definitions id=""d_le"" name=""LiteralExpressionDMN"" xmlns=""https://www.omg.org/spec/DMN/20180521/MODEL/"">
                    <decision id=""decision_le"" name=""LE Decision"">
                        <literalExpression id=""le_expr"" typeRef=""string"">
                            <text>""Hello World""</text>
                        </literalExpression>
                    </decision>
                </definitions>";

            DmnModel? dmnModel = DmnParser.ParseDmn(dmnXml);

            Assert.IsNotNull(dmnModel);
            Assert.AreEqual(1, dmnModel.Decisions.Count);
            Decision decision = dmnModel.Decisions[0];
            Assert.IsNotNull(decision.DecisionLogic);
            Assert.IsInstanceOfType(decision.DecisionLogic, typeof(LiteralExpression));
            
            LiteralExpression literalExpression = (LiteralExpression)decision.DecisionLogic;
            Assert.AreEqual(@"""Hello World""", literalExpression.Text);
            Assert.AreEqual("string", literalExpression.TypeRef);
        }

        [TestMethod]
        public void ParseDmn_LiteralExpressionMissingTextAndTypeRef_ReturnsNull()
        {
            string dmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                <definitions id=""d_le"" name=""LiteralExpressionDMN"" xmlns=""https://www.omg.org/spec/DMN/20180521/MODEL/"">
                    <decision id=""decision_le"" name=""LE Decision"">
                        <literalExpression id=""le_expr"">
                        </literalExpression>
                    </decision>
                </definitions>";

            DmnModel? dmnModel = DmnParser.ParseDmn(dmnXml);

            Assert.IsNotNull(dmnModel);
            Assert.AreEqual(1, dmnModel.Decisions.Count);
            Decision decision = dmnModel.Decisions[0];
            Assert.IsNull(decision.DecisionLogic, "Decision logic should be null if literal expression has no text or typeRef.");
        }

        [TestMethod]
        public void ParseDmn_DecisionTableDifferentHitPolicies_ParsesCorrectly()
        {
            string dmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                <definitions id=""d_hp"" name=""HitPolicyDMN"" xmlns=""https://www.omg.org/spec/DMN/20180521/MODEL/"">
                    <decision id=""decision_hp_unique"" name=""Unique HP"">
                        <decisionTable id=""dt_unique"" hitPolicy=""UNIQUE""/>
                    </decision>
                    <decision id=""decision_hp_any"" name=""Any HP"">
                        <decisionTable id=""dt_any"" hitPolicy=""ANY""/>
                    </decision>
                    <decision id=""decision_hp_collect"" name=""Collect HP"">
                        <decisionTable id=""dt_collect"" hitPolicy=""COLLECT""/>
                    </decision>
                    <decision id=""decision_hp_default"" name=""Default HP"">
                        <decisionTable id=""dt_default""/> <!-- Default should be Unique -->
                    </decision>
                </definitions>";

            DmnModel? dmnModel = DmnParser.ParseDmn(dmnXml);

            Assert.IsNotNull(dmnModel);
            Assert.AreEqual(4, dmnModel.Decisions.Count);

            DecisionTable? dtUnique = (DecisionTable?)dmnModel.Decisions[0].DecisionLogic;
            Assert.IsNotNull(dtUnique);
            Assert.AreEqual(HitPolicy.Unique, dtUnique.HitPolicy);

            DecisionTable? dtAny = (DecisionTable?)dmnModel.Decisions[1].DecisionLogic;
            Assert.IsNotNull(dtAny);
            Assert.AreEqual(HitPolicy.Any, dtAny.HitPolicy);

            DecisionTable? dtCollect = (DecisionTable?)dmnModel.Decisions[2].DecisionLogic;
            Assert.IsNotNull(dtCollect);
            Assert.AreEqual(HitPolicy.Collect, dtCollect.HitPolicy);

            DecisionTable? dtDefault = (DecisionTable?)dmnModel.Decisions[3].DecisionLogic;
            Assert.IsNotNull(dtDefault);
            Assert.AreEqual(HitPolicy.Unique, dtDefault.HitPolicy); // Default hit policy
        }

        [TestMethod]
        public void ParseDmn_DecisionTableWithMultipleRulesAndEntries_ParsesCorrectly()
        {
            string dmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                <definitions xmlns=""https://www.omg.org/spec/DMN/20180521/MODEL/"">
                    <decision id=""decision_rules"" name=""Rules Decision"">
                        <decisionTable id=""dt_rules"">
                            <input id=""in1""><inputExpression id=""inExpr1"" typeRef=""string""><text>x</text></inputExpression></input>
                            <output id=""out1"" name=""y"" typeRef=""string""/>
                            <rule id=""r1"">
                                <inputEntry id=""ie1""><text>""A""</text></inputEntry>
                                <outputEntry id=""oe1""><text>""Result A""</text></outputEntry>
                            </rule>
                            <rule id=""r2"">
                                <inputEntry id=""ie2""><text>""B""</text></inputEntry>
                                <outputEntry id=""oe2""><text>""Result B""</text></outputEntry>
                            </rule>
                        </decisionTable>
                    </decision>
                </definitions>";

            DmnModel? dmnModel = DmnParser.ParseDmn(dmnXml);

            Assert.IsNotNull(dmnModel);
            Assert.AreEqual(1, dmnModel.Decisions.Count);
            Decision decision = dmnModel.Decisions[0];
            Assert.IsInstanceOfType(decision.DecisionLogic, typeof(DecisionTable));
            DecisionTable decisionTable = (DecisionTable)decision.DecisionLogic;

            Assert.AreEqual(2, decisionTable.Rules.Count);

            Rule rule1 = decisionTable.Rules[0];
            Assert.AreEqual("r1", rule1.Id);
            Assert.AreEqual(1, rule1.InputEntries.Count);
            Assert.AreEqual(1, rule1.OutputEntries.Count);
            Assert.AreEqual(@"""A""", rule1.InputEntries[0].Text);
            Assert.AreEqual(@"""Result A""", rule1.OutputEntries[0].Text);

            Rule rule2 = decisionTable.Rules[1];
            Assert.AreEqual("r2", rule2.Id);
            Assert.AreEqual(1, rule2.InputEntries.Count);
            Assert.AreEqual(1, rule2.OutputEntries.Count);
            Assert.AreEqual(@"""B""", rule2.InputEntries[0].Text);
            Assert.AreEqual(@"""Result B""", rule2.OutputEntries[0].Text);
        }

        [TestMethod]
        public void ParseDmn_DecisionTableRuleMissingEntryText_EmptyStrings()
        {
            string dmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                <definitions xmlns=""https://www.omg.org/spec/DMN/20180521/MODEL/"">
                    <decision id=""decision_empty_entries"" name=""Empty Entries Decision"">
                        <decisionTable>
                            <input id=""in1""><inputExpression id=""inExpr1"" typeRef=""string""><text>x</text></inputExpression></input>
                            <output id=""out1"" name=""y"" typeRef=""string""/>
                            <rule id=""r1"">
                                <inputEntry id=""ie1""></inputEntry>
                                <outputEntry id=""oe1""></outputEntry>
                            </rule>
                        </decisionTable>
                    </decision>
                </definitions>";

            DmnModel? dmnModel = DmnParser.ParseDmn(dmnXml);
            Assert.IsNotNull(dmnModel);
            DecisionTable? decisionTable = (DecisionTable?)dmnModel.Decisions[0].DecisionLogic;
            Assert.IsNotNull(decisionTable);

            Rule rule = decisionTable.Rules[0];
            Assert.AreEqual(1, rule.InputEntries.Count);
            Assert.AreEqual(1, rule.OutputEntries.Count);
            Assert.AreEqual(string.Empty, rule.InputEntries[0].Text);
            Assert.AreEqual(string.Empty, rule.OutputEntries[0].Text);
        }

        [TestMethod]
        public void ParseDmn_DecisionTableInputOutputMissingAttributes_EmptyStringsOrNulls()
        {
            string dmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                <definitions xmlns=""https://www.omg.org/spec/DMN/20180521/MODEL/"">
                    <decision id=""decision_missing_attrs"" name=""Missing Attributes Decision"">
                        <decisionTable>
                            <input>
                                <inputExpression>
                                    <text>no id or typeRef</text>
                                </inputExpression>
                            </input>
                            <output/>
                            <rule id=""r1"">
                                <inputEntry><text>val</text></inputEntry>
                                <outputEntry><text>res</text></outputEntry>
                            </rule>
                        </decisionTable>
                    </decision>
                </definitions>";

            DmnModel? dmnModel = DmnParser.ParseDmn(dmnXml);
            Assert.IsNotNull(dmnModel);
            DecisionTable? decisionTable = (DecisionTable?)dmnModel.Decisions[0].DecisionLogic;
            Assert.IsNotNull(decisionTable);

            InputClause inputClause = decisionTable.Inputs[0];
            Assert.IsNull(inputClause.Id);
            Assert.IsNotNull(inputClause.Expression);
            Assert.AreEqual("no id or typeRef", inputClause.Expression.Text);
            // Default value from LiteralExpression constructor is string.Empty if text is not null and typeRef is null.
            // Check DmnParser.cs: LiteralExpression needs text OR typeRef to return new LiteralExpression.
            // If Text is not null and TypeRef is null, it returns new LiteralExpression { Text = text, TypeRef = string.Empty }
            Assert.AreEqual(string.Empty, inputClause.Expression.TypeRef);

            OutputClause outputClause = decisionTable.Outputs[0];
            Assert.IsNull(outputClause.Id);
            Assert.AreEqual(string.Empty, outputClause.Name);
            Assert.AreEqual(string.Empty, outputClause.TypeRef);
        }

        [TestMethod]
        public void ParseDmn_DecisionWithoutDecisionLogic_DecisionLogicIsNull()
        {
            string dmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                <definitions id=""def1"" name=""defName"" xmlns=""https://www.omg.org/spec/DMN/20180521/MODEL/"">
                    <decision id=""decision_no_logic"" name=""No Logic Decision"">
                    </decision>
                </definitions>";

            DmnModel? dmnModel = DmnParser.ParseDmn(dmnXml);
            Assert.IsNotNull(dmnModel);
            Decision decision = dmnModel.Decisions[0];
            Assert.AreEqual(1, dmnModel.Decisions.Count);
            Assert.IsNull(decision.DecisionLogic, "Decision logic should be null if no decisionTable or literalExpression is found.");
        }
    }
}