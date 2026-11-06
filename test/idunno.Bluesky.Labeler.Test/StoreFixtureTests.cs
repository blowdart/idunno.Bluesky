// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Globalization;

namespace idunno.Bluesky.Labeler.Test;

public class StoreFixtureTests
{
    [Theory]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ReusesConnectionsWithoutSharingDatabasesOrLeavingSessionsBehind(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture remaining = await StoreFixture.CreateAsync(engine);
        long remainingSession = await SessionAsync(remaining, token);
        string removedDatabase;
        await using (StoreFixture removed = await StoreFixture.CreateAsync(engine))
        {
            long removedSession = await SessionAsync(removed, token);
            Assert.NotEqual(remainingSession, removedSession);
            Assert.Equal(removedSession, await SessionAsync(removed, token));
            Assert.NotEqual(
                (await remaining.Store.GetStateAsync(token)).StreamId,
                (await removed.Store.GetStateAsync(token)).StreamId);
            await using DbConnection connection = removed.Connection();
            removedDatabase = connection.Database;
        }

        Assert.Equal(remainingSession, await SessionAsync(remaining, token));
        await using DbConnection retained = remaining.Connection();
        await retained.OpenAsync(token);
        await using DbCommand command = retained.CreateCommand();
        command.CommandText = engine == "mysql"
            ? "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = @database"
            : "SELECT COUNT(*) FROM pg_database WHERE datname = @database";
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "@database";
        parameter.Value = removedDatabase;
        command.Parameters.Add(parameter);
        Assert.Equal(0, Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture));
    }

    /// <summary>Reads the physical server session so connection reuse is checked without elapsed-time assertions.</summary>
    private static async Task<long> SessionAsync(StoreFixture fixture, CancellationToken token)
    {
        await using DbConnection connection = fixture.Connection();
        await connection.OpenAsync(token);
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = fixture.Engine == "mysql" ? "SELECT CONNECTION_ID()" : "SELECT pg_backend_pid()";
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }
}
