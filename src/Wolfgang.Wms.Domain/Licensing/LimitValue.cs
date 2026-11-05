// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// A limit's value (E79.1): a count, or unlimited.
/// </summary>
public readonly record struct LimitValue
{
    private LimitValue(int? value)
    {
        Value = value;
    }



    /// <summary>
    /// The count, or null when unlimited.
    /// </summary>
    public int? Value { get; }



    /// <summary>
    /// True when there is no ceiling.
    /// </summary>
    public bool IsUnlimited => Value is null;



    /// <summary>
    /// No ceiling.
    /// </summary>
    public static LimitValue Unlimited { get; } = new(value: null);



    /// <summary>
    /// A ceiling of <paramref name="count"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public static LimitValue Of(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new LimitValue(count);
    }



    /// <summary>
    /// The value plus <paramref name="count"/> (unlimited stays unlimited).
    /// </summary>
    public LimitValue Plus(int count)
    {
        return IsUnlimited ? this : Of(checked(Value!.Value + count));
    }



    /// <summary>
    /// The lower of two values; unlimited never lowers.
    /// </summary>
    public static LimitValue Min(LimitValue left, LimitValue right)
    {
        if (left.IsUnlimited)
        {
            return right;
        }

        return right.IsUnlimited || left.Value <= right.Value ? left : right;
    }



    /// <summary>
    /// True when <paramref name="other"/> is lower than this (a reduction, which tier tables forbid).
    /// </summary>
    public bool IsReducedBy(LimitValue other)
    {
        return !other.IsUnlimited && (IsUnlimited || other.Value < Value);
    }



    /// <summary>
    /// True when <paramref name="count"/> exceeds the ceiling.
    /// </summary>
    public bool IsExceededBy(int count)
    {
        return !IsUnlimited && count > Value;
    }



    /// <inheritdoc/>
    public override string ToString()
    {
        return IsUnlimited ? "unlimited" : Value!.Value.ToString(CultureInfo.InvariantCulture);
    }
}
