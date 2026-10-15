using UniversalSearch.Core;
using Xunit;

public sealed class LaunchGateTests
{
    [Fact]
    public async Task SlowShellDoesNotBlockCallerAndRepeatedEnterDoesNotLaunchTwice()
    {
        var gate = new LaunchGate();
        var shell = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int launches = 0, hides = 0;
        var first = gate.RunAsync(() => { launches++; return shell.Task; }, () => hides++);
        Assert.False(first.IsCompleted);
        Assert.False(await gate.RunAsync(() => { launches++; return Task.CompletedTask; }, () => hides++));
        Assert.Equal(0, hides);
        shell.SetResult();
        Assert.True(await first);
        Assert.Equal(1, launches);
        Assert.Equal(1, hides);
    }

    [Fact]
    public async Task FailedHandoffKeepsWindowVisibleAndAllowsRetry()
    {
        var gate = new LaunchGate(); int hides = 0;
        await Assert.ThrowsAsync<IOException>(() => gate.RunAsync(() => Task.FromException(new IOException()), () => hides++));
        Assert.Equal(0, hides);
        Assert.True(await gate.RunAsync(() => Task.CompletedTask, () => hides++));
        Assert.Equal(1, hides);
    }

    [Fact]
    public async Task SlowHistoryRunsAfterHideAndDoesNotHoldLaunchGate()
    {
        var gate = new LaunchGate(); bool hidden = false;
        var history = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Open()
        {
            if (await gate.RunAsync(() => Task.CompletedTask, () => hidden = true)) await history.Task;
        }
        var opening = Open();
        Assert.True(hidden);
        Assert.False(opening.IsCompleted);
        Assert.True(await gate.RunAsync(() => Task.CompletedTask, () => { }));
        history.SetResult(); await opening;
    }
}
