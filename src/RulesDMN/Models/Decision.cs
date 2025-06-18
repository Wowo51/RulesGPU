//Copyright Warren Harding 2025.
namespace RulesDMN.Models
{
    public class Decision
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public IExpression? DecisionLogic { get; set; }
    }
}