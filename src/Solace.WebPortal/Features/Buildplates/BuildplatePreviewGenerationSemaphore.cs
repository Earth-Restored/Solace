namespace Solace.WebPortal.Features.Buildplates;

public sealed class BuildplatePreviewGenerationSemaphore
{
    public BuildplatePreviewGenerationSemaphore(IConfiguration configuration)
        : base()
    {
        var maxConcurrency = configuration.GetValue<int>("BuildplatePreview:GenerationMaxConcurrency", 2);

        Semaphore = new(maxConcurrency, maxConcurrency);
    }

    public SemaphoreSlim Semaphore { get; }
}