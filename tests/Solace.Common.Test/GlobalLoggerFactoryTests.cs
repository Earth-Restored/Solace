using Microsoft.Extensions.Logging.Abstractions;

namespace Solace.Common.Test;

public sealed class GlobalLoggerFactoryTests
{
    [Test]
    public async Task CreateLogger_BeforeOrAfterInitialize_ReturnsNonNullLogger()
    {
        var logger1 = GlobalLoggerFactory.CreateLogger("TestCategory");
        await Assert.That(logger1).IsNotNull();

        var logger2 = GlobalLoggerFactory.CreateLogger<GlobalLoggerFactoryTests>();
        await Assert.That(logger2).IsNotNull();

        GlobalLoggerFactory.Initialize(NullLoggerFactory.Instance);

        var logger3 = GlobalLoggerFactory.CreateLogger("TestCategory2");
        await Assert.That(logger3).IsNotNull();
    }

    [Test]
    public async Task Initialize_NullFactory_ThrowsArgumentNullException()
    {
        Action action = () => GlobalLoggerFactory.Initialize(null!);
        await Assert.That(action).Throws<ArgumentNullException>();
    }
}
