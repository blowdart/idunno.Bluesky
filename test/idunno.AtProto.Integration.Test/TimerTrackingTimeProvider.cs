// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Channels;

using Microsoft.Extensions.Time.Testing;

namespace idunno.AtProto.Integration.Test;

internal sealed class TimerTrackingTimeProvider(TimeSpan watchedDueTime) : FakeTimeProvider
{
    private readonly Channel<bool> _timers = Channel.CreateUnbounded<bool>();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ITimer timer = base.CreateTimer(callback, state, dueTime, period);
        if (dueTime == watchedDueTime)
        {
            Assert.True(_timers.Writer.TryWrite(true));
        }

        return timer;
    }

    internal async Task WaitForTimerAsync(CancellationToken cancellationToken)
    {
        _ = await _timers.Reader.ReadAsync(cancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
    }
}
