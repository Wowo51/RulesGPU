//Copyright Warren Harding 2025.
using System;
using System.Globalization;

namespace RulesData
{
    public static class DataTypeConverter
    {
        public static object? ConvertStringToType(string valueString, string typeRef)
        {
            string normalizedTypeRef = typeRef.ToLowerInvariant();
            if (normalizedTypeRef.Contains("#"))
            {
                int hashIndex = normalizedTypeRef.LastIndexOf('#');
                normalizedTypeRef = normalizedTypeRef.Substring(hashIndex + 1);
            }

            // Remove DMN function wrappers like 'date("...")', 'date and time("...")' and quotes for string
            string cleanValueString = valueString;
            if (normalizedTypeRef == "string" && cleanValueString.StartsWith("\"") && cleanValueString.EndsWith("\"") && cleanValueString.Length > 1)
            {
                cleanValueString = cleanValueString.Substring(1, cleanValueString.Length - 2);
            }
            else if (cleanValueString.StartsWith("date(\"") && cleanValueString.EndsWith("\")") && cleanValueString.Length > "date(\"\")".Length)
            {
                 cleanValueString = cleanValueString.Substring("date(\"".Length, cleanValueString.Length - "date(\"".Length - "\")".Length);
            }
            else if (cleanValueString.StartsWith("date and time(\"") && cleanValueString.EndsWith("\")") && cleanValueString.Length > "date and time(\"\")".Length)
            {
                 cleanValueString = cleanValueString.Substring("date and time(\"".Length, cleanValueString.Length - "date and time(\"".Length - "\")".Length);
            }

            switch (normalizedTypeRef)
            {
                case "string":
                    return cleanValueString;
                case "number":
                case "integer":
                    // Changed to try parsing as double to be consistent with GPU engine's precision
                    if (double.TryParse(cleanValueString, NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleValue))
                    {
                        if (normalizedTypeRef == "integer")
                        {
                            return (int)doubleValue;
                        }
                        return doubleValue;
                    }
                    return null; // Return null on parse failure
                case "boolean":
                    if (bool.TryParse(cleanValueString, out bool boolValue))
                    {
                        return boolValue;
                    }
                    return null;
                case "date":
                    if (DateTime.TryParseExact(cleanValueString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateValue))
                    {
                        return dateValue;
                    }
                    return null;
                case "datetime":
                    // Handle various ISO 8601 formats that DateTime.TryParse can handle (e.g., with or without milliseconds/timezone)
                    if (DateTime.TryParse(cleanValueString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime datetimeValue))
                    {
                        return datetimeValue;
                    }
                    return null;
                default:
                    // For unknown types or parsing failures, return null.
                    return null; 
            }
        }

        /// <summary>
        /// Converts an object value to its string representation suitable for DMN rule entries (e.g., "date(\"YYYY-MM-DD\")", "\"text\"").
        /// </summary>
        /// <param name="value">The object value.</param>
        /// <param name="typeRef">The type reference, e.g., "string", "number", "date".</param>
        /// <returns>A string representation of the value.</returns>
        public static string ConvertValueToRuleString(object? value, string typeRef)
        {
            if (value is null)
            {
                return string.Empty; // Represent null values as empty string in rules
            }

            string normalizedTypeRef = typeRef.ToLowerInvariant();

            if (normalizedTypeRef.Contains("#"))
            {
                int hashIndex = normalizedTypeRef.LastIndexOf('#');
                normalizedTypeRef = normalizedTypeRef.Substring(hashIndex + 1);
            }

            switch (normalizedTypeRef)
            {
                case "string":
                    // DMN string literals are typically quoted.
                    return $"\"{value}\"";
                case "number":
                    // Numbers typically don't need quotes. Use double precision.
                    if (value is double doubleVal)
                    {
                        return doubleVal.ToString(CultureInfo.InvariantCulture);
                    }
                    else if (value is float floatVal)
                    {
                        return ((double)floatVal).ToString(CultureInfo.InvariantCulture);
                    }
                    else if (value is int intVal)
                    {
                        return ((double)intVal).ToString(CultureInfo.InvariantCulture);
                    }
                    return value.ToString() ?? string.Empty;
                case "integer":
                    // Integers should be represented without decimal points.
                    if (value is int intValRule)
                    {
                        return intValRule.ToString(CultureInfo.InvariantCulture);
                    }
                    else if (value is double doubleValRule) // Handle cases where integer might be stored as double
                    {
                        return ((int)doubleValRule).ToString(CultureInfo.InvariantCulture);
                    }
                    else if (value is float floatValRule)
                    {
                       return ((int)floatValRule).ToString(CultureInfo.InvariantCulture);
                    }
                    return value.ToString() ?? string.Empty;
                case "boolean":
                    return value.ToString()?.ToLowerInvariant() ?? string.Empty;
                case "date":
                    if (value is DateTime dateValue)
                    {
                        // DMN date format: date("YYYY-MM-DD")
                        return $"date(\"{dateValue:yyyy-MM-dd}\")";
                    }
                    return value.ToString() ?? string.Empty;
                case "datetime":
                    if (value is DateTime datetimeValue)
                    {
                        // DMN datetime format: date and time("YYYY-MM-DDTHH:mm:ss")
                        return $"date and time(\"{datetimeValue:yyyy-MM-ddTHH:mm:ss}\")";
                    }
                    return value.ToString() ?? string.Empty;
                default:
                    return value.ToString() ?? string.Empty;
            }
        }
    }
}