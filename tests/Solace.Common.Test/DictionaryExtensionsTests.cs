using Solace.Common.Utils;

namespace Solace.Common.Test;

public sealed class DictionaryExtensionsTests
{
    [Test]
    public async Task AddRange_AddsOrUpdatesEntries()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1 };
        var source = new Dictionary<string, int> { ["a"] = 10, ["b"] = 2 };

        dict.AddRange(source);

        await Assert.That(dict["a"]).IsEqualTo(10);
        await Assert.That(dict["b"]).IsEqualTo(2);
    }

    [Test]
    public async Task ComputeIfAbsent_KeyExists_ReturnsExistingValueWithoutCallingMapping()
    {
        var dict = new Dictionary<string, string> { ["key"] = "existing" };
        var called = false;

        var result = dict.ComputeIfAbsent("key", k =>
        {
            called = true;
            return "new";
        });

        await Assert.That(result).IsEqualTo("existing");
        await Assert.That(called).IsFalse();
    }

    [Test]
    public async Task ComputeIfAbsent_KeyMissing_ExecutesMappingAndAddsValue()
    {
        var dict = new Dictionary<string, string>();
        var result = dict.ComputeIfAbsent("key", k => $"computed_{k}");

        await Assert.That(result).IsEqualTo("computed_key");
        await Assert.That(dict["key"]).IsEqualTo("computed_key");
    }

    [Test]
    public async Task ComputeIfAbsent_MappingReturnsNull_DoesNotAddValue()
    {
        var dict = new Dictionary<string, string?>();
        var result = dict.ComputeIfAbsent("key", k => null);

        await Assert.That(result).IsNull();
        await Assert.That(dict.ContainsKey("key")).IsFalse();
    }

    [Test]
    public async Task RemoveAll_RemovesMatchingEntries()
    {
        var dict = new Dictionary<int, string>
        {
            [1] = "one",
            [2] = "two",
            [3] = "three",
            [4] = "four"
        };

        dict.RemoveAll(kvp => kvp.Key % 2 == 0);

        await Assert.That(dict.Count).IsEqualTo(2);
        await Assert.That(dict.ContainsKey(1)).IsTrue();
        await Assert.That(dict.ContainsKey(3)).IsTrue();
        await Assert.That(dict.ContainsKey(2)).IsFalse();
        await Assert.That(dict.ContainsKey(4)).IsFalse();
    }
}
