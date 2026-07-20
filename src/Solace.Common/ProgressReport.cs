namespace Solace.Common;

// PercentComplete - 0 to 1
// StatusMessage - null - don't update
public readonly record struct ProgressReport(double PercentComplete, string? StatusMessage);
