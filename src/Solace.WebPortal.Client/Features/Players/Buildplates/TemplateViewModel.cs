namespace Solace.WebPortal.Client.Features.Players.Buildplates;

public sealed class TemplateViewModel
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public int Size { get; set; }
    public int BlocksPerMeter { get; set; }
    public bool IsNight { get; set; }

    public bool IsAdding { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}