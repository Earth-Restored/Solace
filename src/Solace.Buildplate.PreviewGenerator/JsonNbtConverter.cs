using System.Text.Json.Serialization;
using Solace.Buildplate.PreviewGenerator.NBT;
using Solace.Common.Exceptions;

namespace Solace.Buildplate.PreviewGenerator;

public sealed class JsonNbtConverter
{
    public static JsonNbtTag Convert(NbtMap tag)
    {
        Dictionary<string, JsonNbtTag> value = [with(StringComparer.Ordinal)];

        foreach (var entry in tag.EntrySet())
        {
            value[entry.Key] = Convert(entry.Value);
        }

        return new CompoundJsonNbtTag(value);
    }

    public static JsonNbtTag Convert(NbtList tag)
        => new ListJsonNbtTag([.. tag.Select(Convert)]);

    private static JsonNbtTag Convert(object? tag)
    {
        if (tag is NbtMap map)
        {
            return Convert(map);
        }
        else if (tag is NbtList list)
        {
            return Convert(list);
        }
        else if (tag is int i)
        {
            return new IntJsonNbtTag(i);
        }
        else if (tag is byte b)
        {
            return new ByteJsonNbtTag(b);
        }
        else if (tag is float f)
        {
            return new FloatJsonNbtTag(f);
        }
        else if (tag is string s)
        {
            return new StringJsonNbtTag(s);
        }
        else
        {
            throw new UnsupportedOperationException($"Cannot convert tag of type {tag?.GetType()?.Name ?? "[null]"}");
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<JsonNbtTagType>))]
    public enum JsonNbtTagType
    {
#pragma warning disable CA1720 // Identifier contains type name
        [JsonStringEnumMemberName("compound")] Compound,
        [JsonStringEnumMemberName("list")] List,
        [JsonStringEnumMemberName("int")] Int,
        [JsonStringEnumMemberName("byte")] Byte,
        [JsonStringEnumMemberName("float")] Float,
        [JsonStringEnumMemberName("string")] String,
#pragma warning restore CA1720 // Identifier contains type name
    }

    public abstract class JsonNbtTag
    {

        public readonly JsonNbtTagType Type;
        public readonly object Value;

        protected JsonNbtTag(JsonNbtTagType type, object value)
        {
            Type = type;
            Value = value;
        }
    }

    public sealed class CompoundJsonNbtTag : JsonNbtTag
    {
        public CompoundJsonNbtTag(Dictionary<string, JsonNbtTag> value)
            : base(JsonNbtTagType.Compound, value)
        {
        }
    }

    public sealed class ListJsonNbtTag : JsonNbtTag
    {
        public ListJsonNbtTag(JsonNbtTag[] value)
            : base(JsonNbtTagType.List, value)
        {
        }
    }

    public sealed class IntJsonNbtTag : JsonNbtTag
    {
        public IntJsonNbtTag(int value)
            : base(JsonNbtTagType.Int, value)
        {
        }
    }

    public sealed class ByteJsonNbtTag : JsonNbtTag
    {
        public ByteJsonNbtTag(byte value)
            : base(JsonNbtTagType.Byte, value)
        {
        }
    }

    public sealed class FloatJsonNbtTag : JsonNbtTag
    {
        public FloatJsonNbtTag(float value)
            : base(JsonNbtTagType.Float, value)
        {
        }
    }

    public sealed class StringJsonNbtTag : JsonNbtTag
    {
        public StringJsonNbtTag(string value)
            : base(JsonNbtTagType.String, value)
        {
        }
    }
}
