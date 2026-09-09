#!/usr/bin/env -S dotnet --
#:package Spectre.Console
#:package YamlDotNet

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Spectre.Console;
using YamlDotNet.Serialization;

AnsiConsole.Write(new FigletText("Solace Setup").LeftJustified().Color(Color.Green));
AnsiConsole.MarkupLine("[bold cyan]Welcome to the Solace Setup Script[/]");
AnsiConsole.WriteLine();

var useDomain = AnsiConsole.Confirm("Are you using a [bold blue]domain name[/]?", defaultValue: false);
var domain = "";
var ip = "";
var hasHttps = false;
var useSubdomains = false;

string? certPath = null;
string? keyPath = null;

if (useDomain)
{
    domain = AnsiConsole.Ask<string>("What is your [bold green]domain name[/]?");
    hasHttps = AnsiConsole.Confirm("Do you have an [bold blue]HTTPS certificate[/]? (Use https, forces subdomains)", defaultValue: true);

    if (hasHttps)
    {
        useSubdomains = true;

        certPath = AnsiConsole.Prompt(
            new TextPrompt<string>("Path to your [bold green]SSL certificate file (.crt/.pem)[/]:")
                .Validate(path => File.Exists(path)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]File does not exist.[/]")));

        keyPath = AnsiConsole.Prompt(
            new TextPrompt<string>("Path to your [bold green]SSL private key file (.key)[/]:")
                .Validate(path => File.Exists(path)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]File does not exist.[/]")));
    }
    else
    {
        useSubdomains = AnsiConsole.Confirm("Use [bold blue]subdomains[/] instead of ports for routing?", defaultValue: true);
    }
}
else
{
    ip = AnsiConsole.Ask<string>("What is your server [bold green]IP address[/]?");
}

var endpoints = new List<EndpointConfig>
{
    new("web-portal", "SHARED_PUBLICENDPOINTS_WEBPORTAL", "", 80),
    new("locator", "SHARED_PUBLICENDPOINTS_LOCATOR", "locator", 8080),
    new("auth-server", "SHARED_PUBLICENDPOINTS_AUTHSERVER", "auth", 8088),
    new("api-server", "SHARED_PUBLICENDPOINTS_APISERVER", "api", 8089),
    new("cdn", "SHARED_PUBLICENDPOINTS_CDN", "cdn", 8090)
};

AnsiConsole.MarkupLine("\n[bold cyan]Endpoint Configuration:[/]");

foreach (var ep in endpoints)
{
    if (useSubdomains)
    {
        if (ep.Name is "web-portal")
        {
            ep.Subdomain = "";
        }
        else
        {
            ep.Subdomain = AnsiConsole.Ask<string>($"Subdomain for [bold yellow]{ep.Name}[/]", ep.DefaultSubdomain);
        }
    }
    else
    {
        ep.Port = AnsiConsole.Ask<int>($"Port for [bold yellow]{ep.Name}[/]", ep.DefaultPort);
    }
}

foreach (var ep in endpoints)
{
    var scheme = hasHttps ? "https" : "http";
    if (useDomain)
    {
        if (useSubdomains)
        {
            var host = string.IsNullOrEmpty(ep.Subdomain) ? domain : $"{ep.Subdomain}.{domain}";
            ep.FinalUrl = $"{scheme}://{host}";
        }
        else
        {
            ep.FinalUrl = $"{scheme}://{domain}:{ep.Port}";
        }
    }
    else
    {
        ep.FinalUrl = $"http://{ip}:{ep.Port}";
    }
}

AnsiConsole.WriteLine();
var baseBuildplatePort = AnsiConsole.Ask<int>("Base [bold green]buildplate instance public port[/]?", 19132);
var buildplatePortCount = AnsiConsole.Ask<int>("How many [bold green]buildplate ports[/] to export?", 16);

AnsiConsole.WriteLine();
var jarPath = AnsiConsole.Prompt(
    new TextPrompt<string>("(Optional) Path to [bold green]Minecraft Java edition 1.20.5 .jar[/] (Leave empty to skip):")
        .AllowEmpty()
        .Validate(path => string.IsNullOrWhiteSpace(path) || File.Exists(path)
            ? ValidationResult.Success()
            : ValidationResult.Error("[red]File does not exist.[/]")));

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("Please read the Minecraft EULA: [link]https://www.minecraft.net/en-us/eula[/]");
var agreeEula = AnsiConsole.Confirm("Do you [bold green]agree[/] to the Minecraft EULA? (Required for buildplates)", defaultValue: false);
AnsiConsole.WriteLine();

await AnsiConsole.Status()
    .StartAsync("Applying setup...", async ctx =>
    {
        ctx.Status("Creating Directories...");

        Directory.CreateDirectory("data");
        Directory.CreateDirectory("data/object_store");
        Directory.CreateDirectory("dataprotection-keys");
        Directory.CreateDirectory("certs");
        Directory.CreateDirectory("certs/web-portal");
        if (!OperatingSystem.IsWindows())
        {
            var unixAllReadWriteView = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            File.SetUnixFileMode("data", unixAllReadWriteView);
            File.SetUnixFileMode("data/object_store", unixAllReadWriteView);
            File.SetUnixFileMode("dataprotection-keys", unixAllReadWriteView);
            File.SetUnixFileMode("certs", unixAllReadWriteView);
            File.SetUnixFileMode("certs/web-portal", unixAllReadWriteView);

            SetReadWrite("staticdata/server_template_dir");
            SetReadWrite("staticdata/server_template_dir/mods");

            static void SetReadWrite(string path)
            {
                var allReadWrite = UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

                Directory.CreateDirectory(path);
                File.SetUnixFileMode(path, File.GetUnixFileMode(path) | allReadWrite);
            }
        }

        if (hasHttps && !string.IsNullOrWhiteSpace(certPath) && !string.IsNullOrWhiteSpace(keyPath))
        {
            ctx.Status("Copying HTTPS Certificates...");
            var targetCertDir = Path.Combine(".", "certs");
            Directory.CreateDirectory(targetCertDir);

            File.Copy(certPath, Path.Combine(targetCertDir, Path.GetFileName(certPath)), overwrite: true);
            File.Copy(keyPath, Path.Combine(targetCertDir, Path.GetFileName(keyPath)), overwrite: true);
        }

        if (!string.IsNullOrWhiteSpace(jarPath))
        {
            ctx.Status("Extracting Minecraft Resource Pack...");
            var assetsDest = Path.Combine("staticdata", "resourcepacks", "java", "1. minecraft", "assets");
            Directory.CreateDirectory(assetsDest);

            using var archive = ZipFile.OpenRead(jarPath);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith("assets/", StringComparison.Ordinal) && !entry.FullName.EndsWith('/'))
                {
                    var destFile = Path.Combine(assetsDest, entry.FullName["assets/".Length..]);
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                    entry.ExtractToFile(destFile, overwrite: true);
                }
            }
        }

        ctx.Status("Generating OIDC Certificates...");
        var certDir = Path.Combine("certs", "web-portal");
        Directory.CreateDirectory(certDir);

        var encPassword = GenerateRandomPassword();
        var signPassword = GenerateRandomPassword();

        GenerateSelfSignedCert("OIDC Encryption", Path.Combine(certDir, "oidc-encryption-cert.pfx"), encPassword);
        GenerateSelfSignedCert("OIDC Signing", Path.Combine(certDir, "oidc-signing-cert.pfx"), signPassword);

        ctx.Status("Generating nginx.conf...");
        var nginxConfig = GenerateNginxConfig(endpoints, domain, hasHttps, useSubdomains, certPath is null ? null : Path.GetFileName(certPath), keyPath is null ? null : Path.GetFileName(keyPath));
        await File.WriteAllTextAsync("nginx.conf", nginxConfig);

        ctx.Status("Generating docker-compose.override.yml...");
        await UpdateDockerComposeOverrideAsync("docker-compose.override.yml", endpoints, hasHttps, useSubdomains, baseBuildplatePort, buildplatePortCount);

        ctx.Status("Updating .env file...");
        EnvFile env;
        if (File.Exists(".env"))
        {
            env = await EnvFile.LoadAsync(".env");
        }
        else
        {
            env = await EnvFile.ParseAsync(new StringReader(""));
        }

        env.SetIfEmpty("DASHBOARD_OTLP_PRIMARY_APIKEY", GenerateRandomHex(32));
        env.SetIfEmpty("POSTGRES_PASSWORD", GenerateRandomPassword(24));
        env.Set("SHARED_ACCEPTMINECRAFTEULA", agreeEula ? "true" : "false");
        env.SetIfEmpty("SHARED_OIDC_WEBPORTAL_AUTHSERVER_CLIENTSECRET", GenerateRandomHex(32));
        env.Set("SHARED_OIDC_WEBPORTAL_ENCRYPTIONCERTPASSWORD", encPassword);
        env.Set("SHARED_OIDC_WEBPORTAL_SIGNINGCERTPASSWORD", signPassword);

        foreach (var ep in endpoints)
        {
            env.Set(ep.EnvKey, ep.FinalUrl!);
        }

        await env.SaveAsync(".env");
    });

AnsiConsole.MarkupLine("[bold green]Setup Complete![/]");
AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[bold yellow]Final Step Required:[/]");
AnsiConsole.MarkupLine("Please download [link]https://cdn.mceserv.net/availableresourcepack/resourcepacks/dba38e59-091a-4826-b76a-a08d7de5a9e2-1301b0c257a311678123b9e7325d0d6c61db3c35[/] using Wayback Machine.");
AnsiConsole.MarkupLine("Rename it to [bold white]vanilla.zip[/] and put it into [bold cyan]staticdata/resourcepacks/[/]");
AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("Once done, you can run [bold green]up.ps1[/] or[bold green]up.sh[/] to start the server.");

string GenerateRandomPassword(int length = 24)
{
    Span<byte> span = stackalloc byte[length];
    RandomNumberGenerator.Fill(span);
    return Convert.ToBase64String(span).Replace("+", "").Replace("/", "").Replace("=", "")[..length];
}

string GenerateRandomHex(int bytes = 32)
{
    Span<byte> span = stackalloc byte[bytes];
    RandomNumberGenerator.Fill(span);
    return Convert.ToHexString(span).ToLowerOrdinal();
}

void GenerateSelfSignedCert(string subject, string path, string password)
{
    using var rsa = RSA.Create(2048);
    var req = new CertificateRequest($"CN={subject}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(5));
    File.WriteAllBytes(path, cert.Export(X509ContentType.Pfx, password));
}

async Task UpdateDockerComposeOverrideAsync(string filePath, List<EndpointConfig> endpoints, bool https, bool subdomains, int baseBuildplatePort, int buildplatePortCount)
{
    var requiredPorts = GetRequiredPorts(endpoints, https, subdomains);
    var portList = requiredPorts.Select(p => $"{p}:{p}").ToList();

    var deserializer = new DeserializerBuilder().Build();
    var serializer = new SerializerBuilder().Build();

    Dictionary<object, object> root = [];

    if (File.Exists(filePath))
    {
        var existingContent = await File.ReadAllTextAsync(filePath);
        if (!string.IsNullOrWhiteSpace(existingContent))
        {
            var deserialized = deserializer.Deserialize<object>(existingContent);
            if (deserialized is Dictionary<object, object> map)
            {
                root = map;
            }
        }
    }

    var services = GetOrCreateMap(root, "services");
    var nginx = GetOrCreateMap(services, "nginx");

    nginx["ports"] = portList;

    var buildplateLauncher = GetOrCreateMap(services, "buildplate-launcher");
    var environment = GetOrCreateMap(buildplateLauncher, "environment");
    environment["BaseInstancePublicPort"] = baseBuildplatePort.ToString();

    var buildplatePorts = Enumerable.Range(baseBuildplatePort, buildplatePortCount)
        .Select(p => $"{p}:{p}")
        .ToList();

    buildplateLauncher["ports"] = buildplatePorts;

    var newYaml = serializer.Serialize(root);
    await File.WriteAllTextAsync(filePath, newYaml);
}

Dictionary<object, object> GetOrCreateMap(Dictionary<object, object> parent, string key)
{
    foreach (var entry in parent)
    {
        if (entry.Key?.ToString() == key && entry.Value is Dictionary<object, object> childMap)
        {
            return childMap;
        }
    }

    var newMap = new Dictionary<object, object>();
    parent[key] = newMap;
    return newMap;
}

List<int> GetRequiredPorts(List<EndpointConfig> endpoints, bool https, bool subdomains)
{
    var ports = new HashSet<int>();

    if (subdomains)
    {
        ports.Add(80);
        if (https)
        {
            ports.Add(443);
        }
    }
    else
    {
        foreach (var endpoint in endpoints)
        {
            ports.Add(endpoint.Port);
        }
    }

    return [.. ports];
}

string GenerateNginxConfig(List<EndpointConfig> endpoints, string domain, bool https, bool subdomains, string? certFile, string? keyFile)
{
    var builder = new StringBuilder();
    builder.AppendLine("events { worker_connections 1024; }");
    builder.AppendLine();
    builder.AppendLine("http {");
    builder.AppendLine("    resolver 127.0.0.11 valid=10s ipv6=off;");
    builder.AppendLine();

    if (https)
    {
        builder.AppendLine($"    ssl_certificate /etc/nginx/certs/{certFile};");
        builder.AppendLine($"    ssl_certificate_key /etc/nginx/certs/{keyFile};");
        builder.AppendLine("    ssl_protocols TLSv1.2 TLSv1.3;");
        builder.AppendLine();
    }

    builder.AppendLine("    proxy_set_header Host $host;");
    builder.AppendLine("    proxy_set_header X-Real-IP $remote_addr;");
    builder.AppendLine("    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;");
    builder.AppendLine("    proxy_set_header X-Forwarded-Proto $scheme;");
    builder.AppendLine("    proxy_set_header X-Forwarded-Host $host;");
    builder.AppendLine();

    foreach (var endpoint in endpoints)
    {
        builder.AppendLine("    server {");
        if (https && subdomains)
        {
            builder.AppendLine("        listen 443 ssl;");
            var hostName = string.IsNullOrEmpty(endpoint.Subdomain) ? domain : $"{endpoint.Subdomain}.{domain}";
            builder.AppendLine($"        server_name {hostName};");
        }
        else if (!https && subdomains)
        {
            builder.AppendLine("        listen 80;");
            var hostName = string.IsNullOrEmpty(endpoint.Subdomain) ? domain : $"{endpoint.Subdomain}.{domain}";
            builder.AppendLine($"        server_name {hostName};");
        }
        else
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"        listen {endpoint.Port};");
            builder.AppendLine("        server_name _;");
        }

        builder.AppendLine();
        builder.AppendLine("        location / {");
        builder.AppendLine($"            set $upstream_target http://{endpoint.Name}:8080;");
        builder.AppendLine("            proxy_pass $upstream_target;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    if (https)
    {
        builder.AppendLine("    server {");
        builder.AppendLine("        listen 80;");
        builder.AppendLine("        server_name _;");
        builder.AppendLine("        return 301 https://$host$request_uri;");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    builder.AppendLine("}");
    return builder.ToString();
}

internal sealed class EndpointConfig
{
    public string Name { get; }
    public string EnvKey { get; }
    public string DefaultSubdomain { get; }
    public int DefaultPort { get; }

    public string? Subdomain { get; set; }
    public int Port { get; set; }
    public string? FinalUrl { get; set; }

    public EndpointConfig(string name, string envKey, string defaultSubdomain, int defaultPort)
    {
        Name = name;
        EnvKey = envKey;
        DefaultSubdomain = defaultSubdomain;
        DefaultPort = defaultPort;
    }
}

internal sealed class EnvFile
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

    public void SetIfEmpty(string key, string? value, string? comment = null)
    {
        ref var node = ref CollectionsMarshal.GetValueRefOrAddDefault(_keyNodes, key, out var exists);
        if (!exists)
        {
            node = new KeyNode(key, value, comment);
            return;
        }

        Debug.Assert(node is not null);

        if (string.IsNullOrWhiteSpace(node.Value))
        {
            node.Value = value;
        }

        if (string.IsNullOrWhiteSpace(node.Comment))
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