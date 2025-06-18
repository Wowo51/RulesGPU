//Copyright Warren Harding 2025.
using System;

namespace RulesData.Generators
{
    public class DateTimeDataTypeGenerator : IDataTypeGenerator
    {
        private readonly Random _random;

        public DateTimeDataTypeGenerator(Random random)
        {
            _random = random;
        }

        public object Generate()
        {
            DateTime start = new DateTime(1990, 1, 1);
            int range = (DateTime.Today - start).Days;
            return start.AddDays(_random.Next(range));
        }
    }
}