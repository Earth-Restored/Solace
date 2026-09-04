using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Solace.Common;

public static class Json
{
    internal const string SerializationUnreferencedCodeMessage = "JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.";
    internal const string SerializationRequiresDynamicCodeMessage = "JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications.";

    private static readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private static readonly JsonSerializerOptions deseralizeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
    private static readonly JsonSerializerOptions optionsIndented = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
    [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
    public static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, options);

    [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
    [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
    public static string SerializeIndented<T>(T value)
        => JsonSerializer.Serialize(value, optionsIndented);

    [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
    [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
    public static void SerializeIndented<T>(Stream utf8Stream, T value)
        => JsonSerializer.Serialize(utf8Stream, value, optionsIndented);

    [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
    [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
    public static string Serialize<T>(T value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(value, options);

    [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
    [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
    public static T? Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, deseralizeOptions);

    [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
    [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
    public static T? Deserialize<T>(string json, JsonSerializerOptions options)
        => JsonSerializer.Deserialize<T>(json, options);

    [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
    [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
    public static T? Deserialize<T>(Stream utf8Json)
        => JsonSerializer.Deserialize<T>(utf8Json, deseralizeOptions);

    [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
    [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
    public static T? Deserialize<T>(Stream utf8Json, JsonTypeInfo<T> info)
        => JsonSerializer.Deserialize<T>(utf8Json, info);

    [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
    [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
    public static ValueTask<T?> DeserializeAsync<T>(Stream utf8Stream, CancellationToken cancellationToken)
        => JsonSerializer.DeserializeAsync<T>(utf8Stream, deseralizeOptions, cancellationToken);
}
