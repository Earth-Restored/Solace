using System.Diagnostics;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Solace.Common.Asp.Auth;
using Solace.Common.Asp.Captcha;
using Solace.DB.Earth;
using static Solace.Common.Constants.AccountConstants;

namespace Solace.AuthServer.Features.Live.Login;

[Handler]
[MapPost("ppsecure/login")]
[MapGroup<LiveLoginGroup>]
public sealed partial class LoginHandler(
    EarthDbContext earthDb,
    CryptoSecrets cryptoSecrets,
    IOptions<AuthSettings> authSettings,
    ICaptchaValidator captchaValidator,
    IHttpContextAccessor httpContextAccessor,
    ILogger<LoginHandler> logger)
{
    public sealed record Command
    {
        [FromForm(Name = "username")]
        public required string Username { get; init; }

        [FromForm(Name = "password")]
        public required string Password { get; init; }

        [FromForm(Name = "captchaToken")]
        public string? CaptchaToken { get; init; }
    }

    private async ValueTask<Results<Ok<LoginResponse>, BadRequest<string>>> HandleAsync(
        [AsParameters] Command command,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        Debug.Assert(httpContext is not null);

        var remoteip = httpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ??
            httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
            httpContext.Connection.RemoteIpAddress?.ToString();

        if (!await captchaValidator.ValidateAsync(command.CaptchaToken, remoteip, cancellationToken))
        {
            return TypedResults.BadRequest("Security check failed. Please try again.");
        }

        var username = command.Username.Trim();
        var password = command.Password.Trim();

        LogLoginAttempt(username);

        var account = await earthDb.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.Username == username, cancellationToken);

        if (account is null)
        {
            return TypedResults.BadRequest("Username or password is incorrect");
        }

        var passwordHash = HashPassword(password, account.PasswordSalt);

        if (!passwordHash.AsSpan().SequenceEqual(account.PasswordHash))
        {
            return TypedResults.BadRequest("Username or password is incorrect");
        }

        return TypedResults.Ok(LoginUtils.CreateLoginResponse(account, cryptoSecrets, authSettings.Value));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Login attempt - Username: {Username}")]
    private partial void LogLoginAttempt(string Username);
}
