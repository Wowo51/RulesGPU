//Copyright Warren Harding 2025.
using System.Collections.Generic;

namespace RulesDMN.Models
{
    public class Rule
    {
        public Rule()
        {
            InputEntries = new List<InputEntry>();
            OutputEntries = new List<OutputEntry>();
        }

        public string? Id { get; set; }
        public List<InputEntry> InputEntries { get; set; }
        public List<OutputEntry> OutputEntries { get; set; }
    }
}