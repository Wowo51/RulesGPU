//Copyright Warren Harding 2025.
using RulesData.Generators;
using System.Collections.Generic;
using System;

namespace RulesData
{
    public static class DataTypeGeneratorFactory
    {
        private static readonly Dictionary<string, IDataTypeGenerator> _generators = new Dictionary<string, IDataTypeGenerator>();
        private static readonly Random _sharedRandom = new Random();

        static DataTypeGeneratorFactory()
        {
            // Register common FEEL types
            _generators.Add("string", new StringDataTypeGenerator(_sharedRandom));
            _generators.Add("number", new DoubleDataTypeGenerator(_sharedRandom)); // DMN 'number' implies decimal/double
            _generators.Add("integer", new IntegerDataTypeGenerator(_sharedRandom));
            _generators.Add("boolean", new BooleanDataTypeGenerator(_sharedRandom));
            _generators.Add("date", new DateTimeDataTypeGenerator(_sharedRandom));
            _generators.Add("datetime", new DateTimeDataTypeGenerator(_sharedRandom)); // Re-use for dateTime
        }

        public static IDataTypeGenerator GetGenerator(string typeRef)
        {
            if (string.IsNullOrWhiteSpace(typeRef))
            {
                return _generators["string"]; // Default to string if typeRef is empty
            }

            // Normalize typeRef to handle FEEL URIs or simple names
            string normalizedTypeRef = typeRef.ToLowerInvariant();
            if (normalizedTypeRef.Contains("#"))
            {
                int hashIndex = normalizedTypeRef.LastIndexOf('#');
                normalizedTypeRef = normalizedTypeRef.Substring(hashIndex + 1);
            }

            if (_generators.TryGetValue(normalizedTypeRef, out IDataTypeGenerator? generator))
            {
                // We know that if TryGetValue returns true, 'generator' will not be null
                // because all values in _generators are non-null instances.
                return generator!;
            }
            // Default to string generator if specific type not found
            return _generators["string"];
        }
    }
}