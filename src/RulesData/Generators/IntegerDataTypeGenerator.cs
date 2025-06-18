//Copyright Warren Harding 2025.
using System;

namespace RulesData.Generators
{
    public class IntegerDataTypeGenerator : IDataTypeGenerator
    {
        private readonly Random _random;

        public IntegerDataTypeGenerator(Random random)
        {
            _random = random;
        }

        public object Generate()
        {
            return _random.Next(-10000, 10000); // Random integer
        }
    }
}