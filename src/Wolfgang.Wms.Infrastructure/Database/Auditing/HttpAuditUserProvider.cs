// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Wolfgang.AuditTrail;

namespace Wolfgang.Wms.Infrastructure.Database.Auditing;

/// <summary>
/// Who an audited change is attributed to in a host (E6.4): <c>user_id</c> is the service identity (the
/// application name: the API, the worker), <c>on_behalf_of_user_id</c> the signed-in user of the current
/// request, when there is one (null for jobs and, until E9, for anonymous callers).
/// </summary>
public sealed class HttpAuditUserProvider : IAuditUserProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _serviceIdentity;



    /// <summary>
    /// Creates the provider; without a host environment (bare containers in tools and tests) the service
    /// identity is <see cref="WmsAuditing.SystemIdentity"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="httpContextAccessor"/> is null.</exception>
    public HttpAuditUserProvider(IHttpContextAccessor httpContextAccessor, IHostEnvironment? environment = null)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _serviceIdentity = string.IsNullOrWhiteSpace(environment?.ApplicationName) ? WmsAuditing.SystemIdentity : environment.ApplicationName;
    }



    /// <inheritdoc/>
    public AuditUser GetCurrentUser()
    {
        var name = _httpContextAccessor.HttpContext?.User.Identity?.Name;
        return new AuditUser(_serviceIdentity, string.IsNullOrEmpty(name) ? null : name);
    }
}
