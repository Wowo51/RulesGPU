//Copyright Warren Harding 2025.
using System;

namespace RulesData.Generators
{
    public class DoubleDataTypeGenerator : IDataTypeGenerator
    {
        private readonly Random _random;

        public DoubleDataTypeGenerator(Random random)
        {
            _random = random;
        }

        public object Generate()
        {
            // Generate a random double between -1000.00 and 1000.00 to align with TorchSharp's float64
            double mantissa = (_random.NextDouble() * 2.0) - 1.0; // -1.0 to 1.0
            double value = mantissa * 1000.0;
            // Round to 8 decimal places for better precision, typical for double comparisons.
            double roundedValue = System.Math.Round(value, 8);
            return roundedValue;
        }
    }
}