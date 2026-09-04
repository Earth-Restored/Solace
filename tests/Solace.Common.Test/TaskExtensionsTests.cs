using Solace.Common.Utils;

namespace Solace.Common.Test;

public sealed class TaskExtensionsTests
{
    [Test]
    public async Task Forget_SuccessfulTask_ExecutesWithoutException()
    {
        var task = Task.FromResult(42);
        task.Forget();
        await Assert.That(task.IsCompletedSuccessfully).IsTrue();
    }

    [Test]
    public async Task Forget_FaultedTaskWithHandler_TriggersHandler()
    {
        var tcs = new TaskCompletionSource();
        var handlerTriggered = new TaskCompletionSource<Exception>();

        var task = tcs.Task;
        task.Forget(ex => handlerTriggered.SetResult(ex));

        var expectedException = new InvalidOperationException("Test fault");
        tcs.SetException(expectedException);

        var caughtException = await handlerTriggered.Task;
        await Assert.That(caughtException).IsEqualTo(expectedException);
    }
}
