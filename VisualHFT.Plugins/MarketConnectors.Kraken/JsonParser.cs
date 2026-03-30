using CryptoExchange.Net.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json;
using Kraken.Net.Objects.Models.Socket;

namespace MarketConnectors.Kraken
{
    public class JsonParser
    {
        public static KrakenOrderUpdate Parse(string jsonString)
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.Converters.Add(new MapEnumConverterFactory());
            options.PropertyNameCaseInsensitive = true;

            try
            {
                using (JsonDocument document = JsonDocument.Parse(jsonString))
                {
                    JsonElement root = document.RootElement;
                    if (root.TryGetProperty("data", out JsonElement dataElement) && dataElement.ValueKind == JsonValueKind.Array)
                    {
                        if (dataElement.GetArrayLength() > 0)
                        {
                            JsonElement firstDataElement = dataElement[0];
                            return System.Text.Json.JsonSerializer.Deserialize<KrakenOrderUpdate>(firstDataElement.GetRawText(), options);
                        }
                    }
                    // If "data" array not found or empty, try to deserialize the whole JSON as KrakenOrderUpdate directly.
                    // This is in case the input JSON is just the inner object and not the full channel message.
                    return System.Text.Json.JsonSerializer.Deserialize<KrakenOrderUpdate>(jsonString, options);
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                // Handle deserialization errors, maybe log or throw a custom exception
                Console.WriteLine($"Error deserializing JSON: {ex.Message}");
                return null; // Or throw exception, or return default value as needed
            }
        }
    }
    public class MapEnumConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsEnum;
        }

        public override System.Text.Json.Serialization.JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var converter = (System.Text.Json.Serialization.JsonConverter)Activator.CreateInstance(
                typeof(MapEnumConverter<>).MakeGenericType(typeToConvert));

            return converter;
        }
    }
    public class MapEnumConverter<T> : System.Text.Json.Serialization.JsonConverter<T> where T : struct, Enum
    {
        private readonly Dictionary<string, T> _stringToEnum = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<T, string> _enumToString = new Dictionary<T, string>();

        public MapEnumConverter()
        {
            var enumType = typeof(T);
            foreach (var enumValue in Enum.GetValues(enumType).Cast<T>())
            {
                var enumMember = enumType.GetMember(enumValue.ToString()).FirstOrDefault();
                if (enumMember != null)
                {
                    var mapAttribute = enumMember.GetCustomAttribute<MapAttribute>();
                    if (mapAttribute != null && mapAttribute.Values != null)
                    {
                        foreach (var mapValue in mapAttribute.Values)
                        {
                            _stringToEnum[mapValue] = enumValue;
                        }
                        _enumToString[enumValue] = mapAttribute.Values.First(); // Use the first value for serialization (if needed)
                    }
                    else
                    {
                        _stringToEnum[enumValue.ToString()] = enumValue;
                        _enumToString[enumValue] = enumValue.ToString();
                    }
                }
                else
                {
                    _stringToEnum[enumValue.ToString()] = enumValue;
                    _enumToString[enumValue] = enumValue.ToString();
                }
            }
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string stringValue = reader.GetString();
                if (_stringToEnum.TryGetValue(stringValue, out T enumValue))
                {
                    return enumValue;
                }
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                // Try to parse from number if needed, or handle default number to enum conversion if required.
                // For now, let's try to convert number to string and then parse.
                string stringValue = reader.GetInt32().ToString(); // Or GetDouble(), etc., based on expected number type
                if (_stringToEnum.TryGetValue(stringValue, out T enumValue))
                {
                    return enumValue;
                }
            }
            throw new System.Text.Json.JsonException($"Unable to convert value \"{reader.GetString()}\" to enum {typeof(T).Name}.");
        }

        public override void Write(Utf8JsonWriter writer, T enumValue, JsonSerializerOptions options)
        {
            writer.WriteStringValue(_enumToString[enumValue]);
        }
    }
}
