namespace Solace.Common.Utils;

public static class StreamExtensions
{
    extension(Stream stream)
    {
        public ValueTask<T?> AsJsonAsync<T>(CancellationToken cancellationToken)
            => Json.DeserializeAsync<T>(stream, cancellationToken);

        public async Task<string> ReadAsString(CancellationToken cancellationToken = default)
        {
            using (var reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync(cancellationToken);
            }
        }
    }
}
