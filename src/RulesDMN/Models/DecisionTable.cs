//Copyright Warren Harding 2025.
using System.Collections.Generic;

namespace RulesDMN.Models
{
    public class DecisionTable : IExpression
    {
        public DecisionTable()
        {
            Inputs = new List<InputClause>();
            Outputs = new List<OutputClause>();
            Rules = new List<Rule>();
        }

        public HitPolicy HitPolicy { get; set; }
        public List<InputClause> Inputs { get; set; }
        public List<OutputClause> Outputs { get; set; }
        public List<Rule> Rules { get; set; }
    }
}