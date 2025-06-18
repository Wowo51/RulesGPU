//Copyright Warren Harding 2025.
using System;

namespace RulesData.Generators
{
    public class BooleanDataTypeGenerator : IDataTypeGenerator
    {
        private readonly Random _random;

        public BooleanDataTypeGenerator(Random random)
        {
            _random = random;
        }

        public object Generate()
        {
            return _random.Next(2) == 0; // Random boolean (true/false)
        }
    }
}