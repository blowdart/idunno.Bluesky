// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.Labeler.Test;

public class LabelQueryProjectionTests
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task TimestampProjectionsPreserveSourceOrderingAcrossBackfillAndHandling(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        string?[] timestamps = ["2026-01-01T00:00:00+02:00", "2025-12-31T23:00:00-02:00", "2026-01-01T00:00:00.1234567Z", null];
        foreach (int index in Enumerable.Range(0, timestamps.Length))
        {
            ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(
                new("did:plc:reporter"), policy.DatabaseTime, "timestamp-" + index);
            LabelModerationReport report = Success(await ((ILabelReportStore)fixture.Store).SubmitReportAsync(
                authentication: authentication,
                submission: new(LabelReportSubject.Record(new("at://did:plc:projection/app.bsky.feed.post/record")), "tools.ozone.report.defs#reasonMisleadingSpam"),
                expectedPolicyRevision: policy.Revision,
                cancellationToken: token));
            await using DbConnection connection = fixture.Connection();
            await connection.OpenAsync(token);
            await using DbCommand read = connection.CreateCommand();
            read.CommandText = "SELECT payload FROM label_reports WHERE report_id=@id";
            DbParameter id = read.CreateParameter();
            id.ParameterName = "@id";
            id.Value = report.Id;
            read.Parameters.Add(id);
            JsonObject payload = Assert.IsType<JsonObject>(JsonNode.Parse(Encoding.UTF8.GetString(Assert.IsType<byte[]>(await read.ExecuteScalarAsync(token)))));
            if (timestamps[index] is { } timestamp)
            {
                payload[nameof(LabelModerationReport.CreatedAt)] = timestamp;
            }
            else
            {
                Assert.True(payload.Remove(nameof(LabelModerationReport.CreatedAt)));
            }
            await using DbCommand update = connection.CreateCommand();
            update.CommandText = "UPDATE label_reports SET payload=@payload WHERE report_id=@id";
            DbParameter updatedId = update.CreateParameter();
            updatedId.ParameterName = "@id";
            updatedId.Value = report.Id;
            update.Parameters.Add(updatedId);
            DbParameter bytes = update.CreateParameter();
            bytes.ParameterName = "@payload";
            bytes.Value = Encoding.UTF8.GetBytes(payload.ToJsonString());
            update.Parameters.Add(bytes);
            await update.ExecuteNonQueryAsync(token);
        }
        await fixture.ExecuteAsync("DELETE FROM label_report_queries");
        byte[][] evidence = await EvidenceAsync(fixture);
        ILabelStore restarted = fixture.NewStore();
        await restarted.InitializeAsync(StoreFixture.s_issuer, token);
        Assert.Equal(evidence, await EvidenceAsync(fixture));
        foreach (bool ascending in new[] { true, false })
        {
            string field = engine switch
            {
                "postgresql" => "(convert_from(payload,'UTF8')::jsonb->>'CreatedAt') COLLATE \"C\"",
                "mysql" => "JSON_UNQUOTE(JSON_EXTRACT(CONVERT(payload USING utf8mb4),'$.CreatedAt')) COLLATE utf8mb4_bin",
                _ => "json_extract(CAST(payload AS TEXT),'$.CreatedAt') COLLATE BINARY"
            };
            string direction = ascending ? "ASC" : "DESC";
            List<long> expected = [];
            await using DbConnection connection = fixture.Connection();
            await connection.OpenAsync(token);
            await using DbCommand command = connection.CreateCommand();
#pragma warning disable CA2100 // Only fixed provider expressions and ordering enter this test query.
            command.CommandText = $"SELECT report_id FROM label_reports ORDER BY {field} {direction},report_id {direction}";
#pragma warning restore CA2100
            await using DbDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                expected.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
            LabelTablePage page = Success(await ((ILabelTableStore)restarted).ReadTableAsync(
                caller, new(Kind: LabelTableKind.Reports, Sort: LabelTableSort.Received, Ascending: ascending), token));
            Assert.Equal(expected, page.Reports!.Select(report => report.Id));
        }
        LabelOzoneEvent committed = Success(await ((ILabelOzoneStore)restarted).CommitOzoneEventAsync(
            caller: caller,
            request: new(
                OperationId: "legacy-timestamp-handling",
                ExternalId: null,
                Subject: LabelReportSubject.Record(new("at://did:plc:projection/app.bsky.feed.post/record")),
                EventJson: """{"$type":"tools.ozone.moderation.defs#modEventAcknowledge"}""",
                ModToolJson: """{"name":"Projection test"}""",
                Reason: "Review legacy report timestamps.",
                Actions: [],
                ReportIds: [],
                ReportTypes: [],
                AllReports: true,
                ResolveSubject: false,
                Automated: false,
                ReportNote: "Preserve query timestamp representations.",
                IncludeAssignedReports: true),
            policyRevision: policy.Revision,
            labels: [],
            cancellationToken: token));
        Assert.Equal(timestamps.Length, committed.ReportIds.Length);
        await using DbConnection verify = fixture.Connection();
        await verify.OpenAsync(token);
        await using DbCommand projected = verify.CreateCommand();
        projected.CommandText = "SELECT t.payload,q.created_at FROM label_reports t JOIN label_report_queries q ON q.report_id=t.report_id";
        await using DbDataReader source = await projected.ExecuteReaderAsync(token);
        int handled = 0;
        while (await source.ReadAsync(token))
        {
            JsonObject payload = Assert.IsType<JsonObject>(JsonNode.Parse(Encoding.UTF8.GetString((byte[])source.GetValue(0))));
            if (payload[nameof(LabelModerationReport.CreatedAt)] is { } timestamp)
            {
                Assert.Equal(timestamp.GetValue<string>(), source.GetString(1));
            }
            else
            {
                Assert.True(source.IsDBNull(1));
            }
            Assert.Equal("legacy-timestamp-handling", payload["DecisionOperationId"]?.GetValue<string>());
            handled++;
        }
        Assert.Equal(timestamps.Length, handled);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task SubjectOrderingUsesCompleteValuesBeyondProviderIndexPrefixes(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        string repository = "did:web:" + new string('a', 2040);
        string collection = "com." + string.Join('.', Enumerable.Repeat(new string('b', 63), 4));
        string prefix = "at://" + repository + "/" + collection + "/" + new string('c', 480);
        List<long> ids = [];
        foreach (string suffix in new[] { "z", "b" })
        {
            ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(
                new("did:plc:reporter"), policy.DatabaseTime, "long-" + suffix);
            LabelModerationReport report = Success(await ((ILabelReportStore)fixture.Store).SubmitReportAsync(
                authentication: authentication,
                submission: new(LabelReportSubject.Record(new(prefix + suffix)), "tools.ozone.report.defs#reasonMisleadingSpam"),
                expectedPolicyRevision: policy.Revision,
                cancellationToken: token));
            ids.Add(report.Id);
        }
        LabelTablePage page = Success(await ((ILabelTableStore)fixture.Store).ReadTableAsync(
            caller, new(Kind: LabelTableKind.Reports, Sort: LabelTableSort.Subject, Ascending: true), token));
        Assert.Equal(ids.AsEnumerable().Reverse(), page.Reports!.Select(report => report.Id));
        LabelTablePage descending = Success(await ((ILabelTableStore)fixture.Store).ReadTableAsync(
            caller, new(Kind: LabelTableKind.Reports, Sort: LabelTableSort.Subject, Ascending: false), token));
        Assert.Equal(ids, descending.Reports!.Select(report => report.Id));
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("postgresql", false)]
    [InlineData("mysql", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", true)]
    public async Task VersionSeventeenBackfillPreservesEvidenceOrderingAndResumesMissingProjectionRows(string engine, bool partial)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelReportSubject subject = LabelReportSubject.Record(new("at://did:plc:projection/app.bsky.feed.post/record"));
        long caseId = 0;
        for (int index = 0; index < 101; index++)
        {
            ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(
                new("did:plc:reporter"), policy.DatabaseTime, "projection-" + index);
            LabelModerationReport report = Success(await ((ILabelReportStore)fixture.Store).SubmitReportAsync(
                authentication: authentication,
                submission: new(subject, "tools.ozone.report.defs#reasonMisleadingSpam"),
                expectedPolicyRevision: policy.Revision,
                cancellationToken: token));
            caseId = report.CaseId;
        }
        ILabelTableStore tables = (ILabelTableStore)fixture.Store;
        LabelTableQuery query = new(Kind: LabelTableKind.Reports, Sort: LabelTableSort.Received, Page: 4);
        LabelTablePage original = Success(await tables.ReadTableAsync(caller, query, token));
        LabelCaseActivityPage activity = Success(await tables.ReadCaseActivityAsync(caller, caseId, 4, token));
        byte[][] evidence = await EvidenceAsync(fixture);
        await fixture.ExecuteAsync(engine == "mysql"
            ? "DROP INDEX label_appeals_status ON label_appeals"
            : "DROP INDEX label_appeals_status");
        if (partial)
        {
            await fixture.ExecuteAsync("""
                DELETE FROM label_report_queries WHERE report_id>1;
                DELETE FROM label_case_queries;
                DELETE FROM label_activity_queries;
                DELETE FROM label_audit_queries;
                UPDATE label_schema SET version=18 WHERE id=1;
                """);
        }
        else
        {
            await fixture.ExecuteAsync("""
                DROP TABLE label_report_queries;
                DROP TABLE label_case_queries;
                DROP TABLE label_activity_queries;
                DROP TABLE label_audit_queries;
                UPDATE label_schema SET version=17 WHERE id=1;
                """);
        }
        ILabelStore restarted = fixture.NewStore();
        await restarted.InitializeAsync(StoreFixture.s_issuer, token);
        await restarted.InitializeAsync(StoreFixture.s_issuer, token);
        Assert.Equal(evidence, await EvidenceAsync(fixture));
        tables = (ILabelTableStore)restarted;
        LabelTablePage restored = Success(await tables.ReadTableAsync(caller, query, token));
        Assert.Equal(original.Total, restored.Total);
        Assert.Equal(original.Reports!.Select(report => report.Id), restored.Reports!.Select(report => report.Id));
        LabelCaseActivityPage restoredActivity = Success(await tables.ReadCaseActivityAsync(caller, caseId, 4, token));
        Assert.Equal(activity.Page, restoredActivity.Page);
        Assert.Equal(activity.Total, restoredActivity.Total);
        Assert.Equal(
            activity.Entries.Select(entry => (entry.Report?.Id, entry.Event?.Sequence, entry.Report?.CreatedAt, entry.Event?.CreatedAt)),
            restoredActivity.Entries.Select(entry => (entry.Report?.Id, entry.Event?.Sequence, entry.Report?.CreatedAt, entry.Event?.CreatedAt)));
        LabelTablePage clamped = Success(await tables.ReadTableAsync(caller, query with { Page = long.MaxValue }, token));
        Assert.Equal(7, clamped.Page);
        Assert.Equal(5, clamped.Reports!.Count);
        LabelTablePage cases = Success(await tables.ReadTableAsync(caller, new(LabelTableKind.Cases), token));
        Assert.Equal(101, Assert.Single(cases.AuthorSummaries!).Reports);
        await using DbConnection connection = fixture.Connection();
        await connection.OpenAsync(token);
        await using (DbCommand index = connection.CreateCommand())
        {
            index.CommandText = engine switch
            {
                "postgresql" => "SELECT COUNT(*) FROM pg_indexes WHERE schemaname=current_schema() AND tablename='label_appeals' AND indexname='label_appeals_status'",
                "mysql" => "SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='label_appeals' AND index_name='label_appeals_status'",
                _ => "SELECT COUNT(*) FROM pragma_index_list('label_appeals') WHERE name='label_appeals_status'"
            };
            Assert.Equal(1, Convert.ToInt64(await index.ExecuteScalarAsync(token), CultureInfo.InvariantCulture));
        }
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT repository_hash,repository_key,handled FROM label_report_queries ORDER BY report_id";
        await using DbDataReader reader = await command.ExecuteReaderAsync(token);
        int rows = 0;
        while (await reader.ReadAsync(token))
        {
            byte[] key = Assert.IsType<byte[]>(reader.GetValue(1));
            Assert.Equal("did:plc:projection", Encoding.UTF8.GetString(key));
            Assert.Equal(SHA256.HashData(key), Assert.IsType<byte[]>(reader.GetValue(0)));
            Assert.Equal(0, Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture));
            rows++;
        }
        Assert.Equal(101, rows);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task AuthorHashMatchesRequireExactRepositoryKeys(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(
            new("did:plc:reporter"), policy.DatabaseTime, "collision-report");
        Success(await ((ILabelReportStore)fixture.Store).SubmitReportAsync(
            authentication: authentication,
            submission: new(LabelReportSubject.Account(new("did:plc:projection")), "tools.ozone.report.defs#reasonMisleadingSpam"),
            expectedPolicyRevision: policy.Revision,
            cancellationToken: token));
        await using (DbConnection connection = fixture.Connection())
        {
            await connection.OpenAsync(token);
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE label_report_queries SET repository_key=@key";
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = "@key";
            parameter.Value = Encoding.UTF8.GetBytes("did:plc:another");
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync(token);
        }
        LabelTablePage page = Success(await ((ILabelTableStore)fixture.Store).ReadTableAsync(caller, new(LabelTableKind.Cases), token));
        Assert.Equal(0, Assert.Single(page.AuthorSummaries!).Reports);
    }

    /// <summary>Snapshots immutable source evidence independently of disposable query projections.</summary>
    private static async Task<byte[][]> EvidenceAsync(StoreFixture fixture)
    {
        List<byte[]> payloads = [];
        await using DbConnection connection = fixture.Connection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        foreach (string sql in new[]
        {
            "SELECT payload FROM label_cases ORDER BY case_id",
            "SELECT payload FROM label_reports ORDER BY report_id",
            "SELECT payload FROM label_case_events ORDER BY seq",
            "SELECT payload FROM label_admin_audit ORDER BY seq"
        })
        {
            await using DbCommand command = connection.CreateCommand();
#pragma warning disable CA2100 // Only fixed evidence queries enter this test command.
            command.CommandText = sql;
#pragma warning restore CA2100
            await using DbDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                payloads.Add((byte[])reader.GetValue(0));
            }
        }
        return [.. payloads];
    }

    /// <summary>Requires successful service results without obscuring storage diagnostics.</summary>
    private static T Success<T>(LabelServiceResult<T> result) where T : class
    {
        Assert.True(result.Succeeded, result.Error?.Message + result.Error?.Exception);
        return result.Result;
    }
}
