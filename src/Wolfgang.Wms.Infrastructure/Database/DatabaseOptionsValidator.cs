// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Options;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// Runs <see cref="DatabaseOptions.Validate"/> at startup (<c>ValidateOnStart</c>) so a misconfigured
/// installation fails before it serves a request, with the setting named in the message.
/// </summary>
public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = options.Validate();
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
