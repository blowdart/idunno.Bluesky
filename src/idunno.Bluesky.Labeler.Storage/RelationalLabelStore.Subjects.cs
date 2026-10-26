// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Security.Cryptography;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    /// <summary>Indexes an exact subject without incorporating CID into the current label-slot identity.</summary>
    private static Task IndexSubjectAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long sequence,
        SignedLabelSnapshot signed,
        CancellationToken token)
    {
        byte[] subject = s_strictUtf8.GetBytes(signed.Label.Uri);
        return ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "INSERT INTO label_subject_index(seq,subject_hash,subject_key,value) VALUES(@seq,@hash,@key,@value)",
            token: token,
            ("@seq", sequence),
            ("@hash", SHA256.HashData(subject)),
            ("@key", subject),
            ("@value", signed.Label.Value));
    }

    /// <summary>Resumes additive subject projection without rewriting signed labels or existing decision receipts.</summary>
    private static async Task BackfillSubjectIndexAsync(DbConnection connection, DbTransaction? transaction, CancellationToken token)
    {
        long after = 0;
        while (true)
        {
            List<StoredLabel> batch = [];
            DbCommand command = Command(
                connection: connection,
                transaction: transaction,
                sql: "SELECT e.seq,e.signed_json,e.payload,e.format_version FROM label_entries e " +
                    "LEFT JOIN label_subject_index d ON d.seq=e.seq WHERE d.seq IS NULL AND e.seq>@after ORDER BY e.seq LIMIT 100",
                ("@after", after));
            await using (command.ConfigureAwait(false))
            {
                DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        batch.Add(new(reader.GetInt64(0), Restore(reader)));
                    }
                }
            }

            if (batch.Count == 0)
            {
                return;
            }

            foreach (StoredLabel label in batch)
            {
                await IndexSubjectAsync(
                    connection: connection,
                    transaction: transaction,
                    sequence: label.Sequence,
                    signed: label.SignedLabel,
                    token: token).ConfigureAwait(false);
            }

            after = batch[^1].Sequence;
        }
    }
}
