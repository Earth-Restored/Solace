using Solace.Common.ObjectPool;

namespace Solace.Common.Test;

public sealed class ObjectPoolTests
{
    [Test]
    public async Task HashSetPooledObjectPolicy_CreateAndReturn_WorksAsExpected()
    {
        var policy = new HashSetPooledObjectPolicy<int>
        {
            InitialCapacity = 16,
            MaximumRetainedCapacity = 100
        };

        var set = policy.Create();
        set.Add(1);
        set.Add(2);

        var returnResult = policy.Return(set);

        await Assert.That(returnResult).IsTrue();
        await Assert.That(set.Count).IsEqualTo(0);
    }

    [Test]
    public async Task HashSetPooledObjectPolicy_ExceedsMaximumRetainedCapacity_ReturnsFalse()
    {
        var policy = new HashSetPooledObjectPolicy<int>
        {
            MaximumRetainedCapacity = 5
        };

        var set = policy.Create();
        for (var i = 0; i < 20; i++)
        {
            set.Add(i);
        }

        var returnResult = policy.Return(set);

        await Assert.That(returnResult).IsFalse();
    }

    [Test]
    public async Task ListPooledObjectPolicy_CreateAndReturn_WorksAsExpected()
    {
        var policy = new ListPooledObjectPolicy<int>
        {
            InitialCapacity = 16,
            MaximumRetainedCapacity = 100
        };

        var list = policy.Create();
        list.Add(10);
        list.Add(20);

        var returnResult = policy.Return(list);

        await Assert.That(returnResult).IsTrue();
        await Assert.That(list.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ListPooledObjectPolicy_ExceedsMaximumRetainedCapacity_ReturnsFalse()
    {
        var policy = new ListPooledObjectPolicy<int>
        {
            MaximumRetainedCapacity = 5
        };

        var list = policy.Create();
        for (var i = 0; i < 20; i++)
        {
            list.Add(i);
        }

        var returnResult = policy.Return(list);

        await Assert.That(returnResult).IsFalse();
    }
}
