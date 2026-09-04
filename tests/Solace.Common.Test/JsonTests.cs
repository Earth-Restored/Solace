namespace Solace.Common.Test;

public sealed class JsonTests
{
    private sealed record TestModel(string Name, int Age);

    [Test]
    public async Task SerializeAndDeserialize_RoundTripsObject()
    {
        var model = new TestModel("Alice", 30);

        var json = Json.Serialize(model);
        var deserialized = Json.Deserialize<TestModel>(json);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Name).IsEqualTo("Alice");
        await Assert.That(deserialized.Age).IsEqualTo(30);
    }

    [Test]
    public async Task SerializeIndented_FormatsWithNewlines()
    {
        var model = new TestModel("Bob", 25);

        var json = Json.SerializeIndented(model);

        await Assert.That(json).Contains("\n");
    }
}
