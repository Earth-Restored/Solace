using System.Text;
using Solace.Common.Utils;

namespace Solace.Common.Test;

public sealed class StreamExtensionsTests
{
    [Test]
    public async Task ReadAsString_ReadsFullStreamContent()
    {
        var text = "Hello, Solace!";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var result = await stream.ReadAsString();

        await Assert.That(result).IsEqualTo(text);
    }
}
