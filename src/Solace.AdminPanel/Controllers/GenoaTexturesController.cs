using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Solace.AdminPanel.Controllers;

[ApiController]
[Authorize(Policy = Permissions.ManagePlayers)]
[Route("api/genoa-textures")]
internal sealed class GenoaTexturesController : ControllerBase
{
    private readonly string _staticDataPath;

    public GenoaTexturesController(IConfiguration configuration)
    {
        _staticDataPath = configuration["StaticDataPath"]!;
    }

    [HttpGet("ui/items/{name}.png")]
    [HttpHead("ui/items/{name}.png")]
    public async Task<Results<PhysicalFileHttpResult, NotFound>> GetUiTexture(string name)
    {
        var cachePath = await GenoaResourcepackCache.GetCachePath(_staticDataPath);

        if (cachePath is null)
        {
            return TypedResults.NotFound();
        }

        var path = Path.Combine(cachePath, "textures", "ui", "items", name + ".png");

        var baseDirectory = Path.GetFullPath(Path.Combine(cachePath, "textures", "ui", "items"));

        var fullPath = Path.GetFullPath(Path.Combine(baseDirectory, name + ".png"));

        if (!fullPath.StartsWith(baseDirectory, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return TypedResults.NotFound();
        }

#pragma warning disable CA3003 // Review code for file path injection vulnerabilities - mitigated by the above code
        if (!System.IO.File.Exists(fullPath))
        {
            return TypedResults.NotFound();
        }
#pragma warning restore CA3003 // Review code for file path injection vulnerabilities

        return TypedResults.PhysicalFile(path);
    }
}