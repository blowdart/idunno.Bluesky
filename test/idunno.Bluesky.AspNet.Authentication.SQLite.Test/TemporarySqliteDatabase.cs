// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Data.Sqlite;

namespace idunno.Bluesky.AspNet.Authentication.SQLite.Test;

/// <summary>
/// Creates a SQLite database in a temporary file, provisioned with the schema the package ships.
/// </summary>
/// <remarks>
/// <para>
///   A file is used rather than an in memory database because each connection to <c>:memory:</c> gets its own database,
///   and the store opens a connection per operation.
/// </para>
/// </remarks>
public sealed class TemporarySqliteDatabase : IDisposable
{
    private readonly string _path;

    public TemporarySqliteDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"idunno-bluesky-sqlite-test-{Guid.NewGuid():N}.db");

        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _path
        }.ConnectionString;

        string schema = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schema.sql"));

        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = schema;
        command.ExecuteNonQuery();
    }

    public string ConnectionString { get; }

    /// <summary>
    /// Counts the rows in <paramref name="table"/>, including any which have expired.
    /// </summary>
    public long CountRows(string table)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\";";

        return (long)command.ExecuteScalar()!;
    }

    /// <summary>
    /// Marks every row in <paramref name="table"/> as expired.
    /// </summary>
    public void ExpireRows(string table)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"UPDATE \"{table}\" SET \"ExpiresAtUtcTicks\" = @expiresAtUtcTicks;";
        command.Parameters.AddWithValue("@expiresAtUtcTicks", DateTime.UtcNow.AddMinutes(-1).Ticks);
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
            // A leftover file in the temporary directory is not worth failing a test run over.
        }
    }
}
