// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.BotSdk.DataProviders;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Checks behavior that applies only to the SQLite provider.</summary>
public sealed class SqliteProviderTests
{
    /// <summary>Checks connectivity without creating a missing database file.</summary>
    [Fact]
    public async Task ReadOnlyConnectionCheckDoesNotCreateDatabase()
    {
        string path = Path.Combine(Path.GetTempPath(), $"bot-doctor-{Guid.NewGuid():N}.db");
        SqliteDatabaseProvider database = new(
            $"Data Source={path}",
            TestRecords.Options(),
            TimeProvider.System,
            NullLogger<SqliteDatabaseProvider>.Instance);

        await Assert.ThrowsAsync<SqliteException>(() =>
            ((IBotDatabaseDiagnostics)database).CheckConnectionAsync(CancellationToken.None));
        Assert.False(File.Exists(path));
    }

    /// <summary>Rejects SQLite connection strings that do not identify persistent storage.</summary>
    /// <param name="connectionString">A nonpersistent SQLite connection string.</param>
    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=memory;Mode=Memory")]
    [InlineData("Data Source=file::memory:?cache=shared")]
    [InlineData("Data Source=file:bot?mode=memory&cache=shared")]
    [InlineData("Data Source=file:bot?cache=shared&mode=memory")]
    [InlineData("Data Source=file:%3Amemory%3A?cache=shared")]
    [InlineData("Data Source=file:bot?%6dode=%6demory")]
    public void RejectsInMemoryConnection(string connectionString)
    {
        Assert.Throws<ArgumentException>(() => new SqliteDatabaseProvider(
            connectionString,
            TestRecords.Options(),
            TimeProvider.System,
            NullLogger<SqliteDatabaseProvider>.Instance));
    }

    /// <summary>Retains persistent URI filenames and ordinary filenames containing query-like text.</summary>
    /// <param name="connectionString">A persistent SQLite connection string.</param>
    [Theory]
    [InlineData("Data Source=file:bot.db?mode=rwc")]
    [InlineData("Data Source=bot.db?mode=memory")]
    public void AcceptsPersistentConnection(string connectionString)
    {
        _ = new SqliteDatabaseProvider(
            connectionString,
            TestRecords.Options(),
            TimeProvider.System,
            NullLogger<SqliteDatabaseProvider>.Instance);
    }
}
