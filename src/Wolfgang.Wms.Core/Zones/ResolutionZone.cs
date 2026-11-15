// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Zones;

/// <summary>
/// The properties only a <see cref="ZoneType.Resolution"/> zone carries (E16.2): where resolved stock goes,
/// who resolves, which exceptions it accepts, and whether it is a physical lane or a virtual queue.
/// </summary>
/// <param name="RestockingBin">The bin (a location barcode or label, E17.1) resolved stock is restocked to; null when none.</param>
/// <param name="ReturnsContainer">The container returns are collected in; null when none.</param>
/// <param name="ResolverUserIds">The users assigned to resolve here; empty when anyone with the permission may.</param>
/// <param name="AcceptsWeightFailures">Whether totes that failed the weight check are routed here.</param>
/// <param name="AcceptsShorts">Whether short picks are resolved here.</param>
/// <param name="AcceptsAdjustments">Whether quantity adjustments are resolved here.</param>
/// <param name="AcceptsMisdirects">Whether misdirected totes are resolved here.</param>
/// <param name="IsVirtualQueue">True for a virtual queue (work items, no conveyor lane); false for a physical lane.</param>
public sealed record ResolutionZone
(
    string? RestockingBin,
    string? ReturnsContainer,
    IReadOnlyList<long> ResolverUserIds,
    bool AcceptsWeightFailures,
    bool AcceptsShorts,
    bool AcceptsAdjustments,
    bool AcceptsMisdirects,
    bool IsVirtualQueue
);
