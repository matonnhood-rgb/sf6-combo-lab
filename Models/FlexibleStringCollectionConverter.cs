using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComboLab.Models;

public sealed class FlexibleStringCollectionConverter
    : JsonConverter<ObservableCollection<string>>
{
    public override ObservableCollection<string> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var values = new ObservableCollection<string>();

        if (reader.TokenType == JsonTokenType.String)
        {
            AddDelimited(values, reader.GetString());
            return values;
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return values;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("タグは文字列または文字列配列で指定してください。");
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                AddDelimited(values, reader.GetString());
            }
        }

        return values;
    }

    public override void Write(
        Utf8JsonWriter writer,
        ObservableCollection<string> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var tag in value)
        {
            writer.WriteStringValue(tag);
        }

        writer.WriteEndArray();
    }

    private static void AddDelimited(
        ICollection<string> target,
        string? source)
    {
        foreach (var value in (source ?? string.Empty).Split(
                     new[] { ',', '、' },
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            target.Add(value);
        }
    }
}
