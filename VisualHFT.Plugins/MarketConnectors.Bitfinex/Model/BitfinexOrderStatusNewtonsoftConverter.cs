
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Reflection; 
using Bitfinex.Net.Enums; 
using CryptoExchange.Net.Attributes; 

namespace MarketConnectors.Bitfinex.Model
{

    public class BitfinexOrderStatusNewtonsoftConverter : JsonConverter<OrderStatus>
    {
        public override OrderStatus ReadJson(JsonReader reader, Type objectType, OrderStatus existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return default(OrderStatus);
            }

            if (reader.TokenType == JsonToken.String)
            {
                string? enumString = reader.Value?.ToString();
                if (string.IsNullOrWhiteSpace(enumString))
                {
                    // Decide how to handle empty string. Options:
                    // 1. Throw:
                    //    throw new JsonSerializationException("Cannot convert empty string to Bitfinex.Net.Enums.OrderStatus.");
                    // 2. Return default or a specific "unknown" like value:
                    return default(OrderStatus); // Or OrderStatus.Unknown if that's more appropriate
                }

                foreach (OrderStatus enumValue in Enum.GetValues(typeof(OrderStatus)))
                {
                    MemberInfo memberInfo = typeof(OrderStatus).GetMember(enumValue.ToString()).FirstOrDefault();
                    if (memberInfo != null)
                    {
                        MapAttribute mapAttribute = memberInfo.GetCustomAttribute<MapAttribute>();
                        if (mapAttribute != null)
                        {
                            // Check primary map value
                            if (mapAttribute.Values.Any(m => m.Equals(enumString, StringComparison.OrdinalIgnoreCase)))
                            {
                                return enumValue;
                            }
                        }
                        else
                        {
                            // If no MapAttribute, try direct name match (important for 'Unknown')
                            if (enumValue.ToString().Equals(enumString, StringComparison.OrdinalIgnoreCase))
                            {
                                return enumValue;
                            }
                        }
                    }
                }

                // If no mapping found, you might want to default to OrderStatus.Unknown
                // or throw an exception, depending on how strict you need to be.
                // For example, to default to Unknown for unrecognized strings:
                // return OrderStatus.Unknown;
                // Or to throw:
                throw new JsonSerializationException(
                    $"Error converting value '{enumString}' to type 'Bitfinex.Net.Enums.OrderStatus'. Value not mapped.");
            }

            throw new JsonSerializationException(
                $"Unexpected token {reader.TokenType} when parsing enum Bitfinex.Net.Enums.OrderStatus.");
        }

        public override void WriteJson(JsonWriter writer, OrderStatus value, JsonSerializer serializer)
        {
            MemberInfo memberInfo = typeof(OrderStatus).GetMember(value.ToString()).FirstOrDefault();
            if (memberInfo != null)
            {
                MapAttribute mapAttribute = memberInfo.GetCustomAttribute<MapAttribute>();
                if (mapAttribute != null && mapAttribute.Values.Length > 0)
                {
                    writer.WriteValue(mapAttribute.Values[0]); // Use the first mapping
                    return;
                }
                else
                {
                    // If no MapAttribute (like for 'Unknown'), write the enum member's name
                    writer.WriteValue(value.ToString()
                        .ToUpperInvariant()); // Bitfinex tends to use uppercase, adjust if needed
                    return;
                }
            }

            // Fallback, though unlikely to be hit if all members are covered
            writer.WriteValue(value.ToString().ToUpperInvariant());
        }
    }
}