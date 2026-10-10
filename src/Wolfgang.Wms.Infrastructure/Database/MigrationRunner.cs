// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
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
        catch (DbException exception)
        {
            // A reachable server with no history table (or no database yet) is not an error: EF's history
            // repository checks that both exist and returns no applied migrations. Only a failure to talk to the
            // server lands here.
            return new MigrationStatus(Reachable: false, Applied: [], Pending: shipped, Expected: Last(shipped))
            {
                Error = exception.Message,
            };
        }

        return new MigrationStatus(Reachable: true, applied, shipped.Except(applied, StringComparer.Ordinal).ToList(), Last(shipped))
        {
            Unknown = applied.Except(shipped, StringComparer.Ordinal).ToList(),
        };
    }



    /// <summary>
    /// Moves the schema to <paramref name="target"/> (a migration id, name, timestamp prefix, <see cref="Empty"/>,
    /// or null for the latest), one migration at a time. A downgrade whose reverted migrations lose data (see
    /// <see cref="DestructiveOperationsIn"/>: dropped tables, columns, schemas or sequences, a restarted sequence,
    /// deleted or updated rows, a narrowed column, raw SQL) runs only with <paramref name="confirmDataLoss"/>. Nothing runs when the
    /// database is unreachable or its schema is newer than this build (<see cref="Refusal"/>).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="target"/> names no shipped migration, or more than one.</exception>
    public async Task<MigrationResult> ApplyAsync(string? target, bool confirmDataLoss, CancellationToken cancellationToken)
    {
        var shipped = Shipped();
        var to = Resolve(shipped, target);
        var status = await StatusAsync(cancellationToken).ConfigureAwait(false);
        var current = status.Current;
        var refusal = Refusal(status);
        if (refusal is not null)
        {
            return new MigrationResult
            (
                Direction: MigrationDirection.None,
                From: current,
                To: to,
                Steps: [],
                FailedMigration: null,
                Error: refusal,
                DestructiveSteps: []
            );
        }

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
    /// script that moves down lists the data-losing steps as a header comment and, when there are any, is
    /// produced only with <paramref name="confirmDataLoss"/> (E4.6), as an applied downgrade is.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="from"/> or <paramref name="to"/> names no shipped
    /// migration, or more than one.</exception>
    public MigrationScript Script(string? from, string? to, bool confirmDataLoss)
    {
        var shipped = Shipped();
        var fromId = Resolve(shipped, from ?? Empty);
        var toId = Resolve(shipped, to);
        var fromIndex = fromId is null ? -1 : shipped.IndexOf(fromId);
        var toIndex = toId is null ? -1 : shipped.IndexOf(toId);
        if (toIndex >= fromIndex)
        {
            var direction = toIndex == fromIndex ? MigrationDirection.None : MigrationDirection.Up;
            return new MigrationScript(direction, Generate(fromId, toId), []);
        }

        var destructive = shipped
            .Skip(toIndex + 1)
            .Take(fromIndex - toIndex)
            .Reverse()
            .SelectMany(id => DestructiveOperationsIn(Load(id)).Select(step => id + ": " + step))
            .ToList();
        if (destructive.Count > 0 && !confirmDataLoss)
        {
            return new MigrationScript(MigrationDirection.Down, Sql: null, destructive);
        }

        var lines = new List<string> { "-- Downgrade from " + fromId + " to " + (toId ?? Empty) };
        lines.AddRange(destructive.Select(step => "-- DATA LOSS " + step));
        lines.Add(Generate(fromId, toId));
        return new MigrationScript(MigrationDirection.Down, string.Join(Environment.NewLine, lines), destructive);
    }



    /// <summary>
    /// Why nothing may be applied to the database <paramref name="status"/> describes, or null when it may:
    /// it cannot be reached (so its schema is unknown, and "already at the target" cannot be claimed), or it
    /// carries migrations this build does not ship (E4.6: a newer schema is never touched by an older build).
    /// The startup check refuses to start for the same reasons with the same message.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="status"/> is null.</exception>
    public static string? Refusal(MigrationStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        if (!status.Reachable)
        {
            return $"{DatabaseOptions.SectionName}: the database cannot be reached; check the connection string and that the server is up. {status.Error}".TrimEnd();
        }

        if (status.SchemaIsNewer)
        {
            return $"The database schema is newer than this build (unknown migrations: {string.Join(", ", status.Unknown)}). Upgrade the application, or restore the backup taken before the upgrade.";
        }

        return null;
    }



    /// <summary>
    /// The Down operations of a migration that may lose data: dropped tables, columns, schemas and sequences, a
    /// restarted sequence (a sequence's current value is state: the row-version sequence is every client's sync
    /// watermark, and a restart discards it as surely as a drop),
    /// deleted or updated rows, a column narrowed to a smaller length, precision or scale, to fewer integral digits
    /// (the scale grows by more than the precision does, so precision minus scale shrinks), to a bounded precision
    /// from an unbounded one, to non-unicode
    /// text, to a type written without its length or precision (the provider's default applies) or to another
    /// type,
    /// and raw SQL (<see cref="MigrationBuilder.Sql"/>), which is not inspected and so is treated as data-losing.
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
                DropSequenceOperation drop => "drop sequence " + Qualified(drop.Schema, drop.Name),
                RestartSequenceOperation restart => "restart sequence " + Qualified(restart.Schema, restart.Name) + " at " + (restart.StartValue?.ToString(CultureInfo.InvariantCulture) ?? "its start value"),
                DeleteDataOperation delete => "delete rows from " + Qualified(delete.Schema, delete.Table),
                UpdateDataOperation update => "update rows in " + Qualified(update.Schema, update.Table),
                AlterColumnOperation alter when Narrowing(alter) is { Length: > 0 } narrowing => "narrow column " + Qualified(alter.Schema, alter.Table) + "." + alter.Name + " (" + narrowing + ")",
                SqlOperation sql => "raw SQL, not inspected: " + Summary(sql.Sql),
                _ => null,
            })
            .Where(step => step is not null)
            .Select(step => step!)
            .ToList();
    }



    /// <summary>
    /// The shipped migration matching <paramref name="target"/>: exact id, else name after the timestamp, else
    /// id prefix; <see cref="Empty"/> resolves to null; null means the latest. A name or prefix matching more
    /// than one migration is rejected rather than guessed, since the target may be a destructive downgrade.
    /// </summary>
    /// <exception cref="ArgumentException">No shipped migration matches, or more than one does.</exception>
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

        if (shipped.Contains(target, StringComparer.Ordinal))
        {
            return target;
        }

        var matches = shipped
            .Where(id => id.EndsWith("_" + target, StringComparison.Ordinal))
            .ToList();
        if (matches.Count == 0)
        {
            matches = shipped
                .Where(id => id.StartsWith(target, StringComparison.Ordinal))
                .ToList();
        }

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new ArgumentException($"'{target}' is not a shipped migration. Known: {string.Join(", ", shipped)}.", nameof(target)),
            _ => throw new ArgumentException($"'{target}' is ambiguous; it matches {string.Join(", ", matches)}. Give the full migration id.", nameof(target)),
        };
    }



    private static async Task<MigrationResult> RunAsync(IMigrator migrator, MigrationDirection direction, string? from, string? to, IReadOnlyList<string> steps, IReadOnlyList<string> targets, IReadOnlyList<string> destructive, CancellationToken cancellationToken)
    {
        var done = new List<string>();
        for (var i = 0; i < steps.Count; i++)
        {
            try
            {
                await migrator.MigrateAsync(targets[i], cancellationToken).ConfigureAwait(false);
                done.Add(steps[i]);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Any failure inside a migration's Up/Down (custom migration code included) names that step.
                return new MigrationResult(Direction: direction, From: from, To: to, Steps: done, FailedMigration: steps[i], Error: exception.Message, DestructiveSteps: destructive);
            }
        }

        return new MigrationResult(Direction: direction, From: from, To: to, Steps: done, FailedMigration: null, Error: null, DestructiveSteps: destructive);
    }



    private string Generate(string? fromId, string? toId)
    {
        return _context
            .GetService<IMigrator>()
            .GenerateScript(fromId ?? Empty, toId ?? Empty, MigrationsSqlGenerationOptions.Idempotent);
    }



    private static string Summary(string sql)
    {
        // One line, so the summary can sit in a "--" comment of a downgrade script header without the rest of
        // the statement escaping the comment.
        var line = string.Join(' ', sql.Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= 80 ? line : line[..80] + "...";
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



    /// <summary>
    /// How the column loses room, as a comma-separated list; empty when it does not. A smaller maximum length,
    /// precision or scale than before, or another store type or CLR type (a conversion the engine may truncate or
    /// refuse) count; a store type that is set on one side only counts too, since the other side is the
    /// provider's inferred type and the migration exists because the two differ. Widening and nullability changes
    /// keep every value and are not flagged.
    /// </summary>
    private static string Narrowing(AlterColumnOperation alter)
    {
        var old = alter.OldColumn;
        var reasons = new List<string>(5);
        var oldType = StoreType.Parse(old.ColumnType);
        var newType = StoreType.Parse(alter.ColumnType);

        // Facets come from the operation when it carries them, else from the store type ("nvarchar(50)",
        // "decimal(9,3)"), which is how EF scaffolds an AlterColumn; "max" and an absent facet mean unbounded.
        var oldLength = old.MaxLength ?? oldType.Length;
        var newLength = alter.MaxLength ?? newType.Length;
        if (newLength is { } length && (oldLength is null || oldLength > length))
        {
            reasons.Add("max length " + (oldLength?.ToString(CultureInfo.InvariantCulture) ?? "unbounded") + " -> " + length.ToString(CultureInfo.InvariantCulture));
        }

        // A type written without the facet it takes gets the provider's default, and the providers disagree:
        // SQL Server reads "varchar" as varchar(1) and "decimal" as decimal(18,0), PostgreSQL reads both as
        // unbounded. This code does not know the provider, so a column whose facet is dropped needs confirmation.
        if (newLength is null && newType.Facetless && StoreType.IsLengthFamily(newType.Family!) && (oldLength is not null || oldType.Unbounded))
        {
            reasons.Add("max length " + (oldLength?.ToString(CultureInfo.InvariantCulture) ?? "unbounded") + " -> provider default");
        }

        NumericNarrowing(alter, old, oldType, newType, reasons);

        // Unicode text made non-unicode (nvarchar -> varchar on SQL Server) loses every character outside the
        // collation's code page; an old column that does not say is unicode, the providers' default.
        if (alter.IsUnicode == false && old.IsUnicode != false)
        {
            reasons.Add("unicode -> non-unicode");
        }

        // Only the type family is a type change; its facets were compared above, so "nvarchar(50)" ->
        // "nvarchar(100)" is a widening, not a conversion. Case and whitespace do not count; the message shows
        // the types as written.
        var familyChanged = (oldType.Family is null) != (newType.Family is null)
            || (oldType.Family is not null && !string.Equals(oldType.Family, newType.Family, StringComparison.Ordinal));
        var clrTypeChanged = old.ClrType is not null && alter.ClrType is not null && old.ClrType != alter.ClrType;
        if (familyChanged || clrTypeChanged)
        {
            reasons.Add("type " + (oldType.Text ?? "inferred for " + old.ClrType?.Name) + " -> " + (newType.Text ?? "inferred for " + alter.ClrType?.Name));
        }

        return string.Join(", ", reasons);
    }



    /// <summary>
    /// Adds the precision, scale and integral-digit reasons for <paramref name="alter"/> to <paramref name="reasons"/>.
    /// </summary>
    private static void NumericNarrowing(AlterColumnOperation alter, ColumnOperation old, StoreType oldType, StoreType newType, List<string> reasons)
    {
        // An old numeric column with no precision known here ("numeric" on PostgreSQL, or facets this
        // migration does not state) may hold more digits than the bounded new precision, so bounding it counts as
        // narrowing, as an unbounded length does above. A known non-numeric old family is a type change instead.
        var oldPrecision = old.Precision ?? oldType.Precision;
        var newPrecision = alter.Precision ?? newType.Precision;
        var oldScale = old.Scale ?? oldType.Scale;
        var newScale = alter.Scale ?? newType.Scale;
        var inferredDecimal = alter.ColumnType is null && alter.ClrType == typeof(decimal);   // no store type written: the provider's default decimal, decimal(18,2) on SQL Server
        var facetlessDecimal = newType.Facetless && StoreType.IsDecimal(newType.Family!);
        if (newPrecision is null && (facetlessDecimal || inferredDecimal) && oldPrecision is { } droppedPrecision)
        {
            // "decimal" with no facets, or none written at all: decimal(18,0) / decimal(18,2) on SQL Server,
            // unbounded on PostgreSQL (see Narrowing). The old facets were known, so this may lose digits.
            reasons.Add("precision " + droppedPrecision.ToString(CultureInfo.InvariantCulture) + " -> provider default");
        }
        else if (newPrecision is { } precision && oldPrecision is null && (oldType.Family is null || StoreType.IsDecimal(oldType.Family)))
        {
            reasons.Add("precision unbounded -> " + precision.ToString(CultureInfo.InvariantCulture));
        }
        else if (newPrecision is { } bounded && oldPrecision is null && oldType.Family is not null && oldType.Length is null && !oldType.Unbounded && string.Equals(oldType.Family, newType.Family, StringComparison.Ordinal))
        {
            // The same family written without its precision had the provider's default, which is the largest it
            // offers (datetime2 is datetime2(7) on SQL Server, timestamp is timestamp(6) on PostgreSQL), so a bound
            // written now is a narrowing.
            reasons.Add("precision provider default -> " + bounded.ToString(CultureInfo.InvariantCulture));
        }
        else if (newPrecision is { } precision2 && oldPrecision is { } fromPrecision && fromPrecision > precision2)
        {
            reasons.Add("precision " + fromPrecision.ToString(CultureInfo.InvariantCulture) + " -> " + precision2.ToString(CultureInfo.InvariantCulture));
        }

        if (newScale is { } scale && oldScale is { } fromScale && fromScale > scale)
        {
            reasons.Add("scale " + fromScale.ToString(CultureInfo.InvariantCulture) + " -> " + scale.ToString(CultureInfo.InvariantCulture));
        }

        // A scale that grows by more than the precision does takes the room from the integral digits (precision
        // minus scale shrinks): decimal(18,2) -> decimal(18,4) keeps 14 of 16, decimal(9,2) -> decimal(11,5) keeps 6
        // of 7, and a large existing value no longer fits.
        if (newPrecision is { } p && newScale is { } s && oldPrecision is { } fp && oldScale is { } fs && p - s < fp - fs)
        {
            reasons.Add("integral digits " + (fp - fs).ToString(CultureInfo.InvariantCulture) + " -> " + (p - s).ToString(CultureInfo.InvariantCulture));
        }
    }



    /// <summary>
    /// A store type split into its family and facets: <c>nvarchar(50)</c> (length), <c>nvarchar(max)</c>
    /// (unbounded), <c>decimal(9, 3)</c> (precision, scale), <c>decimal(18)</c> (precision 18, scale 0, as both
    /// providers read it), <c>int</c> (none). <see cref="Family"/> is lower-case with whitespace removed, and
    /// <c>numeric</c> / <c>dec</c> read as <c>decimal</c>, and a qualifier after the facet (<c>timestamp(3) with
    /// time zone</c>) is part of it. A quoted or bracketed identifier (a user-defined type) is kept as written,
    /// since its case and spaces are the name. <see cref="Text"/> is the type as written, trimmed.
    /// <see cref="Facetless"/> marks a length or decimal family written without its facets (the provider's
    /// default applies), <see cref="Unbounded"/> an explicit <c>max</c>.
    /// </summary>
    private sealed record StoreType(string? Text, string? Family, int? Length, int? Precision, int? Scale, bool Facetless = false, bool Unbounded = false)
    {
        private static readonly Regex Shape = new(@"^(?<family>[^(]+?)\s*(\((?<facets>[^)]*)\)(?<qualifier>.*))?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        public static bool IsDecimal(string family)
        {
            return family is "decimal" or "numeric" or "dec";
        }

        public static bool IsLengthFamily(string family)
        {
            return family is "char" or "nchar" or "varchar" or "nvarchar" or "binary" or "varbinary" or "character" or "charactervarying" or "bpchar";
        }

        public static StoreType Parse(string? columnType)
        {
            if (string.IsNullOrWhiteSpace(columnType))
            {
                return new StoreType(Text: null, Family: null, Length: null, Precision: null, Scale: null);
            }

            var text = columnType.Trim();
            if (text.IndexOfAny(['"', '[', '`']) >= 0)
            {
                // A quoted or bracketed name is an identifier and one token: its case, spaces and parentheses are
                // the name ("Order State" and "OrderState" are two types, so are "Order(TypeA)" and "Order(TypeB)"),
                // and nothing in it is a facet.
                return new StoreType(text, text, Length: null, Precision: null, Scale: null);
            }

            var match = Shape.Match(text);
            var family = match.Success ? match.Groups["family"].Value + match.Groups["qualifier"].Value : text;
            // A keyword type: case and spacing are formatting.
            family = Regex.Replace(family, @"\s+", string.Empty, RegexOptions.None, TimeSpan.FromSeconds(1)).ToLowerInvariant();
            family = IsDecimal(family) ? "decimal" : family;   // numeric and dec are aliases of decimal on both providers: not a type change

            var facets = match.Success && match.Groups["facets"].Success
                ? match.Groups["facets"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                : [];
            var numbers = facets.Select(facet => int.TryParse(facet, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : (int?)null).ToArray();
            var facetless = facets.Length == 0 && (IsDecimal(family) || IsLengthFamily(family));
            var unbounded = facets is [var only] && string.Equals(only, "max", StringComparison.OrdinalIgnoreCase);
            return numbers switch
            {
                [{ } one] when IsDecimal(family) => new StoreType(text, family, Length: null, Precision: one, Scale: 0),
                [{ } one] when IsLengthFamily(family) => new StoreType(text, family, Length: one, Precision: null, Scale: null),
                [{ } one] => new StoreType(text, family, Length: null, Precision: one, Scale: null),   // time(3), datetime2(7), float(24): a precision, not a length
                [{ } precision, { } scale] => new StoreType(text, family, Length: null, precision, scale),
                _ => new StoreType(text, family, Length: null, Precision: null, Scale: null, facetless, unbounded),   // int, varchar, nvarchar(max), facets this code does not read
            };
        }
    }



    private static string Qualified(string? schema, string name)
    {
        return string.IsNullOrEmpty(schema) ? name : string.Create(CultureInfo.InvariantCulture, $"{schema}.{name}");
    }
}
