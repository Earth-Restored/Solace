using System.Text.Json.Serialization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;

namespace Solace.AuthServer.Features.XboxAB;

[Handler]
[MapGet("ab")]
[MapGroup<XboxABGroup>]
public static partial class GetAbOptions
{
    public sealed record Query;

    public sealed record Result(
        [property: JsonPropertyName("Features")] string[] Features,
        [property: JsonPropertyName("Flights")] Dictionary<string, string> Flights,
        [property: JsonPropertyName("Configs")] Config[] Configs,
        [property: JsonPropertyName("ParameterGroups")] object[]? ParameterGroups,
        [property: JsonPropertyName("FlightingVersion")] int FlightingVersion,
        [property: JsonPropertyName("ImpressionId")] string ImpressionId,
        [property: JsonPropertyName("AssignmentContext")] string AssignmentContext
    );

    public sealed record Config(
        [property: JsonPropertyName("Id")] string Id,
        [property: JsonPropertyName("Parameters")] Dictionary<string, object> Parameters
    );

    private static async ValueTask<Result> HandleAsync(
        Query _,
        CancellationToken cancellationToken)
            => new(
            [
                //"mc-sunsetting_1",
                //"mc-reco-algo2simfirst",
                //"mc-rp-hero-row-timer-2",
                //"mc-signaling-usewebsockets",
                //"mc-signaling-useturn",
                //"mcmktvlt-offerids-recos_lgbm3c",
                //"mc-cloud-file-upload",
                //"mc-oneds-prod",
                //"mc-realms-cayman",
                //"mc-realms-libhttp",
                //"mc-rp-morelicensedsidebar",
                //"control-tower-says-yes",
                //"raknet-enabled",
                //"mc-rp-nycbanner3",
                //"mc-rp-risinglava",
            ],
            [
                with(StringComparer.Ordinal),
                //["28kk"] = "mc-sunsetting_1",
                //["2mky"] = "mc-reco-algo2simfirst",
                //["2qco"] = "mc-rp-hero-row-timer-2",
                //["2x69"] = "mc-signaling-usewebsockets",
                //["2x6m"] = "mc-signaling-useturn",
                //["3gfd"] = "mcmktvlt-offerids-recos_lgbm3c",
                //["3gth"] = "mc-cloud-file-upload",
                //["3gw8"] = "mc-oneds-prod",
                //["3iu5"] = "mc-realms-cayman",
                //["3ol8"] = "mc-realms-libhttp",
                //["4geb"] = "mc-rp-morelicensedsidebar",
                //["4j7l"] = "control-tower-says-yes",
                //["4o5r"] = "raknet-enabled",
                //["4p1g"] = "mc-rp-nycbanner3",
                //["4pan"] = "mc-rp-risinglava",
            ],
            [
                new Config(
                    "Minecraft",
                    [
                        with(StringComparer.Ordinal),
                        // ["sunsetting"] = true,
                        // ["algo"] = "two",
                        // ["fjkdsafjlkdsafdjlk"] = true,
                        // ["mc-signaling-usewebsockets"] = true,
                        // ["mc-signaling-useturn"]= true,
                        // ["lgbm3c"] = true,
                        // ["mc-cloud-file-upload"] = true,
                        // ["mc-oneds-prod"] = true,
                        // ["ennables_realms_cayman_sub"] = true,
                        // ["mc-realms-libhttp-treatment-032922"] = true,
                        // ["dfasdfsfd"] = true,
                        // ["mc-control-tower-says-yes"] = true,
                        // ["mc-bedrock-host-raknet"] = true,
                        // ["nyc3banner132023"] = true,
                        // ["withbuttonart132023"] = true,
                    ]
                ),
            ],
            null, // Array.Empty<object>(),
            53535138,
            "CE0898A92D6A4755A37D4F4561983AFE",
           "" // "mc-sunsetting_1:30259009;mc-reco-algo2simfirst:30617967;mc-rp-hero-row-timer-2:30321236;mc-signaling-usewebsockets:30418088;mc-signaling-useturn:30357350;mcmktvlt-offerids-recos_lgbm3c:30622925;mc-cloud-file-upload:30440330;mc-oneds-prod:30450267;mc-realms-cayman:30606215;mc-realms-libhttp:30526657;mc-rp-morelicensedsidebar:30594955;control-tower-says-yes:30600225;raknet-enabled:30640316;mc-rp-nycbanner3:30636461;mc-rp-risinglava:30636462;"
        );
}