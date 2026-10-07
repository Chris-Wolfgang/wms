// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;
using Wolfgang.Wms.Client.Generated;

namespace Wolfgang.Wms.Web.Shared;

/// <summary>
/// The notice source over the API (E9.3): <c>GET /auth/local/status</c> through the generated client. A
/// window shows only while the API says local sign-in is open because of one; an API that cannot be reached
/// or answers oddly yields no banner (and a Warning), never a broken workspace.
/// </summary>
public sealed partial class ApiLocalLoginNotice : ILocalLoginNotice
{
    private readonly WmsClient _client;
    private readonly ILogger<ApiLocalLoginNotice> _logger;



    /// <summary>
    /// Creates the source over <paramref name="client"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public ApiLocalLoginNotice(WmsClient client, ILogger<ApiLocalLoginNotice> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <inheritdoc/>
    public async Task<LocalLoginNotice?> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var status = await _client.Api.V0.Auth.Local.Status.GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return status is { LocalLoginOpen: true, UnlockedUntil: { } until } ? new LocalLoginNotice(until) : null;
        }
        catch (ApiException exception)
        {
            LogUnavailable(_logger, exception);
            return null;
        }
        catch (HttpRequestException exception)
        {
            LogUnavailable(_logger, exception);
            return null;
        }
    }



    [LoggerMessage(Level = LogLevel.Warning, Message = "The local sign-in status could not be read from the API; no break-glass banner is shown.")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);
}
