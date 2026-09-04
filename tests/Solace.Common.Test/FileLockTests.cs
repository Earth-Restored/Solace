using BitcoderCZ.IO;

namespace Solace.Common.Test;

public sealed class FileLockTests
{
    [Test]
    public async Task TryAcquire_AcquiresLockSuccessfully_AndReleasesOnDispose()
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"test_lock_{Guid.NewGuid():N}.lock");
        var absoluteFile = new AbsoluteFile(tempFilePath);
        var fileLock = new FileLock(absoluteFile);

        var handle1 = fileLock.TryAcquire();
        await Assert.That(handle1).IsNotNull();

        var handle2 = fileLock.TryAcquire();
        await Assert.That(handle2).IsNull();

        handle1!.Value.Dispose();

        var handle3 = fileLock.TryAcquire();
        await Assert.That(handle3).IsNotNull();
        handle3!.Value.Dispose();

        absoluteFile.Delete();
    }

    [Test]
    public async Task AcquireAsync_WithTimeout_TimesOutWhenLockIsHeld()
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"test_lock_{Guid.NewGuid():N}.lock");
        var absoluteFile = new AbsoluteFile(tempFilePath);
        var fileLock = new FileLock(absoluteFile);

        using (var handle = fileLock.TryAcquire())
        {
            await Assert.That(handle).IsNotNull();

            var action = () => fileLock.AcquireAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(20), CancellationToken.None);
            await Assert.That(action).Throws<OperationCanceledException>();
        }

        absoluteFile.Delete();
    }
}
