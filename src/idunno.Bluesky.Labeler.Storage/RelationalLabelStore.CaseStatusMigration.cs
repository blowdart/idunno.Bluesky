// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Runtime.CompilerServices;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    /// <summary>Expands the persisted case-state constraint without rewriting case payloads or immutable history.</summary>
    /// <remarks><para>Catalog inspection makes the upgrade repeatable after interrupted MySQL DDL.
    /// SQLite rebuilds only the case table with foreign keys disabled by its migration owner and
    /// checks referential integrity before committing; all dependent tables retain their original references.</para></remarks>
    private async Task UpgradeCaseStatusAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string schema,
        CancellationToken token)
    {
        if (Provider == "sqlite")
        {
            string definition = (string)(await ScalarAsync(
                connection: connection,
                transaction: transaction,
                sql: "SELECT sql FROM sqlite_schema WHERE type='table' AND name='label_cases'",
                token: token).ConfigureAwait(false) ?? throw new InvalidDataException("The case table is missing."));
            if (!definition.Contains("BETWEEN 0 AND 2", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            int start = schema.IndexOf("CREATE TABLE IF NOT EXISTS label_cases (", StringComparison.Ordinal);
            int end = schema.IndexOf(';', start);
            string create = schema[start..(end + 1)].Replace(
                "CREATE TABLE IF NOT EXISTS label_cases (",
                "CREATE TABLE label_cases_upgraded (",
                StringComparison.Ordinal);
            await ExecuteAsync(
                connection: connection,
                transaction: transaction,
                sql: create + """

                INSERT INTO label_cases_upgraded(case_id,subject_hash,subject_key,status,assignee_hash,assignee_key,payload,mute_subject_hash,mute_subject_key)
                SELECT case_id,subject_hash,subject_key,status,assignee_hash,assignee_key,payload,mute_subject_hash,mute_subject_key FROM label_cases;
                DROP TABLE label_cases;
                ALTER TABLE label_cases_upgraded RENAME TO label_cases;
                """,
                token: token).ConfigureAwait(false);
            await ExecuteAsync(connection: connection, transaction: transaction, sql: schema, token: token).ConfigureAwait(false);
            return;
        }

        string query = Provider == "postgresql"
            ? "SELECT conname,pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid='label_cases'::regclass AND contype='c'"
            : """
              SELECT t.CONSTRAINT_NAME,c.CHECK_CLAUSE
              FROM information_schema.TABLE_CONSTRAINTS t
              JOIN information_schema.CHECK_CONSTRAINTS c ON c.CONSTRAINT_SCHEMA=t.CONSTRAINT_SCHEMA AND c.CONSTRAINT_NAME=t.CONSTRAINT_NAME
              WHERE t.TABLE_SCHEMA=DATABASE() AND t.TABLE_NAME='label_cases' AND t.CONSTRAINT_TYPE='CHECK'
              """;
        List<string> statusConstraints = [];
        bool current = false;
        DbCommand command = Command(connection, transaction, query);
        await using (command.ConfigureAwait(false))
        {
            DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                string definition = reader.GetString(1);
                if (definition.Contains("status", StringComparison.OrdinalIgnoreCase))
                {
                    statusConstraints.Add(reader.GetString(0));
                    current |= definition.Contains('3', StringComparison.Ordinal);
                }
            }
        }
        if (current)
        {
            return;
        }
        string quote = Provider == "postgresql" ? "\"" : "`";
        string drop = Provider == "postgresql" ? "DROP CONSTRAINT " : "DROP CHECK ";
        List<string> changes = [.. statusConstraints.Select(name =>
            drop + quote + name.Replace(quote, quote + quote, StringComparison.Ordinal) + quote)];
        changes.Add("ADD CONSTRAINT label_cases_status_check CHECK(status BETWEEN 0 AND 3)");
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "ALTER TABLE label_cases " + string.Join(", ", changes),
            token: token).ConfigureAwait(false);
    }
}
