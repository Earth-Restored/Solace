using System.Diagnostics;
using System.Text.Json.Serialization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.Common.Asp.Auth;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi.Objects;

[Handler]
[MapPost("Object/SetObjects")]
[MapGroup<PlayfabApiGroup>]
public sealed partial class SetObjects(
    CryptoSecrets cryptoSecrets,
    IHttpContextAccessor httpContextAccessor,
    ILogger<SetObjects> logger
)
{
    [ForcePascalCase]
    public sealed record Command(
        RequestEntity Entity,
        int? ExpectedProfileVersion,
        RequestObject[] Objects
    );

    [ForcePascalCase]
    public sealed record RequestObject(
        DataObject DataObject,
        object? DeleteObject,
        object? EscapedDataObject,
        string ObjectName
    );

    [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
    public sealed record DataObject(
        Dictionary<string, object>[] PersonaCollection,
        string Version
    );

    private async ValueTask<Results<Ok, ForbidHttpResult, BadRequest>> HandleAsync(
        Command command,
        CancellationToken cancellationToken
    )
    {
        var httpContext = httpContextAccessor.HttpContext;
        Debug.Assert(httpContext is not null);

        var tokenUnion = PlayfabApiUtils.PlayfabAuth(cryptoSecrets, httpContext.Request, logger);

        if (tokenUnion.IsB)
        {
            return tokenUnion.B.Result is ForbidHttpResult forbid ? forbid : (BadRequest)tokenUnion.B.Result;
        }

        var token = tokenUnion.A;

        if (token.Id != command.Entity.Id || token.Type != command.Entity.Type)
        {
            return TypedResults.Forbid();
        }

        return command.Entity.Type switch
        {
            "master_player_account" => TypedResults.Ok(),// TODO
            _ => TypedResults.BadRequest(),
        };
    }
}