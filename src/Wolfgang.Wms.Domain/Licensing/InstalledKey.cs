// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// One key of the install with the part it plays (E79.11), for the license page.
/// </summary>
/// <param name="Key">The key content (null when the document could not be read).</param>
/// <param name="KeyId">The key id (from the document when readable, else the stored slot).</param>
/// <param name="Status">Its status.</param>
/// <param name="Reason">Why, for a status other than Active.</param>
public sealed record InstalledKey(LicenseKey? Key, string KeyId, KeyStatus Status, string? Reason);
