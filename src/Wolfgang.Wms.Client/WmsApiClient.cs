// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Wolfgang.Wms.Client.Generated;

namespace Wolfgang.Wms.Client;

/// <summary>
/// Builds the generated <see cref="WmsClient"/> over an <see cref="HttpClient"/> (E82.8). The console, the
/// CLI and customer tools all start here; authentication providers (E11) plug into the adapter.
/// </summary>
public static class WmsApiClient
{
    /// <summary>
    /// A client whose requests go to <paramref name="httpClient"/>'s base address (the API root, without the
    /// <c>/api/v0</c> segment: the generated paths carry it).
    /// </summary>
    /// <param name="httpClient">A client with <see cref="HttpClient.BaseAddress"/> set to the API root.</param>
    /// <param name="authentication">The authentication provider; anonymous when null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="httpClient"/> has no base address.</exception>
    public static WmsClient Create(HttpClient httpClient, IAuthenticationProvider? authentication = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (httpClient.BaseAddress is null)
        {
            throw new ArgumentException("The HttpClient needs a BaseAddress: the API root.", nameof(httpClient));
        }

        var adapter = new HttpClientRequestAdapter(authentication ?? new AnonymousAuthenticationProvider(), httpClient: httpClient)
        {
            BaseUrl = httpClient.BaseAddress.ToString().TrimEnd('/'),
        };

        return new WmsClient(adapter);
    }
}
