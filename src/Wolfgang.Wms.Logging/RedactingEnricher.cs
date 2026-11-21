// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Wolfgang.Wms.Logging;

/// <summary>
/// Redacts sensitive values at every level (E12.2): a property whose name says it is a secret (password,
/// secret, connection string, PIN, license key, token, API key, authorization) becomes <c>***</c>; inside
/// any string value, an encrypted <c>enc:v1:</c> payload, a connection-string password (quoted or not) and a
/// bearer token are masked. Destructured values are walked to any depth: a structure's property, a
/// dictionary's entry and a sequence's element are treated the same way as a top-level property. Applies to
/// every event, whatever sink it goes to.
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
            var redacted = Redact(name, value);
            if (!ReferenceEquals(redacted, value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, redacted));
            }
        }
    }



    /// <summary>
    /// The value with every secret masked, walking structures, dictionaries and sequences; the same instance
    /// when nothing in it needed masking.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static LogEventPropertyValue Redact(string? name, LogEventPropertyValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (IsSensitiveName(name))
        {
            return new ScalarValue(Mask);
        }

        return value switch
        {
            ScalarValue { Value: string text } when MaskText(text) is var masked && !string.Equals(masked, text, StringComparison.Ordinal) => new ScalarValue(masked),
            StructureValue structure => RedactStructure(structure),
            SequenceValue sequence => RedactSequence(sequence),
            DictionaryValue dictionary => RedactDictionary(dictionary),
            _ => value,
        };
    }



    private static LogEventPropertyValue RedactStructure(StructureValue structure)
    {
        var changed = false;
        var properties = new List<LogEventProperty>(structure.Properties.Count);
        foreach (var property in structure.Properties)
        {
            var redacted = Redact(property.Name, property.Value);
            changed |= !ReferenceEquals(redacted, property.Value);
            properties.Add(ReferenceEquals(redacted, property.Value) ? property : new LogEventProperty(property.Name, redacted));
        }

        return changed ? new StructureValue(properties, structure.TypeTag) : structure;
    }



    private static LogEventPropertyValue RedactSequence(SequenceValue sequence)
    {
        var changed = false;
        var elements = new List<LogEventPropertyValue>(sequence.Elements.Count);
        foreach (var element in sequence.Elements)
        {
            var redacted = Redact(name: null, element);
            changed |= !ReferenceEquals(redacted, element);
            elements.Add(redacted);
        }

        return changed ? new SequenceValue(elements) : sequence;
    }



    private static LogEventPropertyValue RedactDictionary(DictionaryValue dictionary)
    {
        var changed = false;
        var entries = new List<KeyValuePair<ScalarValue, LogEventPropertyValue>>(dictionary.Elements.Count);
        foreach (var (key, entry) in dictionary.Elements)
        {
            var redacted = Redact(key.Value as string, entry);
            changed |= !ReferenceEquals(redacted, entry);
            entries.Add(new KeyValuePair<ScalarValue, LogEventPropertyValue>(key, redacted));
        }

        return changed ? new DictionaryValue(entries) : dictionary;
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

        var lowered = Normalized(name);   // Api-Key, api_key and ApiKey all read as apikey
        return SensitiveNames.Any(s => lowered.Contains(Normalized(s), StringComparison.Ordinal));
    }



    private static string Normalized(string name)
    {
        return name.ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
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



    // A value may be double-quoted ("" escapes a quote), single-quoted ('' escapes) or bare; the whole value is replaced.
    [GeneratedRegex(@"(?<key>\b(?:password|pwd|pass)\b)\s*=\s*(?:""(?:[^""]|"""")*""|'(?:[^']|'')*'|[^;\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringPassword();



    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-\._~+/]+=*", RegexOptions.IgnoreCase)]
    private static partial Regex BearerToken();
}
