// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Wolfgang.Wms.Logging;

/// <summary>
/// Redacts sensitive values at every level (E12.2): a property whose name says it is a secret (password,
/// secret, connection string, PIN, license key, token, API key, authorization) becomes <c>***</c>; inside
/// any string value, an encrypted <c>enc:v1:</c> payload, a connection-string password and a bearer
/// token are masked. Applies to every event, whatever sink it goes to.
/// </summary>
public sealed partial class RedactingEnricher : ILogEventEnricher
{
    /// <summary>
    /// What a redacted value shows.
    /// </summary>
    public const string Mask = "***";



    private static readonly string[] SensitiveNames = ["password", "passwd", "pwd", "secret", "connectionstring", "pin", "licensekey", "license_key", "token", "apikey", "api_key", "authorization"];



    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        foreach (var (name, value) in logEvent.Properties.ToList())
        {
            if (IsSensitiveName(name))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Mask)));
            }
            else if (value is ScalarValue { Value: string text } && MaskText(text) is { } masked && !string.Equals(masked, text, StringComparison.Ordinal))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(masked)));
            }
        }
    }



    /// <summary>
    /// True when a property name says its value is a secret.
    /// </summary>
    public static bool IsSensitiveName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var lowered = name.ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal);
        return SensitiveNames.Any(s => lowered.Contains(s.Replace("_", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal));
    }



    /// <summary>
    /// The text with encrypted payloads, connection-string passwords and bearer tokens masked.
    /// </summary>
    public static string MaskText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var masked = ProtectedPayload().Replace(text, "enc:v1:" + Mask);
        masked = ConnectionStringPassword().Replace(masked, "${key}=" + Mask);
        return BearerToken().Replace(masked, "Bearer " + Mask);
    }



    [GeneratedRegex(@"enc:v1:[A-Za-z0-9+/=_\-\.]+")]
    private static partial Regex ProtectedPayload();



    [GeneratedRegex(@"(?<key>\b(?:password|pwd|pass)\b)\s*=\s*[^;'""\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringPassword();



    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-\._~+/]+=*", RegexOptions.IgnoreCase)]
    private static partial Regex BearerToken();
}
