// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Web.Shared;

/// <summary>
/// The keyboard-wedge rule behind <c>ScanListener</c> (E82.4), kept as a plain class so it is unit-tested
/// without rendering: a tethered scanner types the barcode and sends Enter; the text before Enter, trimmed,
/// is the scan; blank scans are ignored; the buffer clears after every Enter.
/// </summary>
public sealed class ScanBuffer
{
    /// <summary>
    /// The key a scanner sends at the end of a barcode.
    /// </summary>
    public const string CompleteKey = "Enter";



    /// <summary>
    /// The text typed so far.
    /// </summary>
    public string Text { get; private set; } = string.Empty;



    /// <summary>
    /// Replaces the buffered text with the field's current value.
    /// </summary>
    public void Update(string? text)
    {
        Text = text ?? string.Empty;
    }



    /// <summary>
    /// Handles a key: on <see cref="CompleteKey"/> the buffer is cleared and, when the trimmed text is not
    /// blank, it is returned as the scan. Any other key leaves the buffer alone.
    /// </summary>
    /// <param name="key">The key name from the keyboard event.</param>
    /// <param name="scanned">The completed scan, or null.</param>
    /// <returns>True when a non-blank scan completed.</returns>
    public bool TryComplete(string? key, out string? scanned)
    {
        scanned = null;
        if (!string.Equals(key, CompleteKey, StringComparison.Ordinal))
        {
            return false;
        }

        var text = Text.Trim();
        Text = string.Empty;
        if (text.Length == 0)
        {
            return false;
        }

        scanned = text;
        return true;
    }
}
