// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Web.Shared;

/// <summary>
/// Routes each completed scan from the one <c>ScanListener</c> that <c>WorkspaceLayout</c> renders for every
/// workspace (E82.4) to the screen that is taking scans. A screen calls <see cref="Listen"/> (the layout hands the
/// dispatcher down as a cascading value) and disposes the registration when it goes away; the most recent
/// listener gets the scan, so a dialog opened over a screen takes scans until it closes. The device's validation
/// and feedback rules (E40) run in the listener.
/// </summary>
public sealed class ScanDispatcher
{
    private readonly List<Func<string, Task>> _listeners = [];



    /// <summary>
    /// True while at least one screen is taking scans.
    /// </summary>
    public bool HasListener => _listeners.Count > 0;



    /// <summary>
    /// Starts sending scans to <paramref name="listener"/> until the returned registration is disposed.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="listener"/> is null.</exception>
    public IDisposable Listen(Func<string, Task> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _listeners.Add(listener);
        return new Registration(this, listener);
    }



    /// <summary>
    /// Sends <paramref name="scan"/> to the most recent listener.
    /// </summary>
    /// <returns>True when a listener took the scan; false when no screen is taking scans.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scan"/> is null.</exception>
    public async Task<bool> DispatchAsync(string scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        if (_listeners.Count == 0)
        {
            return false;
        }

        await _listeners[^1](scan).ConfigureAwait(false);
        return true;
    }



    private sealed class Registration(ScanDispatcher owner, Func<string, Task> listener) : IDisposable
    {
        public void Dispose()
        {
            owner._listeners.Remove(listener);
        }
    }
}
