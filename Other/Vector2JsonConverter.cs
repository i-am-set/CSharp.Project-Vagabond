using Microsoft.Xna.Framework;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A custom JSON converter to handle the serialization and deserialization of the
    /// MonoGame Vector2 struct, which uses public fields (X, Y) instead of properties.
    /// </summary>
    public class Vector2JsonConverter : JsonConverter<Vector2>
    {
        public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject token for Vector2.");
            }

            Vector2 result = new Vector2();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return result;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString();
                    reader.Read();
                    switch (propertyName.ToUpperInvariant())
                    {
                        case "X":
                            result.X = reader.GetSingle();
                            break;
                        case "Y":
                            result.Y = reader.GetSingle();
                            break;
                    }
                }
            }
            throw new JsonException("Unexpected end of JSON when parsing Vector2.");
        }

        public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("X", value.X);
            writer.WriteNumber("Y", value.Y);
            writer.WriteEndObject();
        }
    }
}