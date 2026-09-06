#!/usr/bin/env -S dotnet --
#:package System.CommandLine
#:package Spectre.Console

// todo: use commandline, take the path to the .env file - required, bool - overwrite entries - optional, default false

using System.CommandLine;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;

var envPathArg = new Argument<FileInfo>("env-file-path")
{
    Description = "Path to the .env file",
};

var overwriteOption = new Option<bool>("--overwrite", "-o")
{
    Description = "Overwrite existing entries in the .env file"
};

var imageHostOption = new Option<string>("--image-host", "-i")
{
    Description = "Container image host prefix"
};

var rootCommand = new RootCommand("Sets up a .env file with default values")
{
    envPathArg,
    overwriteOption,
    imageHostOption
};

List<string> projects =
[
    "api-server",
    "auth-server",
    "buildplate-launcher",
    "buildplate-server-setup",
    "buildplate-updater",
    "cdn",
    "event-bus",
    "locator",
    "object-store",
    "tappable-generator",
    "tile-renderer",
    "web-portal",
];

var defaults = new Dictionary<string, string>(StringComparer.Ordinal)
{
    { "NGINX_BINDMOUNT_0", "./nginx.conf" },
    { "NGINX_BINDMOUNT_1", "./certs" },
    { "SHARED_CAPTCHA_PROVIDER", "None" },
    { "SHARED_FIXUPBUILDPLATESONIMPORT", "false" },
    { "TILERENDERER_TILESOURCE_TILEJSONURL", "https://tiles.openfreemap.org/planet" },
    { "WEBPORTAL_BUILDPLATEPREVIEW_ENABLED", "true" },
    { "WEBPORTAL_BUILDPLATEPREVIEW_GENERATIONMAXCONCURRENCY", "1" },
};

const int InternalPort = 8080;

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var envFileArg = parseResult.GetValue(envPathArg);
    var overwriteEntries = parseResult.GetValue(overwriteOption);
    var imageHost = parseResult.GetValue(imageHostOption);

    if (string.IsNullOrWhiteSpace(imageHost))
    {
        imageHost = "ghcr.io/earth-restored";
    }

    if (envFileArg is null)
    {
        AnsiConsole.MarkupLine("[bold red]Error:[/] No .env file path specified.");
        return;
    }

    var envFilePath = envFileArg.FullName;

    var summaryGrid = new Grid()
        .AddColumn()
        .AddColumn()
        .AddRow("[grey]Target File:[/]", $"[yellow]{Markup.Escape(envFilePath)}[/]")
        .AddRow("[grey]Image Host:[/]", $"[blue]{Markup.Escape(imageHost)}[/]")
        .AddRow("[grey]Overwrite Existing:[/]", overwriteEntries ? "[bold red]Yes[/]" : "[bold green]No[/]");

    AnsiConsole.Write(summaryGrid);
    AnsiConsole.WriteLine();

    var envFile = await EnvFile.LoadAsync(envFilePath, cancellationToken);

    var baseUriString = imageHost.Contains("://", StringComparison.Ordinal) ? imageHost : $"https://{imageHost}";
    var imageHostUri = new Uri(baseUriString);

    var registryHost = imageHostUri.Authority;
    var repoPrefix = imageHostUri.AbsolutePath.Trim('/');

    using var httpClient = new HttpClient();

    var resultsTable = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("[bold]Variable Key[/]")
        .AddColumn("[bold]Value[/]");

    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("blue bold"))
        .StartAsync("Resolving OCI Image Digests...", async ctx =>
        {
            foreach (var projectName in projects)
            {
                ctx.Status($"Fetching latest digest for [bold yellow]{projectName}[/]...");

                var projectNameKey = projectName.Replace('-', '_').ToUpperOrdinal();
                var repoName = string.IsNullOrEmpty(repoPrefix)
                    ? $"solace-{projectName}"
                    : $"{repoPrefix}/solace-{projectName}";

                try
                {
                    var resolvedRef = await ResolveLatestDigestAsync(httpClient, registryHost, repoName, cancellationToken);

                    var imageSpecifier = resolvedRef.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                        ? $"{imageHost}/solace-{projectName}@{resolvedRef}"
                        : $"{imageHost}/solace-{projectName}:{resolvedRef}";

                    var imgKey = $"{projectNameKey}_IMAGE";
                    var portKey = $"{projectNameKey}_PORT";

                    envFile.Set(imgKey, imageSpecifier, $"Container image name for {projectName}", overwriteValue: overwriteEntries);
                    envFile.Set(portKey, InternalPort.ToString(), $"Default container port for {projectName}", overwriteValue: overwriteEntries);

                    resultsTable.AddRow($"[cyan]{imgKey}[/]", Markup.Escape(imageSpecifier));
                    resultsTable.AddRow($"[cyan]{portKey}[/]", InternalPort.ToString());
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[bold red]✖ Failed to resolve digest for {projectName}:[/] [grey]{Markup.Escape(ex.Message)}[/]");
                }
            }
        });

    foreach (var (key, value) in defaults)
    {
        envFile.Set(key, value, overwriteValue: overwriteEntries);
        resultsTable.AddRow($"[grey]{key}[/]", Markup.Escape(value));
    }

    await envFile.SaveAsync(envFilePath, cancellationToken);

    AnsiConsole.MarkupLine("[bold green] Environment entries applied successfully[/]\n");

    AnsiConsole.Write(
        new Panel(resultsTable)
            .Header("[bold cyan] Configured Environment Variables [/]")
            .Border(BoxBorder.Rounded));
});

return await rootCommand.Parse(args).InvokeAsync();

static async Task<string> ResolveLatestDigestAsync(HttpClient http, string registryHost, string repo, CancellationToken ct)
{
    var manifestUri = new UriBuilder("https", registryHost)
    {
        Path = $"/v2/{repo}/manifests/latest"
    }.Uri;

    using var request = CreateManifestRequest(manifestUri);

    using var response = await http.SendAsync(request, ct);

    if (response is { StatusCode: HttpStatusCode.Unauthorized, Headers.WwwAuthenticate.Count: > 0, })
    {
        var authHeader = response.Headers.WwwAuthenticate.First();
        var token = await FetchOciTokenAsync(http, authHeader.Parameter, ct);

        if (!string.IsNullOrEmpty(token))
        {
            using var retryReq = CreateManifestRequest(manifestUri);
            retryReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var retryRes = await http.SendAsync(retryReq, ct);
            if (retryRes.IsSuccessStatusCode && TryExtractDigest(retryRes, out var digest))
            {
                return digest;
            }
        }
    }
    else if (response.IsSuccessStatusCode && TryExtractDigest(response, out var digest))
    {
        return digest;
    }

#pragma warning disable CA2201 // Do not raise reserved exception types
    throw new Exception($"Image '{repo}' could not be found.");
#pragma warning restore CA2201 // Do not raise reserved exception types
}

static HttpRequestMessage CreateManifestRequest(Uri uri)
{
    var req = new HttpRequestMessage(HttpMethod.Head, uri);

    req.Headers.Accept.ParseAdd("application/vnd.docker.distribution.manifest.list.v2+json");
    req.Headers.Accept.ParseAdd("application/vnd.docker.distribution.manifest.v2+json");
    req.Headers.Accept.ParseAdd("application/vnd.oci.image.index.v1+json");
    req.Headers.Accept.ParseAdd("application/vnd.oci.image.manifest.v1+json");

    return req;
}

static bool TryExtractDigest(HttpResponseMessage response, [NotNullWhen(true)] out string? digest)
{
    if (response.Headers.TryGetValues("Docker-Content-Digest", out var values))
    {
        digest = values.FirstOrDefault();
        return !string.IsNullOrWhiteSpace(digest);
    }

    digest = null;
    return false;
}

static async Task<string> FetchOciTokenAsync(HttpClient http, string? challengeParameter, CancellationToken ct)
{
    if (string.IsNullOrEmpty(challengeParameter))
    {
        return "";
    }

    var paramsDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var matches = Regex.Matches(challengeParameter, @"(\w+)=\""([^\""]+)\""", RegexOptions.None, matchTimeout: TimeSpan.FromSeconds(1));

    foreach (var match in (IList<Match>)matches)
    {
        paramsDict[match.Groups[1].Value] = match.Groups[2].Value;
    }

    if (!paramsDict.TryGetValue("realm", out var realm))
    {
        return "";
    }

    var query = new List<string>();
    if (paramsDict.TryGetValue("service", out var service))
    {
        query.Add($"service={Uri.EscapeDataString(service)}");
    }

    if (paramsDict.TryGetValue("scope", out var scope))
    {
        query.Add($"scope={Uri.EscapeDataString(scope)}");
    }

    var tokenUri = $"{realm}?" + string.Join("&", query);

    try
    {
        using var doc = await JsonDocument.ParseAsync(await http.GetStreamAsync(tokenUri, ct), cancellationToken: ct);
        if (doc.RootElement.TryGetProperty("token", out var tokenProp))
        {
            return tokenProp.GetString() ?? "";
        }

        if (doc.RootElement.TryGetProperty("access_token", out var accessTokenProp))
        {
            return accessTokenProp.GetString() ?? "";
        }
    }
    catch
    {
    }

    return "";
}

sealed class EnvFile
{
    private abstract class Node
    {
    }

    private sealed class KeyNode : Node
    {
        public string Key { get; set; }
        public string? Value { get; set; }
        public string? Comment { get; set; }

        public KeyNode(string key, string? value, string? comment)
        {
            Key = key;
            Value = value;
            Comment = comment;
        }
    }

    private sealed class RawNode : Node
    {
        public string Content { get; set; }
        public RawNode(string content)
        {
            Content = content;
        }
    }

    private readonly List<Node> _nodes = [];
    private readonly Dictionary<string, KeyNode> _keyNodes = [with(StringComparer.Ordinal)];

    public IEnumerable<string> Keys => _keyNodes.Keys;

    public static async Task<EnvFile> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        using (var reader = File.OpenText(filePath))
        {
            return await ParseAsync(reader, cancellationToken);
        }
    }

    public static async Task<EnvFile> ParseAsync(TextReader reader, CancellationToken cancellationToken = default)
    {
        var env = new EnvFile();

        var pendingComments = new List<string>();
        var pendingRawLines = new List<string>();

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith('#'))
            {
                pendingComments.Add(ExtractCommentText(line));
                pendingRawLines.Add(line);
            }
            else if (string.IsNullOrWhiteSpace(trimmed))
            {
                FlushPendingCommentsAsRaw(env, pendingRawLines, pendingComments);
                env._nodes.Add(new RawNode(line));
            }
            else if (trimmed.Contains('='))
            {
                var eqIndex = trimmed.IndexOf('=');
                var key = trimmed.AsSpan(0, eqIndex).Trim().ToString();
                var val = trimmed.AsSpan(eqIndex + 1).Trim().ToString();

                var comment = pendingComments.Count > 0
                    ? string.Join(Environment.NewLine, pendingComments)
                    : null;

                pendingComments.Clear();
                pendingRawLines.Clear();

                var keyNode = new KeyNode(key, val, comment);
                env._nodes.Add(keyNode);
                env._keyNodes[key] = keyNode;
            }
            else
            {
                FlushPendingCommentsAsRaw(env, pendingRawLines, pendingComments);
                env._nodes.Add(new RawNode(line));
            }
        }

        FlushPendingCommentsAsRaw(env, pendingRawLines, pendingComments);

        return env;
    }

    public bool ContainsKey(string key)
        => _keyNodes.ContainsKey(key);

    public bool TryGet(string key, [MaybeNullWhen(false)] out string? value, [MaybeNullWhen(false)] out string? comment)
    {
        if (_keyNodes.TryGetValue(key, out var node))
        {
            value = node.Value;
            comment = node.Comment;
            return true;
        }

        value = null;
        comment = null;
        return false;
    }

    public void Set(string key, string? value, string? comment = null, bool overwriteValue = true, bool overwriteComment = true)
    {
        ref var node = ref CollectionsMarshal.GetValueRefOrAddDefault(_keyNodes, key, out var exists);
        if (!exists)
        {
            node = new KeyNode(key, value, comment);
            return;
        }

        Debug.Assert(node is not null);

        if (overwriteValue || string.IsNullOrWhiteSpace(node.Value))
        {
            node.Value = value;
        }

        if (overwriteComment || string.IsNullOrWhiteSpace(node.Comment))
        {
            node.Comment = comment;
        }
    }

    public void Set(string key, string value, string? comment = null, bool overwriteComment = false)
    {
        if (_keyNodes.TryGetValue(key, out var existing))
        {
            existing.Value = value;
            if (comment != null && (string.IsNullOrEmpty(existing.Comment) || overwriteComment))
            {
                existing.Comment = comment;
            }
        }
        else
        {
            var node = new KeyNode(key, value, comment);
            _nodes.Add(node);
            _keyNodes[key] = node;
        }
    }

    public bool Remove(string key)
    {
        if (_keyNodes.TryGetValue(key, out var node))
        {
            _keyNodes.Remove(key);
            _nodes.Remove(node);
            return true;
        }

        return false;
    }

    public async Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
        => await File.WriteAllTextAsync(filePath, ToString(), cancellationToken);

    private static readonly string[] NewLines = ["\r\n", "\n"];

    public override string ToString()
    {
        var sb = new StringBuilder();

        foreach (var node in _nodes)
        {
            if (node is RawNode raw)
            {
                sb.AppendLine(raw.Content);
            }
            else if (node is KeyNode keyNode)
            {
                if (!string.IsNullOrEmpty(keyNode.Comment))
                {
                    var lines = keyNode.Comment.Split(NewLines, StringSplitOptions.None);
                    foreach (var commentLine in lines)
                    {
                        sb.AppendLine($"# {commentLine}");
                    }
                }

                sb.AppendLine($"{keyNode.Key}={keyNode.Value}");
            }
        }

        return sb.ToString();
    }

    private static void FlushPendingCommentsAsRaw(EnvFile env, List<string> rawLines, List<string> comments)
    {
        foreach (var raw in rawLines)
        {
            env._nodes.Add(new RawNode(raw));
        }

        rawLines.Clear();
        comments.Clear();
    }

    private static string ExtractCommentText(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith('#'))
        {
            var trimmedSpan = trimmed.AsSpan()[1..];
            if (trimmedSpan.StartsWith(' '))
            {
                trimmedSpan = trimmedSpan[1..];
            }

            trimmed = trimmedSpan.ToString();
        }

        return trimmed;
    }
}