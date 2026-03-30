using Newtonsoft.Json;
using System;
using System.Linq;
using System.Reflection;
using Bitfinex.Net.Enums;
using CryptoExchange.Net.Attributes;

namespace MarketConnectors.Bitfinex.Model
{

    public class BitfinexOrderTypeNewtonsoftConverter : JsonConverter<OrderType>
    {
        public override OrderType ReadJson(JsonReader reader, Type objectType, OrderType existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                //throw new JsonSerializationException("Cannot convert null value to Bitfinex.Net.Enums.OrderType.");
                return default(OrderType);
            }

            if (reader.TokenType == JsonToken.String)
            {
                string? enumString = reader.Value?.ToString();
                if (string.IsNullOrWhiteSpace(enumString))
                {
                    throw new JsonSerializationException(
                        "Cannot convert empty string to Bitfinex.Net.Enums.OrderType.");
                }

                foreach (OrderType enumValue in Enum.GetValues(typeof(OrderType)))
                {
                    MemberInfo memberInfo = typeof(OrderType).GetMember(enumValue.ToString()).FirstOrDefault();
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
                            // Fallback if a MapAttribute is missing for some reason, try direct name match
                            if (enumValue.ToString().Equals(enumString, StringComparison.OrdinalIgnoreCase))
                            {
                                return enumValue;
                            }
                        }
                    }
                }

                throw new JsonSerializationException(
                    $"Error converting value '{enumString}' to type 'Bitfinex.Net.Enums.OrderType'. Value not found in MapAttributes.");
            }

            throw new JsonSerializationException(
                $"Unexpected token {reader.TokenType} when parsing enum Bitfinex.Net.Enums.OrderType.");
        }

        public override void WriteJson(JsonWriter writer, OrderType value, JsonSerializer serializer)
        {
            MemberInfo memberInfo = typeof(OrderType).GetMember(value.ToString()).FirstOrDefault();
            if (memberInfo != null)
            {
                MapAttribute mapAttribute = memberInfo.GetCustomAttribute<MapAttribute>();
                if (mapAttribute != null && mapAttribute.Values.Length > 0)
                {
                    writer.WriteValue(mapAttribute.Values[0]); // Use the first mapping
                    return;
                }
            }

            // Fallback: write the standard enum string value (or throw)
            writer.WriteValue(value.ToString());
        }
    }
}