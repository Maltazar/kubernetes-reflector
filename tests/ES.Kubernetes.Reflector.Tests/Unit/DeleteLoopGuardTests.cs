using ES.FX.Additions.KubernetesClient.Models;
using ES.Kubernetes.Reflector.Mirroring.Core;

namespace ES.Kubernetes.Reflector.Tests.Unit;

public class DeleteLoopGuardTests
{
    private static NamespacedName Target => new("ns", "name");

    [Fact]
    public void AllowsDeletes_UpToThreshold_WithinWindow()
    {
        var guard = new DeleteLoopGuard(threshold: 3, window: TimeSpan.FromSeconds(10),
            cooldown: TimeSpan.FromMinutes(5));
        var now = DateTimeOffset.UnixEpoch;

        Assert.True(guard.TryBeginDelete(Target, now, out _));
        Assert.True(guard.TryBeginDelete(Target, now, out _));
        Assert.True(guard.TryBeginDelete(Target, now, out _));
    }

    [Fact]
    public void SuppressesDelete_AfterThresholdExceeded_WithinWindow()
    {
        var guard = new DeleteLoopGuard(3, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
        var now = DateTimeOffset.UnixEpoch;
        for (var i = 0; i < 3; i++) Assert.True(guard.TryBeginDelete(Target, now, out _));

        Assert.False(guard.TryBeginDelete(Target, now, out var justTripped));
        Assert.True(justTripped);
    }

    [Fact]
    public void JustTripped_IsOnlyTrue_OnFirstSuppression()
    {
        var guard = new DeleteLoopGuard(1, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
        var now = DateTimeOffset.UnixEpoch;

        Assert.True(guard.TryBeginDelete(Target, now, out _));
        Assert.False(guard.TryBeginDelete(Target, now, out var first));
        Assert.True(first);
        Assert.False(guard.TryBeginDelete(Target, now.AddSeconds(1), out var second));
        Assert.False(second);
    }

    [Fact]
    public void ResetsCount_WhenWindowElapses()
    {
        var guard = new DeleteLoopGuard(2, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
        var now = DateTimeOffset.UnixEpoch;

        Assert.True(guard.TryBeginDelete(Target, now, out _));
        Assert.True(guard.TryBeginDelete(Target, now, out _));
        // Window elapsed — count resets, so a further delete is allowed instead of suppressed.
        Assert.True(guard.TryBeginDelete(Target, now.AddSeconds(11), out _));
    }

    [Fact]
    public void AllowsDeleteAgain_AfterCooldownElapses()
    {
        var guard = new DeleteLoopGuard(1, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
        var now = DateTimeOffset.UnixEpoch;

        Assert.True(guard.TryBeginDelete(Target, now, out _));
        Assert.False(guard.TryBeginDelete(Target, now, out _)); // tripped
        Assert.False(guard.TryBeginDelete(Target, now.AddMinutes(1), out _)); // still cooling down
        Assert.True(guard.TryBeginDelete(Target, now.AddMinutes(6), out _)); // cooldown elapsed
    }

    [Fact]
    public void TracksTargetsIndependently()
    {
        var guard = new DeleteLoopGuard(1, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
        var now = DateTimeOffset.UnixEpoch;
        var a = new NamespacedName("ns", "a");
        var b = new NamespacedName("ns", "b");

        Assert.True(guard.TryBeginDelete(a, now, out _));
        Assert.False(guard.TryBeginDelete(a, now, out _));
        Assert.True(guard.TryBeginDelete(b, now, out _));
    }

    [Fact]
    public void Clear_ResetsState()
    {
        var guard = new DeleteLoopGuard(1, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
        var now = DateTimeOffset.UnixEpoch;

        Assert.True(guard.TryBeginDelete(Target, now, out _));
        Assert.False(guard.TryBeginDelete(Target, now, out _));
        guard.Clear();
        Assert.True(guard.TryBeginDelete(Target, now, out _));
    }

    [Fact]
    public void Forget_ResetsStateForSingleTarget()
    {
        var guard = new DeleteLoopGuard(1, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
        var now = DateTimeOffset.UnixEpoch;

        Assert.True(guard.TryBeginDelete(Target, now, out _));
        Assert.False(guard.TryBeginDelete(Target, now, out _));
        guard.Forget(Target);
        Assert.True(guard.TryBeginDelete(Target, now, out _));
    }
}
