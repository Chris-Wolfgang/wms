// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.RegularExpressions;

namespace Wolfgang.Wms.Domain.Identifiers;

/// <summary>
/// A compiled identifier format (E3.8): a mask or a .NET regular expression, evaluated with
/// <see cref="RegexOptions.NonBacktracking"/> where the pattern allows it and a match timeout otherwise, so
/// a bad pattern can never stall intake or a scan. Anchored to the whole value.
/// </summary>
public sealed class IdentifierFormat
{
    /// <summary>
    /// Upper bound on one match when the pattern needs the backtracking engine.
    /// </summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private readonly Regex _regex;



    private IdentifierFormat(FormatKind kind, string source, Regex regex)
    {
        Kind = kind;
        Source = source;
        _regex = regex;
    }



    /// <summary>
    /// Whether <see cref="Source"/> is a mask or a regular expression.
    /// </summary>
    public FormatKind Kind { get; }



    /// <summary>
    /// The format as the administrator wrote it.
    /// </summary>
    public string Source { get; }



    /// <summary>
    /// The regular expression actually evaluated (the compiled mask, or the anchored regex); shown in the UI.
    /// </summary>
    public string Pattern => _regex.ToString();



    /// <summary>
    /// True when the engine could use <see cref="RegexOptions.NonBacktracking"/> (linear time guaranteed).
    /// </summary>
    public bool IsNonBacktracking => (_regex.Options & RegexOptions.NonBacktracking) != 0;



    /// <summary>
    /// Compiles a format.
    /// </summary>
    /// <exception cref="ArgumentException">The mask or the regular expression is invalid; the message says why.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a known <see cref="FormatKind"/>.</exception>
    public static IdentifierFormat Create(FormatKind kind, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var pattern = kind switch
        {
            FormatKind.Mask => MaskCompiler.ToRegex(source),
            FormatKind.Regex => Anchor(source),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown format kind."),
        };

        try
        {
            return new IdentifierFormat(kind, source, Compile(pattern));
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException($"Format '{source}' is not a valid regular expression: {exception.Message}", nameof(source), exception);
        }
    }



    /// <summary>
    /// Tries to compile a format; false with a reason when it is invalid.
    /// </summary>
    public static bool TryCreate(FormatKind kind, string? source, out IdentifierFormat? format, out string? error)
    {
        format = null;
        error = null;
        if (string.IsNullOrWhiteSpace(source))
        {
            error = "A format is required.";
            return false;
        }

        try
        {
            format = Create(kind, source);
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }



    /// <summary>
    /// True when the whole value matches. A timeout on the backtracking engine counts as no match.
    /// </summary>
    public bool IsMatch(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            return _regex.IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }



    /// <summary>
    /// <c>^(?:body)$</c>: the administrator's regex always matches the whole value, whether or not they anchored it.
    /// </summary>
    private static string Anchor(string regex)
    {
        var body = regex.AsSpan();
        if (body.StartsWith("^", StringComparison.Ordinal))
        {
            body = body[1..];
        }

        if (body.EndsWith("$", StringComparison.Ordinal) && !body.EndsWith("\\$", StringComparison.Ordinal))
        {
            body = body[..^1];
        }

        return "^(?:" + body.ToString() + ")$";
    }



    private static Regex Compile(string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, MatchTimeout);
        }
        catch (NotSupportedException)
        {
            // Backreferences, lookarounds and atomic groups need the backtracking engine; the timeout bounds it.
            return new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout);
        }
    }
}
