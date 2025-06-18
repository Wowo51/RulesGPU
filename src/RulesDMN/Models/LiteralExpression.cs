//Copyright Warren Harding 2025.
namespace RulesDMN.Models
{
    public class LiteralExpression : IExpression
    {
        public string Text { get; set; } = string.Empty;
        public string TypeRef { get; set; } = string.Empty;
    }
}