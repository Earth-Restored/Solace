using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Asp.Auth;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Global;

namespace Solace.Common.Asp.Tests;

public sealed class EarthDbContextExtensionsTests
{
    private static EarthDbContext CreateInMemoryDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<EarthDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new EarthDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Test]
    public async Task GetOrInitializeSecretsAsync_EmptyDatabase_InitializesAndReturnsSecrets()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var db = CreateInMemoryDbContext(connection);

        var secrets = await db.GetOrInitializeSecretsAsync();

        await Assert.That(secrets).IsNotNull();
        await Assert.That(secrets.LoginUserTokenSecret.IsEmpty).IsFalse();
        await Assert.That(secrets.PlayfabSessionTicketSecret.IsEmpty).IsFalse();

        var dbSecretCount = await db.Secrets.CountAsync();
        await Assert.That(dbSecretCount).IsEqualTo(CryptoSecrets.AllNames.Length);
    }

    [Test]
    public async Task GetOrInitializeSecretsAsync_PreExistingSecrets_LoadsExistingSecrets()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var db = CreateInMemoryDbContext(connection);

        var dummyBase64 = Convert.ToBase64String([10, 20, 30, 40,]);
        foreach (var name in CryptoSecrets.AllNames)
        {
            db.Secrets.Add(new Secret { Id = name, Value = dummyBase64 });
        }

        await db.SaveChangesAsync();

        var secrets = await db.GetOrInitializeSecretsAsync();

        await Assert.That(secrets).IsNotNull();
        await Assert.That(secrets.LoginUserTokenSecret.SequenceEqual(new byte[] { 10, 20, 30, 40, })).IsTrue();
    }
}
