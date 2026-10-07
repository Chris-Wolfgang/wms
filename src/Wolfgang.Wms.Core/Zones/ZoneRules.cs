// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Zones;

/// <summary>
/// What a zone draft must satisfy (E16.2), checked the same way by the store and by the console.
/// </summary>
public static class ZoneRules
{
    /// <summary>Longest code.</summary>
    public const int CodeLength = 32;

    /// <summary>Longest name.</summary>
    public const int NameLength = 128;

    /// <summary>Longest walk-order prefix.</summary>
    public const int WalkOrderPrefixLength = 16;

    /// <summary>Longest restocking bin or returns container label.</summary>
    public const int BinLength = 64;

    /// <summary>Most resolvers one zone can name.</summary>
    public const int MaxResolvers = 64;



    /// <summary>
    /// Null when <paramref name="draft"/> is acceptable, else the first reason it is not.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
    public static string? Validate(ZoneDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return Code(draft.Code)
            ?? Text("name", draft.Name, required: true, NameLength)
            ?? Text("walkOrderPrefix", draft.WalkOrderPrefix, required: false, WalkOrderPrefixLength)
            ?? Type(draft)
            ?? Resolution(draft);
    }



    /// <summary>
    /// The form codes are compared in: trimmed, upper-case invariant.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public static string Normalize(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return code.Trim().ToUpperInvariant();
    }



    private static string? Code(string? code)
    {
        if (Text("code", code, required: true, CodeLength) is { } reason)
        {
            return reason;
        }

        return code!.Trim().All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? null : "code may contain letters, digits, '-' and '_' only.";
    }



    private static string? Type(ZoneDraft draft)
    {
        if (!Enum.IsDefined(draft.Type))
        {
            return "type must be Pick, Bulk or Resolution.";
        }

        return draft.IsRejectLane && draft.Type != ZoneType.Pick ? "isRejectLane applies to pick zones only." : null;
    }



    private static string? Resolution(ZoneDraft draft)
    {
        if (draft.Type != ZoneType.Resolution)
        {
            return draft.Resolution is null ? null : "resolution applies to resolution zones only.";
        }

        if (draft.Resolution is not { } resolution)
        {
            return "resolution is required for a resolution zone.";
        }

        return Text("resolution.restockingBin", resolution.RestockingBin, required: false, BinLength)
            ?? Text("resolution.returnsContainer", resolution.ReturnsContainer, required: false, BinLength)
            ?? Resolvers(resolution.ResolverUserIds);
    }



    private static string? Resolvers(IReadOnlyList<long>? ids)
    {
        if (ids is null)
        {
            return "resolution.resolverUserIds is required (an empty list for no assigned resolvers).";
        }

        if (ids.Count > MaxResolvers)
        {
            return $"resolution.resolverUserIds may name at most {MaxResolvers} users.";
        }

        if (ids.Any(id => id <= 0))
        {
            return "resolution.resolverUserIds must be user ids.";
        }

        return ids.Distinct().Count() == ids.Count ? null : "resolution.resolverUserIds must not repeat a user.";
    }



    private static string? Text(string field, string? value, bool required, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return required ? $"{field} is required." : null;
        }

        return value.Trim().Length <= maxLength ? null : $"{field} must be at most {maxLength} characters.";
    }
}
