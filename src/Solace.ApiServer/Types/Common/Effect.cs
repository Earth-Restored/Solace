namespace Solace.ApiServer.Types.Common;

internal sealed record Effect(
    string Type,
    string? Duration,
    float? Value,
    string? Unit,
    string Targets,
    Guid[] Items,
    string[] ItemScenarios,
    string Activation,
    string? ModifiesType
);