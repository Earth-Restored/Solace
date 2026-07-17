using System.Text;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Solace.ApiServer.Utils;
using Solace.DB;

namespace Solace.AuthServer.Features.Live.Login;

[Handler]
[MapPost("ppsecure/reauthenticate")]
[MapGroup<LiveLoginGroup>]
public sealed partial class Reauthenticate(
    EarthDbContext earthDb,
    CryptoSecrets cryptoSecrets,
    IOptions<AuthSettings> authSettings,
    ILogger<Reauthenticate> logger)
{
    public sealed record Command
    {
        [FromForm(Name = "userToken")]
        public required string UserToken { get; init; }
        
        [FromForm(Name = "password")]
        public required string Password { get; init; }
    }

    private async ValueTask<Results<Ok<LoginResponse>, NotFound<string>, BadRequest<string>, ForbidHttpResult>> HandleAsync(
        [AsParameters] Command command,
        CancellationToken cancellationToken)
    {
       
        if (string.IsNullOrEmpty(command.UserToken) || string.IsNullOrEmpty(command.Password))
        {
            return TypedResults.BadRequest("Invalid user or password");
        }

        var existingToken = JwtUtils.Verify<UserToken>(command.UserToken, cryptoSecrets.LoginUserTokenSecret, logger, allowExpired: true);
        if (existingToken is null)
        {
            return TypedResults.Forbid();
        }

        byte[] passwordBytes = Encoding.UTF8.GetBytes(command.Password);
        byte[] saltBytes = Convert.FromBase64String(existingToken.Data.PasswordSalt);

        byte[] passwordCheckHash = Org.BouncyCastle.Crypto.Generators.SCrypt.Generate(passwordBytes, saltBytes, 16384, 8, 1, 64);

        string passwordCheckHashBase64 = Convert.ToBase64String(passwordCheckHash);
        if (passwordCheckHashBase64 != existingToken.Data.PasswordHash)
        {
            return TypedResults.BadRequest("Invalid user or password");
        }

        var account = await earthDb.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.Id == existingToken.Data.UserId, cancellationToken);

        if (account is null)
        {
            return TypedResults.BadRequest("Invalid user or password");
        }

        return TypedResults.Ok(LoginUtils.CreateLoginResponse(account, cryptoSecrets, authSettings.Value));
    }
}
