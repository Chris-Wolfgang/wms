// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// The result of a provider's health check (E11.0).
/// </summary>
/// <param name="Healthy">True when the provider can sign people in.</param>
/// <param name="Detail">What was checked, or why it failed; never a secret.</param>
public sealed record AuthProviderHealth(bool Healthy, string Detail);
