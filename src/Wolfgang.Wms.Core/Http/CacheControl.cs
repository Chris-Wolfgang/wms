// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Http;

/// <summary>
/// The two <c>Cache-Control</c> policies the product uses (E1.12, ADR 0003). There is no third: API data is
/// always revalidated, static assets are immutable by name.
/// </summary>
public static class CacheControl
{
    /// <summary>
    /// API responses: the client may keep a copy but must revalidate with <c>If-None-Match</c> every time.
    /// Never <c>no-store</c> (that defeats the 304 path) and never a <c>max-age</c> (no time-based staleness).
    /// </summary>
    public const string Api = "private, no-cache";



    /// <summary>
    /// Static assets published under content-hashed names: cache for a year and never revalidate; a new
    /// build changes the name, not the content.
    /// </summary>
    public const string StaticAsset = "public, max-age=31536000, immutable";
}
