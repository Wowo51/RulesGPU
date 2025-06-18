//Copyright Warren Harding 2025.
using System;
using System.Collections.Generic;
using System.Linq;

namespace RulesGPU
{
    /// <summary>
    /// Encodes and decodes string values to and from integer IDs.
    /// This is used to convert categorical string data into a numerical format suitable for TorchSharp tensors.
    /// </summary>
    public sealed class StringValueEncoder
    {
        private readonly Dictionary<string, int> _stringToIntMap;
        private readonly Dictionary<int, string> _intToStringMap;
        private int _nextId;

        public StringValueEncoder()
        {
            _stringToIntMap = new Dictionary<string, int>();
            _intToStringMap = new Dictionary<int, string>();
            _nextId = 0;
        }

        /// <summary>
        /// Adds a string value to the encoder's vocabulary and returns its corresponding integer ID.
        /// If the string already exists, its existing ID is returned.
        /// </summary>
        /// <param name="value">The string value to encode.</param>
        /// <returns>The integer ID of the string value.</returns>
        public int AddAndEncode(string value)
        {
            if (_stringToIntMap.TryGetValue(value, out int id))
            {
                return id;
            }

            int newId = _nextId++;
            _stringToIntMap[value] = newId;
            _intToStringMap[newId] = value;
            return newId;
        }

        /// <summary>
        /// Encodes a string value to its corresponding integer ID.
        /// Returns -1 if the string is not found in the vocabulary.
        /// </summary>
        /// <param name="value">The string value to encode.</param>
        /// <returns>The integer ID of the string value, or -1 if not found.</returns>
        public int Encode(string value)
        {
            if (_stringToIntMap.TryGetValue(value, out int id))
            {
                return id;
            }
            return -1; // Return -1 if string not found, instead of throwing.
        }

        /// <summary>
        /// Decodes an integer ID back to its original string value.
        /// Returns string.Empty if the ID is not found in the vocabulary.
        /// </summary>
        /// <param name="id">The integer ID to decode.</param>
        /// <returns>The decoded string value, or string.Empty if not found.</returns>
        public string Decode(int id)
        {
            if (_intToStringMap.TryGetValue(id, out string? value))
            {
                return value; 
            }
            return string.Empty; // Return string.Empty if ID not found, instead of throwing.
        }
    }
}