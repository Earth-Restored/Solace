using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Constants;
using Solace.Db.Earth;
using Solace.Db.Earth.Utils;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Profiles;

namespace Solace.WebPortal.Features.Profiles;

[Handler]
[MapPost("")]
[MapGroup<ProfilesGroup>]
[Authorize(Policy = Permissions.CreateProfile)]
public static partial class CreateProfile
{
    private static async ValueTask<Results<Ok<CreateProfileResponse>, BadRequest<string>, UnauthorizedHttpResult>> HandleAsync(
        CreateProfileCommand command,
        EarthDbContext earthDb,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken
    )
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser is null)
        {
            return TypedResults.Unauthorized();
        }

        var userId = httpUser.GetIdLong();

        var username = command.Username?.Trim();

        if (string.IsNullOrWhiteSpace(username) || username.Length < AccountConstants.UsernameLengthMin || username.Length > AccountConstants.UsernameLengthMax)
        {
            return TypedResults.BadRequest($"Username must be between {AccountConstants.UsernameLengthMin} and {AccountConstants.UsernameLengthMax} characters.");
        }

        if (!AccountConstants.GetUsernameRegex().IsMatch(username))
        {
            return TypedResults.BadRequest($"Username must only contain {AccountConstants.UsernameAllowedCharacters}.");
        }

        await using var transaction = await earthDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        if ((await earthDb.Profiles.CountAsync(profile => profile.WebPortalAccountId == userId, cancellationToken)) >= 3)
        {
            return TypedResults.BadRequest("You cannot have more than 3 profiles.");
        }

        if (await earthDb.Profiles.AnyAsync(profile => profile.Username == username, cancellationToken))
        {
            return TypedResults.BadRequest("Username is already taken. Please choose another.");
        }

        var newProfile = await earthDb.GetOrCreateAccount(Guid.CreateVersion7(), userId);
        newProfile.Username = username;

        try
        {
            await earthDb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation)
        {
            return TypedResults.BadRequest("Username is already taken or profile creation failed.");
        }

        return TypedResults.Ok(new CreateProfileResponse(newProfile.Id));
    }
}
