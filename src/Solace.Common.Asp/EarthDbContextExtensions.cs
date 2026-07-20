using System.Buffers;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Asp.Auth;
using Solace.DB;
using Solace.DB.Models.Global;

namespace Solace.Common.Asp;

public static class EarthDbContextExtensions
{
    extension(EarthDbContext earthDb)
    {
        public async Task<CryptoSecrets> GetOrInitializeSecretsAsync()
        {
            var currentSecrets = await earthDb.Secrets
                .AsNoTracking()
                .ToDictionaryAsync(s => s.Id, s => s.Value);

            if (CryptoSecrets.AllNames.All(currentSecrets.ContainsKey))
            {
                return new CryptoSecrets(currentSecrets);
            }

            using var rng = RandomNumberGenerator.Create();

            Secret[] potentialSecrets =
            [
                new() { Id = CryptoSecrets.LoginUserTokenName, Value = GenerateSecureSecret(64) },
                new() { Id = CryptoSecrets.LoginDeviceTokenName, Value = GenerateSecureSecret(64) },
                new() { Id = CryptoSecrets.LoginXboxTokenName, Value = GenerateSecureSecret(64) },
                new() { Id = CryptoSecrets.LoginUserTokenSessionKeyName, Value = GenerateSecureSecret(24) },

                new() { Id = CryptoSecrets.LiveAuthTokenName, Value = GenerateSecureSecret(64) },
                new() { Id = CryptoSecrets.LiveXapiTokenName, Value = GenerateSecureSecret(64) },
                new() { Id = CryptoSecrets.LivePlayfabTokenName, Value = GenerateSecureSecret(64) },

                new() { Id = CryptoSecrets.PlayfabEntityTokenName, Value = GenerateSecureSecret(64) },
                new() { Id = CryptoSecrets.PlayfabSessionTicketName, Value = GenerateSecureSecret(64) },
            ];

            var insertSql = """
                INSERT INTO "Secrets" ("Id", "Value") VALUES ({0}, {1}) ON CONFLICT ("Id") DO NOTHING;
                """;

            try
            {
                foreach (var secret in potentialSecrets)
                {
                    await earthDb.Database.ExecuteSqlRawAsync(insertSql, secret.Id, secret.Value);
                }

                earthDb.ChangeTracker.Clear();
            }
            catch (DbUpdateException)
            {
                earthDb.ChangeTracker.Clear();
            }

            return new CryptoSecrets(await earthDb.Secrets
                .AsNoTracking()
                .ToDictionaryAsync(s => s.Id, s => s.Value));

            string GenerateSecureSecret(int decodedLength)
            {
                var bytesArray = ArrayPool<byte>.Shared.Rent(decodedLength);

                try
                {
                    var bytes = bytesArray.AsSpan(0, decodedLength);

                    rng.GetBytes(bytes);

                    return Convert.ToBase64String(bytes);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(bytesArray, clearArray: true);
                }
            }
        }
    }
}