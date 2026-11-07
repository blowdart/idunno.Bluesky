// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    /// <summary>Adds collision-checked, CID-independent mute lookup keys without changing source evidence.</summary>
    /// <remarks><para>Nullable staging columns and bounded backfill batches resume interrupted MySQL setup.
    /// Completed projections are required before normal operations. SQLite rebuilds the two tables under
    /// its migration owner's immediate transaction with foreign keys disabled and checked before commit.</para></remarks>
    private async Task UpgradeMuteSubjectsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string schema,
        CancellationToken token)
    {
        foreach (string table in new[] { "label_cases", "label_reports" })
        {
            foreach (string column in new[] { "mute_subject_hash", "mute_subject_key" })
            {
                string exists = Provider switch
                {
                    "sqlite" => $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name=@column",
                    "postgresql" => $"SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=current_schema() AND table_name='{table}' AND column_name=@column",
                    _ => $"SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='{table}' AND column_name=@column"
                };
                if (Integer(await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: exists,
                    token: token,
                    ("@column", column)).ConfigureAwait(false)) == 0)
                {
                    string type = Provider switch
                    {
                        "sqlite" => "BLOB",
                        "postgresql" => "BYTEA",
                        _ => column == "mute_subject_hash" ? "BINARY(32)" : "LONGBLOB"
                    };
                    string lengthFunction = Provider == "sqlite" ? "length" : "octet_length";
                    string check = column != "mute_subject_hash" || Provider == "mysql" ? string.Empty :
                        $" CHECK({lengthFunction}({column})=32)";
                    await ExecuteAsync(
                        connection: connection,
                        transaction: transaction,
                        sql: $"ALTER TABLE {table} ADD COLUMN {column} {type} NULL{check}",
                        token: token).ConfigureAwait(false);
                }
            }

            await BackfillMuteSubjectsAsync(connection, transaction, table, token).ConfigureAwait(false);
            string nullable = Provider switch
            {
                "sqlite" => $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name IN ('mute_subject_hash','mute_subject_key') AND \"notnull\"=0",
                "postgresql" => $"SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=current_schema() AND table_name='{table}' AND column_name IN ('mute_subject_hash','mute_subject_key') AND is_nullable='YES'",
                _ => $"SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='{table}' AND column_name IN ('mute_subject_hash','mute_subject_key') AND is_nullable='YES'"
            };
            if (Integer(await ScalarAsync(
                connection: connection,
                transaction: transaction,
                sql: nullable,
                token: token).ConfigureAwait(false)) == 0)
            {
                continue;
            }
            if (Provider == "sqlite")
            {
                int start = schema.IndexOf($"CREATE TABLE IF NOT EXISTS {table} (", StringComparison.Ordinal);
                int end = schema.IndexOf(';', start);
                string create = schema[start..(end + 1)].Replace(
                    $"CREATE TABLE IF NOT EXISTS {table} (",
                    $"CREATE TABLE {table}_upgraded (",
                    StringComparison.Ordinal);
                string columns = table == "label_cases"
                    ? "case_id,subject_hash,subject_key,status,assignee_hash,assignee_key,payload,mute_subject_hash,mute_subject_key"
                    : "report_id,case_id,reason_hash,reason_type,payload,mute_subject_hash,mute_subject_key";
                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: create + $"""

                        INSERT INTO {table}_upgraded({columns}) SELECT {columns} FROM {table};
                        DROP TABLE {table};
                        ALTER TABLE {table}_upgraded RENAME TO {table};
                        """,
                    token: token).ConfigureAwait(false);
                await ExecuteAsync(connection: connection, transaction: transaction, sql: schema, token: token).ConfigureAwait(false);
            }
            else
            {
                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: Provider == "postgresql"
                        ? $"ALTER TABLE {table} ALTER COLUMN mute_subject_hash SET NOT NULL, ALTER COLUMN mute_subject_key SET NOT NULL"
                        : $"ALTER TABLE {table} MODIFY COLUMN mute_subject_hash BINARY(32) NOT NULL, MODIFY COLUMN mute_subject_key LONGBLOB NOT NULL",
                    token: token).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Resumes missing mute projections in primary-key order with bounded memory and no network lookups.</summary>
    private static async Task BackfillMuteSubjectsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string table,
        CancellationToken token)
    {
        string idColumn = table == "label_cases" ? "case_id" : "report_id";
        long after = 0;
        while (true)
        {
            List<(long Id, byte[] Key)> page = [];
            DbCommand command = Command(
                connection: connection,
                transaction: transaction,
                sql: $"SELECT {idColumn},payload FROM {table} WHERE {idColumn}>@after AND " +
                    $"(mute_subject_hash IS NULL OR mute_subject_key IS NULL) ORDER BY {idColumn} LIMIT 100",
                ("@after", after));
            await using (command.ConfigureAwait(false))
            {
                DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    byte[] payload = (byte[])reader.GetValue(1);
                    string subject = table == "label_cases" ? DecodeCase(payload).Subject : DecodeReport(payload).Subject;
                    page.Add((Integer(reader.GetValue(0)), s_strictUtf8.GetBytes(subject)));
                }
            }
            foreach ((long id, byte[] key) in page)
            {
                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: $"UPDATE {table} SET mute_subject_hash=@hash,mute_subject_key=@key WHERE {idColumn}=@id",
                    token: token,
                    ("@hash", SHA256.HashData(key)), ("@key", key), ("@id", id)).ConfigureAwait(false);
                after = id;
            }
            if (page.Count < 100)
            {
                break;
            }
        }
    }
}
