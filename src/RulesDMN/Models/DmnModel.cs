//Copyright Warren Harding 2025.
using System.Collections.Generic;

namespace RulesDMN.Models
{
    public class DmnModel
    {
        public DmnModel()
        {
            Decisions = new List<Decision>();
        }

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<Decision> Decisions { get; set; }
    }
}