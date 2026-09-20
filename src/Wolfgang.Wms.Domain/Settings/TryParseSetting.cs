// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Wms.Domain.Settings;

/// <summary>
/// Parses stored text.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="text">The stored text.</param>
/// <param name="value">The parsed value when the text is valid.</param>
public delegate bool TryParseSetting<T>(string text, [MaybeNullWhen(false)] out T value);
