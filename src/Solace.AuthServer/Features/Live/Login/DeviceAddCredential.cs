namespace Solace.AuthServer.Features.Live.Login;

public static class DeviceAddCredential
{
    // get 415 with Immediate.Apis, can't customize enough when using it
    public static void MapEndpoint(IEndpointRouteBuilder app)
        => app.MapPost("login.live.com/ppsecure/deviceaddcredential.srf", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var rawXmlBody = await reader.ReadToEndAsync(context.RequestAborted);

            return TypedResults.Content("""
                <DeviceAddResponse Success="true"><success>true</success><puid>0</puid></DeviceAddResponse>
                """, contentType: "text/xml");
        })
        .DisableAntiforgery()
        .Accepts<string>("application/x-www-form-urlencoded");
}