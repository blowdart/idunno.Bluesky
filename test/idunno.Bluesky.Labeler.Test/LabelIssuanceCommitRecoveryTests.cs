// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;

namespace idunno.Bluesky.Labeler.Test;

public class LabelIssuanceCommitRecoveryTests
{
    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ReportsKnownRollbackBeforeCommitAndAllowsSameIdRetry(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient httpClient = new(new TargetResolutionHandler());
        LabelIssuanceService service = CreateService(fixture.Store, key, httpClient);
        LabelOperationRequest request = Request("known-rollback");
        int decisionAttempts = 0;
        fixture.SetCommitHooks(
            beforeCommit: async (connection, transaction, token) =>
        {
            await using DbCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT COUNT(*) FROM label_decisions";
            if (Convert.ToInt64(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture) > 0)
            {
                decisionAttempts++;
                await InjectTransientFailureAsync(
                    engine: engine,
                    connection: connection,
                    transaction: transaction,
                    token: token);
            }
        });

        LabelServiceResult<LabelOperationReceipt> failed = await service.ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        fixture.SetCommitHooks();
        Assert.False(failed.Succeeded);
        Assert.Equal(LabelServiceErrorCode.StorageUnavailable, failed.Error!.Code);
        Assert.NotNull(failed.Error!.Exception);
        Assert.True(decisionAttempts > 0);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).IssuedThrough);
        Assert.Equal(
            LabelServiceErrorCode.OperationNotFound,
            (await (fixture.Store).GetOperationAsync(
                request.OperationId,
                TestContext.Current.CancellationToken)).Error!.Code);

        LabelOperationReceipt retried = (await service.ExecuteAsync(
            request,
            TestContext.Current.CancellationToken)).Result!;
        Assert.True(retried.Changed);
        Assert.Equal(1, retried.StateAtCommit.IssuedThrough);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RecoversCommittedReceiptAfterAcknowledgementLossUsingFreshProviderInstance(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient httpClient = new(new TargetResolutionHandler());
        TestLabelIssuanceAuthorizer authorizer = new();
        LabelIssuanceService service = new(
            issuer: StoreFixture.s_issuer,
            signingKey: key,
            store: fixture.Store,
            authorizer: authorizer,
            policyProvider: new TestLabelIssuancePolicyProvider(
                new LabelIssuancePolicy(
                    version: "policy-v1",
                    declaredValues: ["spam"],
                    allowAccounts: true,
                    allowRecords: true)),
            httpClient: httpClient);
        LabelOperationRequest request = Request("lost-acknowledgement");
        bool acknowledgementLost = false;
        fixture.SetCommitHooks(
            afterCommit: async (connection, token) =>
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM label_decisions";
            if (!acknowledgementLost &&
                Convert.ToInt64(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture) > 0)
            {
                acknowledgementLost = true;
                await InjectTransientFailureAsync(
                    engine: engine,
                    connection: connection,
                    transaction: null,
                    token: token);
            }
        });

        LabelServiceResult<LabelOperationReceipt> result = await service.ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        fixture.SetCommitHooks();
        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.True(acknowledgementLost);
        Assert.Contains(
            authorizer.Requests,
            check =>
            !check.IsAdmissionCheck && check.ResolvedTarget?.Repository.Value == TargetResolutionHandler.SubjectDid);
        Assert.True(result.Result!.Changed);
        Assert.Equal(1, result.Result.StateAtCommit.IssuedThrough);

        ILabelStore restartedStore = fixture.NewStore();
        LabelServiceResult<LabelOperationReceipt> recovered = await restartedStore.GetOperationAsync(
            request.OperationId,
            TestContext.Current.CancellationToken);
        Assert.True(recovered.Succeeded);
        Assert.Equal(result.Result!.Request.OperationId, recovered.Result!.Request.OperationId);
        Assert.Equal(result.Result.StateAtCommit, recovered.Result.StateAtCommit);
        Assert.Equal(result.Result.Label!.Sequence, recovered.Result.Label!.Sequence);
        Assert.Equal(result.Result.Label.SignedLabel.GetJsonBytes(), recovered.Result.Label.SignedLabel.GetJsonBytes());
        Assert.Equal(1, (await restartedStore.GetStateAsync(TestContext.Current.CancellationToken)).IssuedThrough);
    }

    /// <summary>Constructs a service with explicit grants and policy so faults exercise persistence rather than admission.</summary>
    private static LabelIssuanceService CreateService(
        ILabelStore store,
        LabelSigningKey key,
        HttpClient httpClient) =>
        new(
            issuer: StoreFixture.s_issuer,
            signingKey: key,
            store: store,
            authorizer: new TestLabelIssuanceAuthorizer(),
            policyProvider: new TestLabelIssuancePolicyProvider(
                new LabelIssuancePolicy(
                    version: "policy-v1",
                    declaredValues: ["spam"],
                    allowAccounts: true,
                    allowRecords: true)),
            httpClient: httpClient);

    /// <summary>Creates repeatable business intent whose operation ID can be reused across failure and recovery.</summary>
    private static LabelOperationRequest Request(string operationId) =>
        LabelOperationRequest.Issue(
            operationId: operationId,
            target: LabelTarget.Account(new Did(TargetResolutionHandler.SubjectDid)),
            value: "spam",
            context: new LabelOperationContext(new Did("did:plc:label-test-moderator"), "commit-boundary test"));

    /// <summary>Raises a genuine provider contention error at the selected transaction boundary.</summary>
    /// <remarks><para>The same error is safely retried before commit but must be treated as uncertain
    /// after submission; provider-specific injection checks both classifications without message parsing.</para></remarks>
    private static async Task InjectTransientFailureAsync(
        string engine,
        DbConnection connection,
        DbTransaction? transaction,
        CancellationToken token)
    {
        if (engine == "sqlite")
        {
            throw new SqliteException("Injected commit-boundary failure.", 5);
        }

        await using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = engine == "postgresql"
            ? "DO $$ BEGIN RAISE EXCEPTION 'Injected commit-boundary failure' USING ERRCODE = '40001'; END $$"
            : "SIGNAL SQLSTATE '40001' SET MYSQL_ERRNO = 1213, MESSAGE_TEXT = 'Injected commit-boundary failure'";
        await command.ExecuteNonQueryAsync(token);
        throw new InvalidOperationException("The database did not inject the expected transient failure.");
    }
}
