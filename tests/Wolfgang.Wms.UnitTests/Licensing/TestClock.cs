// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.UnitTests.Licensing;

/// <summary>
/// A clock the test moves by hand; timers created on it (a <see cref="PeriodicTimer"/>'s) fire when the
/// clock passes their due time.
/// </summary>
internal sealed class TestClock : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now;



    public TestClock(DateTimeOffset now)
    {
        _now = now;
    }



    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }



    public void Advance(TimeSpan by)
    {
        _now += by;
        foreach (var timer in _timers.ToArray())
        {
            timer.Fire(_now);
        }
    }



    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        _timers.Add(timer);
        return timer;
    }



    private sealed class ManualTimer : ITimer
    {
        private readonly TestClock _clock;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private DateTimeOffset? _due;
        private TimeSpan _period;

        public ManualTimer(TestClock clock, TimerCallback callback, object? state)
        {
            _clock = clock;
            _callback = callback;
            _state = state;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _due = dueTime == Timeout.InfiniteTimeSpan ? null : _clock._now + dueTime;
            _period = period;
            return true;
        }

        public void Fire(DateTimeOffset now)
        {
            if (_due is { } due && due <= now)
            {
                _due = _period > TimeSpan.Zero ? now + _period : null;
                _callback(_state);
            }
        }

        public void Dispose()
        {
            _clock._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
