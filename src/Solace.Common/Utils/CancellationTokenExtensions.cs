namespace Solace.Common.Utils;

public static class CancellationTokenExtensions
{
    extension(CancellationToken cancellationToken)
    {
        public Task AsTask()
            => Task.Delay(Timeout.Infinite, cancellationToken);
    }
}