// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// The migration operations behind <c>wms-migrate</c> and the startup check (E4): status, apply to a target
/// in either direction one migration at a time (so a failure names the migration), refuse a data-losing
/// downgrade without confirmation, and generate idempotent provider-specific scripts with no connection.
/// </summary>
public sealed class MigrationRunner
{
    /// <summary>
    /// The target that means "no migrations applied" (an empty schema).
    /// </summary>
    public const string Empty = "0";

    private readonly WmsDbContext _context;



    /// <summary>
    /// Creates the runner over a context configured for the installation's provider.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public MigrationRunner(WmsDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }



    /// <summary>
    /// Applied and pending migrations and what this build expects (E4.3).
    /// </summary>
    public async Task<MigrationStatus> StatusAsync(CancellationToken cancellationToken)
    {
        var shipped = Shipped();
        IReadOnlyList<string> applied;
        try
        {
            applied = (await _context.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
        }
        catch (DbException)
        {
            return new MigrationStatus(Reachable: false, Applied: [], Pending: shipped, Expected: Last(shipped));
        }

        return new MigrationStatus(Reachable: true, applied, shipped.Except(applied, StringComparer.Ordinal).ToList(), Last(shipped))
        {
            Unknown = applied.Except(shipped, StringComparer.Ordinal).ToList(),
        };
    }



    /// <summary>
    /// Moves the schema to <paramref name="target"/> (a migration id, name, timestamp prefix, <see cref="Empty"/>,
    /// or null for the latest), one migration at a time. A downgrade whose reverted migrations drop tables,
    /// columns, schemas or rows runs only with <paramref name="confirmDataLoss"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="target"/> names no shipped migration.</exception>
    public async Task<MigrationResult> ApplyAsync(string? target, bool confirmDataLoss, CancellationToken cancellationToken)
    {
        var shipped = Shipped();
        var status = await StatusAsync(cancellationToken).ConfigureAwait(false);
        var current = status.Current;
        var to = Resolve(shipped, target);
        var currentIndex = current is null ? -1 : shipped.IndexOf(current);
        var targetIndex = to is null ? -1 : shipped.IndexOf(to);

        if (targetIndex == currentIndex)
        {
            return new MigrationResult(Direction: MigrationDirection.None, From: current, To: to, Steps: [], FailedMigration: null, Error: null, DestructiveSteps: []);
        }

        var migrator = _context.GetService<IMigrator>();
        if (targetIndex > currentIndex)
        {
            var steps = shipped.Skip(currentIndex + 1).Take(targetIndex - currentIndex).ToList();
            return await RunAsync(migrator, MigrationDirection.Up, current, to, steps, steps, [], cancellationToken).ConfigureAwait(false);
        }

        var reverted = shipped.Skip(targetIndex + 1).Take(currentIndex - targetIndex).Reverse().ToList();
        var destructive = reverted.SelectMany(id => DestructiveOperationsIn(Load(id)).Select(step => id + ": " + step)).ToList();
        if (destructive.Count > 0 && !confirmDataLoss)
        {
            return new MigrationResult(Direction: MigrationDirection.Down, From: current, To: to, Steps: [], FailedMigration: null, Error: null, DestructiveSteps: destructive);
        }

        // Reverting migration N means migrating to N-1 (or to Empty for the first).
        var targets = reverted.Select(id => shipped.IndexOf(id) == 0 ? Empty : shipped[shipped.IndexOf(id) - 1]).ToList();
        return await RunAsync(migrator, MigrationDirection.Down, current, to, reverted, targets, destructive, cancellationToken).ConfigureAwait(false);
    }



    /// <summary>
    /// An idempotent, provider-specific SQL script from <paramref name="from"/> (null or <see cref="Empty"/>:
    /// an empty schema) to <paramref name="to"/> (null: the latest), with no database connection (E4.2). A
    /// script that moves down lists the data-losing steps as a header comment (E4.6).
    /// </summary>
    public string Script(string? from, string? to)
    {
        var shipped = Shipped();
        var fromId = Resolve(shipped, from ?? Empty);
        var toId = Resolve(shipped, to);
        var fromIndex = fromId is null ? -1 : shipped.IndexOf(fromId);
        var toIndex = toId is null ? -1 : shipped.IndexOf(toId);
        var script = _context.GetService<IMigrator>().GenerateScript(fromId ?? Empty, toId ?? Empty, MigrationsSqlGenerationOptions.Idempotent);
        if (toIndex >= fromIndex)
        {
            return script;
        }

        var destructive = shipped.Skip(toIndex + 1).Take(fromIndex - toIndex).Reverse()
            .SelectMany(id => DestructiveOperationsIn(Load(id)).Select(step => "-- DATA LOSS " + id + ": " + step))
            .ToList();
        var header = "-- Downgrade from " + fromId + " to " + (toId ?? Empty) + (destructive.Count == 0 ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, destructive));
        return header + Environment.NewLine + script;
    }



    /// <summary>
    /// The Down operations of a migration that lose data: dropped tables, columns and schemas, deleted rows.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="migration"/> is null.</exception>
    public static IReadOnlyList<string> DestructiveOperationsIn(Migration migration)
    {
        ArgumentNullException.ThrowIfNull(migration);

        return migration.DownOperations
            .Select(operation => operation switch
            {
                DropTableOperation drop => "drop table " + Qualified(drop.Schema, drop.Name),
                DropColumnOperation drop => "drop column " + Qualified(drop.Schema, drop.Table) + "." + drop.Name,
                DropSchemaOperation drop => "drop schema " + drop.Name,
                DeleteDataOperation delete => "delete rows from " + Qualified(delete.Schema, delete.Table),
                _ => null,
            })
            .Where(step => step is not null)
            .Select(step => step!)
            .ToList();
    }



    /// <summary>
    /// The shipped migration matching <paramref name="target"/>: exact id, name after the timestamp, or
    /// timestamp prefix; <see cref="Empty"/> resolves to null; null means the latest.
    /// </summary>
    /// <exception cref="ArgumentException">No shipped migration matches.</exception>
    public static string? Resolve(IReadOnlyList<string> shipped, string? target)
    {
        ArgumentNullException.ThrowIfNull(shipped);

        if (target is null || string.Equals(target, "latest", StringComparison.OrdinalIgnoreCase))
        {
            return Last(shipped);
        }

        if (string.Equals(target, Empty, StringComparison.Ordinal))
        {
            return null;
        }

        var match = shipped.FirstOrDefault(id => string.Equals(id, target, StringComparison.Ordinal))
            ?? shipped.FirstOrDefault(id => id.EndsWith("_" + target, StringComparison.Ordinal))
            ?? shipped.FirstOrDefault(id => id.StartsWith(target, StringComparison.Ordinal));
        return match ?? throw new ArgumentException($"'{target}' is not a shipped migration. Known: {string.Join(", ", shipped)}.", nameof(target));
    }



    private async Task<MigrationResult> RunAsync(IMigrator migrator, MigrationDirection direction, string? from, string? to, IReadOnlyList<string> steps, IReadOnlyList<string> targets, IReadOnlyList<string> destructive, CancellationToken cancellationToken)
    {
        var done = new List<string>();
        for (var i = 0; i < steps.Count; i++)
        {
            try
            {
                await migrator.MigrateAsync(targets[i], cancellationToken).ConfigureAwait(false);
                done.Add(steps[i]);
            }
            catch (Exception exception) when (exception is DbException or InvalidOperationException)
            {
                return new MigrationResult(Direction: direction, From: from, To: to, Steps: done, FailedMigration: steps[i], Error: exception.Message, DestructiveSteps: destructive);
            }
        }

        return new MigrationResult(Direction: direction, From: from, To: to, Steps: done, FailedMigration: null, Error: null, DestructiveSteps: destructive);
    }



    private List<string> Shipped()
    {
        return _context.Database.GetMigrations().ToList();
    }



    private Migration Load(string id)
    {
        var assembly = _context.GetService<IMigrationsAssembly>();
        return assembly.CreateMigration(assembly.Migrations[id], _context.Database.ProviderName ?? throw new InvalidOperationException("The context has no database provider."));
    }



    private static string? Last(IReadOnlyList<string> migrations)
    {
        return migrations.Count == 0 ? null : migrations[^1];
    }



    private static string Qualified(string? schema, string name)
    {
        return string.IsNullOrEmpty(schema) ? name : string.Create(CultureInfo.InvariantCulture, $"{schema}.{name}");
    }
}
