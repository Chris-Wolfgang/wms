// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.AotSmoke;

/// <summary>
/// Assertions for the smoke checks. A failure throws <see cref="SmokeFailureException"/>, which the runner reports.
/// </summary>
internal static class Smoke
{
    /// <summary>
    /// Fails unless <paramref name="actual"/> holds exactly <paramref name="expected"/>, in order.
    /// </summary>
    public static void SequenceEqual(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        var expectedList = expected.ToList();
        var actualList = actual.ToList();
        if (!expectedList.SequenceEqual(actualList, StringComparer.Ordinal))
        {
            throw new SmokeFailureException($"expected [{string.Join(", ", expectedList)}] but got [{string.Join(", ", actualList)}]");
        }
    }



    /// <summary>
    /// Fails unless <paramref name="action"/> throws <typeparamref name="TException"/> with a message containing
    /// <paramref name="messageFragment"/>.
    /// </summary>
    public static void Throws<TException>(Action action, string messageFragment)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception) when (exception.Message.Contains(messageFragment, StringComparison.Ordinal))
        {
            return;
        }

        throw new SmokeFailureException($"expected {typeof(TException).Name} mentioning '{messageFragment}'");
    }
}



/// <summary>
/// A smoke check found a result that differs from the expected one.
/// </summary>
internal sealed class SmokeFailureException : Exception
{
    public SmokeFailureException(string message)
        : base(message)
    {
    }
}
