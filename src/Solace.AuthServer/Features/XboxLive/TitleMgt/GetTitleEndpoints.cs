using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Nager.PublicSuffix;
using Nager.PublicSuffix.RuleProviders;

namespace Solace.AuthServer.Features.XboxLive.TitleMgt;

[Handler]
[MapGet("title.mgt.xboxlive.com/titles/{Title}/endpoints")]
public sealed partial class GetTitleEndpoints(
    IHttpContextAccessor httpContextAccessor,
    ILogger<GetTitleEndpoints> logger
)
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static DomainParser? _domainParser;

    public sealed record Query
    {
        [FromRoute]
        public required string Title { get; init; }
    }

    public sealed record Response(IEnumerable<Endpoint> EndPoints);

    public sealed record Endpoint(string Protocol, string Host, int? Port, string HostType, string? RelyingParty, string? TokenType);

    private async ValueTask<Results<ContentHttpResult, BadRequest>> HandleAsync(
        Query query,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        Debug.Assert(httpContext is not null);

        IEnumerable<Endpoint> endpoints;

        switch (query.Title)
        {
            case "default":
                {
                    var singleDomainMode = !httpContext.Request.Path.StartsWithSegments(new PathString("/titles"), StringComparison.OrdinalIgnoreCase);

                    var host = httpContext.Request.Host;
                    Debug.Assert(host.HasValue);

                    var isHostIp = IPAddress.TryParse(host.Host, out _);

                    var protocol = httpContext.Request.IsHttps ? "https" : "http";

                    var hostString = !isHostIp && !singleDomainMode
                        ? (await GetDomainParserAsync(cancellationToken)).Parse(host.Host)?.RegistrableDomain ?? host.Host
                        : host.Host;

                    var port = host.Port ?? (httpContext.Request.IsHttps ? 443 : 80);

                    if (!singleDomainMode && isHostIp)
                    {
                        LogIPWithMultiDomain();
                        return TypedResults.BadRequest();
                    }

                    endpoints =
                    [
                        singleDomainMode
                            ? new Endpoint(
                                protocol,
                                hostString,
                                port,
                                isHostIp ? "ip" : "fqdn",
                                "http://xboxlive.com",
                                "JWT"
                            )
                            : new Endpoint(
                                protocol,
                                $"*.{hostString}",
                                port,
                                "wildcard",
                                "http://xboxlive.com",
                                "JWT"
                            ),
                        new Endpoint(
                            "https",
                            "xboxlive.com",
                            null,
                            "fqdn",
                            "http://xboxlive.com",
                            "JWT"
                        ),
                    ];
                }

                break;
            case "2037747551":
                {
                    endpoints =
                    [
                        new Endpoint(
                            "https",
                            "*.playfabapi.com",
                            null,
                            "wildcard",
                            "https://b980a380.minecraft.playfabapi.com/",
                            "JWT"
                        ),
                        new Endpoint(
                            "https",
                            "*.commerce.gameservices.com",
                            null,
                            "wildcard",
                            "https://minecraft.commerce.microsoftstudios.com/",
                            "JWT"
                        ),
                        new Endpoint(
                            "http",
                            "*",
                            null,
                            "wildcard",
                            null,
                            null
                        ),
                        new Endpoint(
                            "https",
                            "*",
                            null,
                            "wildcard",
                            null,
                            null
                        ),
                    ];
                }

                break;
            default:
                return TypedResults.BadRequest();
        }

        return TypedResults.Content(JsonSerializer.Serialize(new Response(endpoints), jsonOptions), "application/json");
    }

    private static async Task<DomainParser> GetDomainParserAsync(CancellationToken cancellationToken)
    {
        if (_domainParser is not null)
        {
            return _domainParser;
        }

        var ruleProvider = new LocalFileRuleProvider("public_suffix_list.dat");
        await ruleProvider.BuildAsync(cancellationToken: cancellationToken);

        return _domainParser = new DomainParser(ruleProvider);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Title request from a client connecting using IP, but not using singleDomainMode")]
    private partial void LogIPWithMultiDomain();
}