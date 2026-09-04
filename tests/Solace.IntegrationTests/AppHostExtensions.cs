using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Threading;

namespace Solace.IntegrationTests;

public static class AppHostExtensions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    public static async Task<DistributedApplication> RunAsync(HashSet<string> projects, string[]? args = null, CancellationToken cancellationToken = default)
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Solace_AppHost>(
            [
                "UseVolumes=false",
                "Shared:StaticDataPath=../../../../../staticdata",
                "TestMode=true",
                .. args ?? []
            ],
            cancellationToken);

        appHost.ConfigureStartup(projects);

        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        var app = await appHost.BuildAsync(cancellationToken)
            .WithTimeout(DefaultTimeout);
        await app.StartAsync(cancellationToken)
            .WithTimeout(DefaultTimeout);

        return app;
    }

    extension(IDistributedApplicationTestingBuilder appHost)
    {
        public void ConfigureStartup(params HashSet<string> projects)
        {
            foreach (var resource in appHost.Resources)
            {
                if (!projects.Contains(resource.Name))
                {
                    resource.Annotations.Add(new ExplicitStartupAnnotation());
                }
            }
        }
    }
}
