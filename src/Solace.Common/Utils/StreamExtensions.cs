using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Solace.Common.Utils;

public static class StreamExtensions
{
    extension(Stream stream)
    {
        public ValueTask<T?> AsJsonAsync<T>(JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
            => JsonSerializer.DeserializeAsync(stream, jsonTypeInfo, cancellationToken);

        public async Task<string> ReadAsString(CancellationToken cancellationToken = default)
        {
            using (var reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync(cancellationToken);
            }
        }
    }
}
