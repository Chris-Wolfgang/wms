// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.External;

/// <summary>
/// What a provider sign-in led to (E11.1).
/// </summary>
public enum ExternalSignInOutcome
{
    /// <summary>The account exists (created on first sign-in), is enabled and verified; its roles follow the group mappings.</summary>
    Success,

    /// <summary>The account is disabled in the console.</summary>
    Disabled,

    /// <summary>The account row does not match its signature (E10.4); it is not honoured.</summary>
    IntegrityFailure,
}
