// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Configuration;
using Wolfgang.Wms.Migrate;

namespace Wolfgang.Wms.UnitTests.Migrate;

public sealed class MigrateCommandLineTests
{
    [Fact]
    public void No_arguments_means_apply_latest()
    {
        var command = MigrateCommandLine.Parse([]);

        Assert.Null(command.Error);
        Assert.False(command.Status);
        Assert.False(command.Script);
        Assert.Null(command.To);
        Assert.False(command.ConfirmDataLoss);
    }



    [Fact]
    public void A_leading_migrate_word_is_ignored_and_flags_are_case_insensitive()
    {
        var command = MigrateCommandLine.Parse(["migrate", "--STATUS", "--Provider", "PostgreSql", "--trust-server-certificate"]);

        Assert.Null(command.Error);
        Assert.True(command.Status);
        Assert.Equal("PostgreSql", command.Provider);
        Assert.True(command.TrustServerCertificate);
    }



    [Fact]
    public void Script_takes_from_to_and_output()
    {
        var command = MigrateCommandLine.Parse(["--script", "--from", "0", "--to", "Initial", "--output", "up.sql", "--connection-string", "Server=x"]);

        Assert.Null(command.Error);
        Assert.True(command.Script);
        Assert.Equal("0", command.From);
        Assert.Equal("Initial", command.To);
        Assert.Equal("up.sql", command.Output);
        Assert.Equal("Server=x", command.ConnectionString);
    }



    [Theory]
    [InlineData(new[] { "--to" }, "--to needs a value.")]
    [InlineData(new[] { "--to", "--status" }, "--to needs a value.")]
    [InlineData(new[] { "--up" }, "Unknown argument '--up'.")]
    [InlineData(new[] { "--status", "--script" }, "--status and --script cannot be combined.")]
    [InlineData(new[] { "--from", "0" }, "--from applies to --script only.")]
    public void Usage_errors_are_named(string[] args, string expected)
    {
        Assert.Equal(expected, MigrateCommandLine.Parse(args).Error);
    }



    [Fact]
    public void Help_and_confirm_data_loss_parse()
    {
        Assert.True(MigrateCommandLine.Parse(["-h"]).Help);
        Assert.True(MigrateCommandLine.Parse(["--help"]).Help);
        Assert.True(MigrateCommandLine.Parse(["--to", "0", "--confirm-data-loss"]).ConfirmDataLoss);
        Assert.Contains("--confirm-data-loss", MigrateCommandLine.Usage, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => MigrateCommandLine.Parse(null!));
    }



    [Fact]
    public void Flags_override_the_configured_database_section()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Wms:Database:Provider"] = "SqlServer",
                ["Wms:Database:ConnectionString"] = "Server=configured",
            })
            .Build();

        var configured = MigrateProgram.Options(MigrateCommandLine.Parse([]), configuration);
        var overridden = MigrateProgram.Options(MigrateCommandLine.Parse(["--provider", "PostgreSql", "--connection-string", "Host=flag", "--trust-server-certificate"]), configuration);

        Assert.Equal("SqlServer", configured.Provider);
        Assert.Equal("Server=configured", configured.ConnectionString);
        Assert.Equal("PostgreSql", overridden.Provider);
        Assert.Equal("Host=flag", overridden.ConnectionString);
        Assert.True(overridden.TrustServerCertificate);
        Assert.Throws<ArgumentNullException>(() => MigrateProgram.Options(null!, configuration));
        Assert.Throws<ArgumentNullException>(() => MigrateProgram.Options(MigrateCommandLine.Parse([]), null!));
    }
}
