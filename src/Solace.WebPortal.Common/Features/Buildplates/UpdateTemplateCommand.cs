namespace Solace.WebPortal.Common.Features.Buildplates;

public sealed record UpdateTemplateCommand(Guid Id, string Name, int BlocksPerMeter);
