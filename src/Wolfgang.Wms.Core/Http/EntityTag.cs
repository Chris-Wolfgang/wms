// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;

namespace Wolfgang.Wms.Core.Http;

/// <summary>
/// A strong HTTP entity tag derived from the database row version (E1.12, ADR 0003). A single resource's tag
/// is its <c>row_version</c>; a list's or report's tag is the highest <c>row_version</c> in scope plus the
/// row count, so an insert, an update and a delete each change it. Validation costs one version column
/// read, never a hash of the body.
/// </summary>
/// <remarks>
/// Row versions are carried as <see cref="ulong"/>: SQL Server's 8-byte <c>rowversion</c> read big-endian,
/// PostgreSQL's <c>xmin</c> widened. Infrastructure does that conversion; callers here only see the number.
/// </remarks>
public readonly record struct EntityTag
{
    private EntityTag(string value)
    {
        Value = value;
    }



    /// <summary>
    /// The header value, quotes included, ready for <c>ETag</c>.
    /// </summary>
    public string Value { get; }



    /// <summary>
    /// The tag for one resource: its row version.
    /// </summary>
    public static EntityTag FromRowVersion(ulong rowVersion)
    {
        return new EntityTag(string.Create(CultureInfo.InvariantCulture, $"\"{rowVersion:x}\""));
    }



    /// <summary>
    /// The tag for a list or report: the highest row version in scope and the number of rows, so that a
    /// delete (count changes) is as visible as an update (version changes).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public static EntityTag FromCollection(ulong maxRowVersion, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return new EntityTag(string.Create(CultureInfo.InvariantCulture, $"\"{maxRowVersion:x}-{count:x}\""));
    }



    /// <summary>
    /// True when an <c>If-None-Match</c> header names this tag: the header is <c>*</c>, or one of its
    /// comma-separated entries equals <see cref="Value"/> after any <c>W/</c> weak marker is dropped
    /// (RFC 9110 weak comparison, which is what <c>If-None-Match</c> uses).
    /// </summary>
    public bool IsMatchedBy(string? ifNoneMatch)
    {
        if (string.IsNullOrWhiteSpace(ifNoneMatch))
        {
            return false;
        }

        var header = ifNoneMatch.AsSpan().Trim();
        if (header.SequenceEqual("*"))
        {
            return true;
        }

        foreach (var range in header.Split(','))
        {
            var candidate = header[range].Trim();
            if (candidate.StartsWith("W/", StringComparison.Ordinal))
            {
                candidate = candidate[2..];
            }

            if (candidate.SequenceEqual(Value))
            {
                return true;
            }
        }

        return false;
    }



    /// <inheritdoc/>
    public override string ToString()
    {
        return Value;
    }
}
