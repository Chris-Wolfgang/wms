// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Serilog.Core;
using Serilog.Events;

namespace Wolfgang.Wms.Logging;

/// <summary>
/// Rate-limits the chatty levels (E12.2): Verbose events and EF Core's SQL command logging are allowed a
/// fixed number per second; the rest of that second is dropped, so an elevated level cannot flood a disk or
/// a collector. Everything at Debug and above passes.
/// </summary>
public sealed class VerboseRateLimit : ILogEventFilter
{
    /// <summary>
    /// The source context of EF Core's SQL command logging.
    /// </summary>
    public const string SqlSourceContext = "Microsoft.EntityFrameworkCore.Database.Command";



    private readonly int _perSecond;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private long _second;
    private int _count;



    /// <summary>
    /// Creates the filter.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="perSecond"/> is not positive.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public VerboseRateLimit(int perSecond, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(perSecond, 0);
        _perSecond = perSecond;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }



    /// <summary>
    /// How many events were dropped since the filter was created.
    /// </summary>
    public long Dropped { get; private set; }



    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="logEvent"/> is null.</exception>
    public bool IsEnabled(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (logEvent.Level > LogEventLevel.Verbose && !IsSql(logEvent))
        {
            return true;
        }

        var second = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        lock (_gate)
        {
            if (second != _second)
            {
                _second = second;
                _count = 0;
            }

            if (_count < _perSecond)
            {
                _count++;
                return true;
            }

            Dropped++;
            return false;
        }
    }



    private static bool IsSql(LogEvent logEvent)
    {
        return logEvent.Properties.TryGetValue("SourceContext", out var source)
            && source is ScalarValue { Value: string context }
            && string.Equals(context, SqlSourceContext, StringComparison.Ordinal);
    }
}
