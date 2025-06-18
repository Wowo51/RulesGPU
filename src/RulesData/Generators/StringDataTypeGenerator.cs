//Copyright Warren Harding 2025.
using System;

namespace RulesData.Generators
{
    public class StringDataTypeGenerator : IDataTypeGenerator
    {
        private readonly Random _random;
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        public StringDataTypeGenerator(Random random)
        {
            _random = random;
        }

        public object Generate()
        {
            int length = _random.Next(5, 15); // Random string length between 5 and 14
            char[] stringChars = new char[length];
            for (int i = 0; i < length; i++)
            {
                stringChars[i] = Chars[_random.Next(Chars.Length)];
            }
            return new string(stringChars);
        }
    }
}