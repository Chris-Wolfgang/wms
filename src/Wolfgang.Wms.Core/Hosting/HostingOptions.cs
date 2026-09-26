// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Hosting;

/// <summary>
/// <c>Wms:Hosting</c> (E10.6, E12.5): how the process is fronted. A bootstrap key: the host must know before
/// the first request whether the scheme and client address arrive in <c>X-Forwarded-*</c> headers.
/// </summary>
public sealed class HostingOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public const string SectionName = "Wms:Hosting";



    /// <summary>
    /// The configuration key of the proxy flag, for messages and the bootstrap list.
    /// </summary>
    public const string BehindProxyKey = SectionName + ":BehindProxy";



    /// <summary>
    /// True when a reverse proxy (Caddy, IIS, an ingress) terminates TLS in front of the host and forwards
    /// the original scheme, host and client address in <c>X-Forwarded-Proto</c>, <c>X-Forwarded-Host</c> and
    /// <c>X-Forwarded-For</c>. The proxy must be the only way in and must strip those headers from clients.
    /// </summary>
    public bool BehindProxy { get; set; }



    /// <summary>
    /// E12.5: allow plain HTTP from any address (a lab); by default only the loopback address and the health
    /// probes may use it.
    /// </summary>
    public bool AllowHttp { get; set; }
}
