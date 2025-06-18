//Copyright Warren Harding 2025.
using System.Xml.Linq;
using System.Collections.Generic;
using RulesDMN.Models;

namespace RulesDMN
{
    public static class DmnParser
    {
        public static DmnModel? ParseDmn(string dmnXml)
        {
            if (string.IsNullOrWhiteSpace(dmnXml))
            {
                return null;
            }

            try
            {
                XDocument doc = XDocument.Parse(dmnXml);
                XNamespace dmnNs = "https://www.omg.org/spec/DMN/20180521/MODEL/";
                XElement? definitionsElement = doc.Element(dmnNs + "definitions");

                if (definitionsElement is null)
                {
                    return null;
                }

                DmnModel dmnModel = new DmnModel
                {
                    Id = (string?)definitionsElement.Attribute("id") ?? string.Empty,
                    Name = (string?)definitionsElement.Attribute("name") ?? string.Empty
                };

                foreach (XElement decisionElement in definitionsElement.Elements(dmnNs + "decision"))
                {
                    string? id = (string?)decisionElement.Attribute("id");
                    string? name = (string?)decisionElement.Attribute("name");

                    // Decisions must have both a non-null and non-empty ID and Name to be added to the model.
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                    {
                        Decision decision = new Decision
                        {
                            Id = id,
                            Name = name,
                            DecisionLogic = ParseDecisionLogic(decisionElement, dmnNs)
                        };
                        dmnModel.Decisions.Add(decision);
                    }
                }

                return dmnModel;
            }
            catch (System.Xml.XmlException)
            {
                return null;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        private static IExpression? ParseDecisionLogic(XElement decisionElement, XNamespace dmnNs)
        {
            XElement? decisionTableElement = decisionElement.Element(dmnNs + "decisionTable");
            if (decisionTableElement is not null)
            {
                return ParseDecisionTable(decisionTableElement, dmnNs);
            }
            
            XElement? literalExpressionElement = decisionElement.Element(dmnNs + "literalExpression");
            if (literalExpressionElement is not null)
            {
                return ParseLiteralExpression(literalExpressionElement, dmnNs);
            }
            // If neither decisionTable nor literalExpression is found, return null.
            return null; 
        }

        private static DecisionTable ParseDecisionTable(XElement decisionTableElement, XNamespace dmnNs)
        {
            DecisionTable decisionTable = new DecisionTable();

            string? hitPolicyAttr = (string?)decisionTableElement.Attribute("hitPolicy");
            if (hitPolicyAttr is not null && System.Enum.TryParse(hitPolicyAttr, true, out HitPolicy policy))
            {
                decisionTable.HitPolicy = policy;
            }
            else
            {
                decisionTable.HitPolicy = HitPolicy.Unique;
            }

            foreach (XElement inputElement in decisionTableElement.Elements(dmnNs + "input"))
            {
                InputClause inputClause = new InputClause();
                inputClause.Id = (string?)inputElement.Attribute("id");

                XElement? inputExpressionElement = inputElement.Element(dmnNs + "inputExpression");
                // Assign null to Expression if inputExpressionElement is null
                inputClause.Expression = inputExpressionElement is not null
                    ? ParseLiteralExpression(inputExpressionElement, dmnNs)
                    : null;
                
                decisionTable.Inputs.Add(inputClause);
            }

            foreach (XElement outputElement in decisionTableElement.Elements(dmnNs + "output"))
            {
                OutputClause outputClause = new OutputClause();
                outputClause.Id = (string?)outputElement.Attribute("id");
                outputClause.Name = (string?)outputElement.Attribute("name") ?? string.Empty;
                outputClause.TypeRef = (string?)outputElement.Attribute("typeRef") ?? string.Empty;
                
                decisionTable.Outputs.Add(outputClause);
            }

            foreach (XElement ruleElement in decisionTableElement.Elements(dmnNs + "rule"))
            {
                Rule rule = new Rule();
                rule.Id = (string?)ruleElement.Attribute("id");

                foreach (XElement inputEntryElement in ruleElement.Elements(dmnNs + "inputEntry"))
                {
                    rule.InputEntries.Add(new InputEntry { Text = (string?)inputEntryElement.Element(dmnNs + "text") ?? string.Empty });
                }

                foreach (XElement outputEntryElement in ruleElement.Elements(dmnNs + "outputEntry"))
                {
                    rule.OutputEntries.Add(new OutputEntry { Text = (string?)outputEntryElement.Element(dmnNs + "text") ?? string.Empty });
                }
                decisionTable.Rules.Add(rule);
            }

            return decisionTable;
        }
        
        private static LiteralExpression? ParseLiteralExpression(XElement element, XNamespace dmnNs)
        {
            string? text = (string?)element.Element(dmnNs + "text");
            string? typeRef = (string?)element.Attribute("typeRef");

            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(typeRef))
            {
                return null;
            }

            return new LiteralExpression
            {
                Text = text ?? string.Empty,
                TypeRef = typeRef ?? string.Empty
            };
        }
    }
}