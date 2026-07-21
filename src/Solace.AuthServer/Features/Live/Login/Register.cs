using System.Diagnostics;
using System.Security.Cryptography;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Solace.Common.Asp.Auth;
using Solace.Common.Asp.Captcha;
using Solace.DB.Earth;
using Solace.DB.Earth.Utils;
using static Solace.Common.Constants.AccountConstants;

namespace Solace.AuthServer.Features.Live.Login;

[Handler]
[MapPost("ppsecure/register")]
[MapGroup<LiveLoginGroup>]
public sealed partial class Register(
    EarthDbContext earthDb,
    CryptoSecrets cryptoSecrets,
    IOptions<AuthSettings> authSettings,
    ICaptchaValidator captchaValidator,
    IHttpContextAccessor httpContextAccessor,
    ILogger<Register> logger)
{
    public sealed record Command
    {
        [FromForm(Name = "username")]
        public required string Username { get; init; }

        [FromForm(Name = "password")]
        public required string Password { get; init; }

        [FromForm(Name = "firstName")]
        public string? FirstName { get; init; }

        [FromForm(Name = "lastName")]
        public string? LastName { get; init; }

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
        var firstName = command.FirstName?.Trim();
        var lastName = command.LastName?.Trim();

        if (firstName is { Length: 0 })
        {
            firstName = null;
        }

        if (lastName is { Length: 0 })
        {
            lastName = null;
        }

        LogRegisterAttempt(username, firstName, lastName);

        if (string.IsNullOrWhiteSpace(username) || username.Length < UsernameLengthMin || username.Length > UsernameLengthMax)
        {
            return TypedResults.BadRequest($"Username must be {UsernameLengthMin}-{UsernameLengthMax} characters long");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < PasswordLengthMin || password.Length > PasswordLengthMax)
        {
            return TypedResults.BadRequest($"Password must be {PasswordLengthMin}-{PasswordLengthMax} characters long");
        }

        if (!string.IsNullOrWhiteSpace(firstName) && (firstName.Length < NameLengthMin || firstName.Length > NameLengthMax))
        {
            return TypedResults.BadRequest($"First name must be {NameLengthMin}-{NameLengthMax} characters long");
        }

        if (!string.IsNullOrWhiteSpace(lastName) && (lastName.Length < NameLengthMin || lastName.Length > NameLengthMax))
        {
            return TypedResults.BadRequest($"Last name must be {NameLengthMin}-{NameLengthMax} characters long");
        }

        if (!GetUsernameRegex().IsMatch(username))
        {
            return TypedResults.BadRequest($"Username must contain only: {UsernameAllowedCharacters}"); // keep in sync with GetUsernameRegex
        }

        if (await earthDb.Accounts
            .AnyAsync(account => account.Username == username, cancellationToken))
        {
            return TypedResults.BadRequest("Account with the specified username already exists");
        }

        var accountId = Guid.CreateVersion7();

        var passwordSalt = new byte[16];
        RandomNumberGenerator.Fill(passwordSalt);

        var paswordHash = HashPassword(password, passwordSalt);

        var account = await earthDb.GetOrCreateAccount(accountId);

        account.Id = accountId;
        account.CreatedDate = DateTimeOffset.UtcNow;
        account.Username = username;
        account.ProfilePictureUrl = null; // TODO
        account.FirstName = firstName;
        account.LastName = lastName;
        account.PasswordSalt = passwordSalt;
        account.PasswordHash = paswordHash;

        try
        {
            await earthDb.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation)
        {
            LogConcurrencyConflictHitForUsername(username);
            return TypedResults.BadRequest("Account with the specified username already exists");
        }

        LogAccountCreated(accountId, username);

        return TypedResults.Ok(LoginUtils.CreateLoginResponse(account, cryptoSecrets, authSettings.Value));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Register attempt - Username: {Username}, First name: {FirstName}, Last name: {LastName}")]
    private partial void LogRegisterAttempt(string Username, string? FirstName, string? LastName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Account created - Id: {AccountId}, Username: {Username}")]
    private partial void LogAccountCreated(Guid AccountId, string Username);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Concurrency conflict hit for username {Username}")]
    private partial void LogConcurrencyConflictHitForUsername(string Username);
}
