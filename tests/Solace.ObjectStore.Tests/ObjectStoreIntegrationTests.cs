using Microsoft.Extensions.Logging.Abstractions;
using Solace.ObjectStore.Client;

namespace Solace.ObjectStore.Tests;

public sealed class ObjectStoreIntegrationTests
{
    [Test]
    public async Task Store_SmallBytes_ReturnsId_AndCanRetrieve()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        byte[] data = [1, 2, 3, 4, 5,];
        var id = await client.StoreAsync(data.AsMemory());

        await Assert.That(id).IsNotNull();

        var retrieved = await client.GetArrayAsync(id!.Value);
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved).IsEquivalentTo(data);
    }

    [Test]
    public async Task Store_LargeStream_ReturnsId_AndCanRetrieve()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        var data = new byte[200_000];
        new Random(7).NextBytes(data);

        var id = await client.StoreAsync(new MemoryStream(data));

        await Assert.That(id).IsNotNull();

        var retrieved = await client.GetArrayAsync(id!.Value);
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved).IsEquivalentTo(data);
    }

    [Test]
    public async Task Store_EmptyBytes_ReturnsId_AndRetrievesEmpty()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        var id = await client.StoreAsync(ReadOnlyMemory<byte>.Empty);

        await Assert.That(id).IsNotNull();

        var retrieved = await client.GetArrayAsync(id!.Value);
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved.Length).IsEqualTo(0);
    }

    [Test]
    public async Task GetMemoryAsync_ExistingObject_ReturnsCorrectData()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        byte[] data = [10, 20, 30,];
        var id = await client.StoreAsync(data.AsMemory());

        var result = await client.GetMemoryAsync(id!.Value);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value.ToArray()).IsEquivalentTo(data);
    }

    [Test]
    public async Task GetMemoryAsync_UnknownId_ReturnsNull()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        var result = await client.GetMemoryAsync(Guid.NewGuid());

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetStreamAsync_ExistingObject_StreamsCorrectData()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        var data = new byte[150_000];
        new Random(99).NextBytes(data);
        var id = await client.StoreAsync(new MemoryStream(data));

        var stream = await client.GetStreamAsync(id!.Value);
        await Assert.That(stream).IsNotNull();

        using var ms = new MemoryStream();
        await stream!.CopyToAsync(ms);
        await Assert.That(ms.ToArray()).IsEquivalentTo(data);
    }

    [Test]
    public async Task GetStreamAsync_UnknownId_ReturnsNull()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        var stream = await client.GetStreamAsync(Guid.NewGuid());

        await Assert.That(stream).IsNull();
    }

    [Test]
    public async Task GetArrayAsync_UnknownId_ReturnsNull()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        var result = await client.GetArrayAsync(Guid.NewGuid());

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task UpdateAsync_Bytes_ReplacesExistingObject()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        byte[] original = [1, 2, 3,];
        var id = await client.StoreAsync(original.AsMemory());

        byte[] updated = [9, 8, 7, 6,];
        var updatedId = await client.UpdateAsync(id!.Value, updated.AsMemory());

        await Assert.That(updatedId).IsNotNull();
        await Assert.That(updatedId.Value).IsEqualTo(id.Value);

        var retrieved = await client.GetArrayAsync(id.Value);
        await Assert.That(retrieved).IsEquivalentTo(updated);
    }

    [Test]
    public async Task UpdateAsync_Stream_ReplacesExistingObject()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        var original = new byte[50_000];
        new Random(1).NextBytes(original);
        var id = await client.StoreAsync(new MemoryStream(original));

        var updated = new byte[75_000];
        new Random(2).NextBytes(updated);
        var updatedId = await client.UpdateAsync(id!.Value, new MemoryStream(updated));

        await Assert.That(updatedId).IsNotNull();
        await Assert.That(updatedId.Value).IsEqualTo(id.Value);

        var retrieved = await client.GetArrayAsync(id.Value);
        await Assert.That(retrieved).IsEquivalentTo(updated);
    }

    [Test]
    public async Task DeleteAsync_ExistingObject_ReturnsTrueAndObjectIsGone()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        var id = await client.StoreAsync(new byte[] { 42, }.AsMemory());
        var deleted = await client.DeleteAsync(id!.Value);

        await Assert.That(deleted).IsTrue();

        var retrieved = await client.GetArrayAsync(id.Value);
        await Assert.That(retrieved).IsNull();
    }

    [Test]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        var deleted = await client.DeleteAsync(Guid.NewGuid());

        await Assert.That(deleted).IsFalse();
    }

    [Test]
    public async Task GetTotalSizeAsync_EmptyStore_ReturnsZero()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        var size = await client.GetTotalSizeAsync();

        await Assert.That(size).IsEqualTo(0L);
    }

    [Test]
    public async Task GetTotalSizeAsync_AfterStoring_ReflectsStoredBytes()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        var data1 = new byte[1000];
        var data2 = new byte[2000];
        await client.StoreAsync(data1.AsMemory());
        await client.StoreAsync(data2.AsMemory());

        var size = await client.GetTotalSizeAsync();

        await Assert.That(size).IsEqualTo(3000L);
    }

    [Test]
    public async Task GetTotalSizeAsync_AfterDelete_DecreasesSize()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        var data = new byte[500];
        var id = await client.StoreAsync(data.AsMemory());

        await client.DeleteAsync(id!.Value);

        var size = await client.GetTotalSizeAsync();

        await Assert.That(size).IsEqualTo(0L);
    }

    [Test]
    public async Task DeleteAllAsync_ClearsAllObjects()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        await client.StoreAsync(new byte[100].AsMemory());
        await client.StoreAsync(new byte[200].AsMemory());

#pragma warning disable CS0618
        await client.DeleteAllAsync();
#pragma warning restore CS0618

        var size = await client.GetTotalSizeAsync();
        await Assert.That(size).IsEqualTo(0L);
    }

    [Test]
    public async Task MultipleObjects_StoredAndRetrievedIndependently()
    {
        await using var server = await TestObjectStoreServer.StartAsync();
        await using var client = await ObjectStoreClient.ConnectAsync(server.Address, NullLogger.Instance);

        byte[] a = [1, 1, 1,];
        byte[] b = [2, 2, 2,];
        byte[] c = [3, 3, 3,];

        var idA = await client.StoreAsync(a.AsMemory());
        var idB = await client.StoreAsync(b.AsMemory());
        var idC = await client.StoreAsync(c.AsMemory());

        await Assert.That(await client.GetArrayAsync(idA!.Value)).IsEquivalentTo(a);
        await Assert.That(await client.GetArrayAsync(idB!.Value)).IsEquivalentTo(b);
        await Assert.That(await client.GetArrayAsync(idC!.Value)).IsEquivalentTo(c);
    }
}
