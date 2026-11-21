// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The body of <c>PUT /system/license/keys</c> (E79.3): the signed document as issued.
/// </summary>
/// <param name="Key">The pasted key document.</param>
public sealed record InstallLicenseKeyRequest(string Key);
