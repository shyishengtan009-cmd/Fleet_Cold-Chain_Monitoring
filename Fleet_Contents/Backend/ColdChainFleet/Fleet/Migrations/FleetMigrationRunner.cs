using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FleetCore.Context;
using Npgsql;

namespace FleetCore.Fleet.Migrations;

/// <summary>
/// Runs numbered SQL migration scripts for the Fleet schema.
///
/// ── How it works ─────────────────────────────────────────────────────────────
/// SQL scripts live as embedded resources under Fleet/Migrations/Scripts/ and
/// are named with a 4-digit prefix (0001_, 0002_, …) so they always execute in
/// the correct order regardless of file system ordering.
///
/// Execution history is tracked in iot.schema_versions (created on first run).
/// Each script runs exactly once — on subsequent service restarts already-applied
/// scripts are skipped with an INFO log. Every script runs inside a transaction:
/// if any statement in the script fails the migration is rolled back and the
/// service startup throws so the problem is visible immediately.
///
/// ── Adding a new migration ────────────────────────────────────────────────────
///   1. Create Fleet/Migrations/Scripts/NNNN_describe_change.sql
///   2. Write idempotent DDL (CREATE IF NOT EXISTS, ADD COLUMN IF NOT EXISTS)
///   3. The script runs automatically on next service startup — no other changes needed
///
/// ── Why no external library ───────────────────────────────────────────────────
/// The full feature set of DbUp/FluentMigrator is not needed here. This runner
/// covers the one requirement that matters: each script runs once in order.
/// Using Npgsql directly keeps the dependency tree clean.
/// </summary>
public static class FleetMigrationRunner
{
    // Embedded resource names contain this segment: FleetCore.Fleet.Migrations.Scripts.*
    private const string ScriptMarker = "Fleet.Migrations.Scripts.";

    // ─── RunAsync ─────────────────────────────────────────────────────────────

    public static async Task RunAsync(DatabaseContext databaseContext)
    {
        using var conn = (NpgsqlConnection)databaseContext.CreateConnection();
        await conn.OpenAsync();

        await EnsureSchemaVersionsTable(conn);

        var assembly = Assembly.GetExecutingAssembly();
        var scripts  = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(ScriptMarker))
            .OrderBy(n => n)
            .ToList();

        FleetLog.Info($"[Fleet-Migrate] {scripts.Count} migration script(s) found.");

        foreach (var fullName in scripts)
        {
            // Short name used as the primary key in iot.schema_versions
            var shortName = fullName[(fullName.IndexOf(ScriptMarker, StringComparison.Ordinal) + ScriptMarker.Length)..];

            if (await IsApplied(conn, shortName))
            {
                FleetLog.Info($"[Fleet-Migrate] {shortName} — already applied, skipping.");
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(fullName)
                ?? throw new InvalidOperationException($"Embedded resource '{fullName}' not found.");
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync();

            FleetLog.Info($"[Fleet-Migrate] Applying {shortName} ...");

            await using var tx = await conn.BeginTransactionAsync();
            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn, tx) { CommandTimeout = 120 };
                await cmd.ExecuteNonQueryAsync();

                await RecordApplied(conn, tx, shortName);
                await tx.CommitAsync();

                FleetLog.Info($"[Fleet-Migrate] {shortName} — applied OK.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                throw new InvalidOperationException(
                    $"Fleet migration '{shortName}' failed: {ex.Message}", ex);
            }
        }

        FleetLog.Info("[Fleet-Migrate] Schema is up to date.");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static async Task EnsureSchemaVersionsTable(NpgsqlConnection conn)
    {
        const string sql = @"
CREATE SCHEMA IF NOT EXISTS iot;
CREATE TABLE IF NOT EXISTS iot.schema_versions (
    script_name TEXT        PRIMARY KEY,
    applied_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<bool> IsApplied(NpgsqlConnection conn, string shortName)
    {
        const string sql = "SELECT 1 FROM iot.schema_versions WHERE script_name = @n LIMIT 1;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("n", shortName);
        return await cmd.ExecuteScalarAsync() is not null;
    }

    private static async Task RecordApplied(NpgsqlConnection conn, NpgsqlTransaction tx, string shortName)
    {
        const string sql = "INSERT INTO iot.schema_versions (script_name) VALUES (@n);";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("n", shortName);
        await cmd.ExecuteNonQueryAsync();
    }
}
