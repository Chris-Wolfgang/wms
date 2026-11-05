// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json.Serialization;
using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The license page (E79.6, E79.11): the tier and coverage, every feature granted, every limit with its
/// usage, the device totals ("5 included + 10 purchased = 15"), every installed key, and the banners.
/// </summary>
/// <param name="Tier">The tier name.</param>
/// <param name="Organization">The organization the base key binds to; empty on the free tier.</param>
/// <param name="Coverage">Whether this release is covered.</param>
/// <param name="CoveredUntil">The last covered day, or null when perpetual.</param>
/// <param name="ReleaseVersion">This release.</param>
/// <param name="ReleaseDate">Its date, the one coverage is checked against.</param>
/// <param name="Features">The feature names granted.</param>
/// <param name="Limits">Every limit with its usage.</param>
/// <param name="IncludedDevices">Devices the tier includes.</param>
/// <param name="PurchasedDevices">Devices bought through add-ons.</param>
/// <param name="AllowancePercent">The overage allowance in percent.</param>
/// <param name="AllowanceMinimumUnits">The allowance floor in units.</param>
/// <param name="GraceDays">The grace period in days.</param>
/// <param name="UsageWarningPercent">The warning threshold.</param>
/// <param name="Keys">Every installed key.</param>
/// <param name="Banners">What the console shows at the top: lapsed coverage, limits over, warnings.</param>
public sealed record LicenseStatus
(
    string Tier,
    string Organization,
    [property: JsonConverter(typeof(JsonStringEnumConverter<CoverageStatus>))] CoverageStatus Coverage,
    DateOnly? CoveredUntil,
    string ReleaseVersion,
    DateOnly ReleaseDate,
    IReadOnlyList<string> Features,
    IReadOnlyList<LimitUsage> Limits,
    int IncludedDevices,
    int PurchasedDevices,
    int AllowancePercent,
    int AllowanceMinimumUnits,
    int GraceDays,
    int UsageWarningPercent,
    IReadOnlyList<InstalledKeyInfo> Keys,
    IReadOnlyList<string> Banners
);
