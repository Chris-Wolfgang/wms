// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Data.SqlClient;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// The <c>Wms:Database</c> configuration section (E2.1): provider, connection string and the SQL Server
/// <c>TrustServerCertificate</c> switch, set in <c>appsettings</c> or as environment variables
/// (<c>Wms__Database__Provider</c>). Validated at startup; an unknown provider fails with a message that names
/// the setting and the accepted values.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>
    /// Configuration section path.
    /// </summary>
    public const string SectionName = "Wms:Database";



    /// <summary>
    /// <c>SqlServer</c>, <c>PostgreSql</c>, or <c>None</c> (bootstrap only). Case-insensitive.
    /// </summary>
    public string Provider { get; set; } = nameof(DatabaseProvider.None);



    /// <summary>
    /// The provider's connection string; required unless the provider is <c>None</c>.
    /// </summary>
    public string? ConnectionString { get; set; }



    /// <summary>
    /// SQL Server only: trust the server certificate without validating its chain. Use for Express or a
    /// self-signed certificate on a private network; never on a shared network.
    /// </summary>
    public bool TrustServerCertificate { get; set; }



    /// <summary>
    /// The parsed provider, or null when <see cref="Provider"/> is not one of the accepted names. Names only:
    /// a numeric value such as <c>1</c> is rejected, though <see cref="Enum.TryParse{TEnum}(string?, bool, out TEnum)"/>
    /// would accept it.
    /// </summary>
    public DatabaseProvider? ParsedProvider =>
        Enum.GetValues<DatabaseProvider>()
            .Cast<DatabaseProvider?>()
            .SingleOrDefault(p => string.Equals(p.ToString(), Provider, StringComparison.OrdinalIgnoreCase));



    /// <summary>
    /// The accepted <see cref="Provider"/> names as the validation message lists them (<c>None, SqlServer or
    /// PostgreSql</c>), taken from <see cref="DatabaseProvider"/> so a new engine is listed without editing the
    /// message.
    /// </summary>
    public static string AcceptedProviders { get; } = ListOf(Enum.GetNames<DatabaseProvider>());



    /// <summary>
    /// The connection string the provider receives: for SQL Server with <see cref="TrustServerCertificate"/>
    /// the switch is applied to the string and encryption is required (<c>Encrypt=Mandatory</c>, or
    /// <c>Strict</c> when configured), since without certificate validation encryption is the protection that
    /// remains; otherwise the string as configured.
    /// </summary>
    public string EffectiveConnectionString()
    {
        var connectionString = ConnectionString ?? string.Empty;
        if (ParsedProvider == DatabaseProvider.SqlServer && TrustServerCertificate)
        {
            var builder = new SqlConnectionStringBuilder(connectionString) { TrustServerCertificate = true };
            builder.Encrypt = builder.Encrypt.Equals(SqlConnectionEncryptOption.Strict)
                ? SqlConnectionEncryptOption.Strict
                : SqlConnectionEncryptOption.Mandatory;

            return builder.ConnectionString;
        }

        return connectionString;
    }



    /// <summary>
    /// Every problem with the options, each naming the setting; empty when valid.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var provider = ParsedProvider;
        if (provider is null)
        {
            errors.Add($"{SectionName}:Provider must be one of {AcceptedProviders}; got '{Provider}'.");
            return errors;
        }

        if (provider != DatabaseProvider.None && string.IsNullOrWhiteSpace(ConnectionString))
        {
            errors.Add($"{SectionName}:ConnectionString is required when {SectionName}:Provider is {provider}.");
        }

        if (provider != DatabaseProvider.SqlServer && TrustServerCertificate)
        {
            errors.Add($"{SectionName}:TrustServerCertificate applies to SqlServer only; remove it for {provider}.");
        }

        return errors;
    }



    private static string ListOf(string[] names)
    {
        return string.Join(", ", names[..^1]) + " or " + names[^1];
    }
}
