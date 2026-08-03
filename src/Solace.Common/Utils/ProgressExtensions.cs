namespace Solace.Common.Utils;

public static class ProgressExtensions
{
    extension(IProgress<ProgressReport> progress)
    {
        public void Complete()
            => progress.Report(new ProgressReport(1d, "Done"));

        public IProgress<ProgressReport> WrapRange(double from, double to)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(from);
            ArgumentOutOfRangeException.ThrowIfNegative(to);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(from, to);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(to, 1.0);

            var range = to - from;

            return new Progress<ProgressReport>(report =>
            {
                progress.Report(new ProgressReport((report.PercentComplete * range) + from, report.StatusMessage));
            });
        }
    }
}
