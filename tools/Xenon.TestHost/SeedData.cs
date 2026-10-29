// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.AspNet.Labeler;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

namespace Xenon.TestHost;

/// <summary>Seeds an explicitly requested test environment through the real provider's authorization and commit boundaries.</summary>
internal static class SeedData
{
    /// <summary>Adds idempotent local-only reports for the approved bot's posts, moderation list and profile.</summary>
    internal static async Task InitializeRealPostsAsync(
        IServiceProvider services,
        Did administrator,
        HttpClient identity,
        LabelSigningKey key,
        CancellationToken cancellationToken)
    {
        Did author = new("did:plc:ec72yg6n2sydzjvtovvdlxrk");
        if (!await IdentityResolution.VerifyHandleAsync(
            handle: new("bot.idunno.blue"),
            did: author,
            httpClient: identity,
            cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException("The approved seed account bot.idunno.blue no longer verifies to its expected DID.");
        }

        Uri pds = await IdentityResolution.ResolvePdsAsync(
            did: author, httpClient: identity, cancellationToken: cancellationToken) ??
            throw new InvalidOperationException("The approved seed account's PDS could not be resolved.");
        ILabelAdministrationStore team = services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationReceipt setup = Require(await services.BootstrapLocalAdministratorAsync(
            administrator, "xenon-test-bootstrap", cancellationToken));
        LabelAdministrationCaller caller = Require(await team.AuthenticateLocalAsync(
            setup.LocalIdentity!,
            cancellationToken));
        ILabelReportStore reports = services.GetRequiredService<ILabelReportStore>();
        HashSet<string?> existing = [];
        long afterId = 0;
        while (true)
        {
            IReadOnlyList<LabelModerationReport> page = Require(await reports.GetReportsAsync(
                caller: caller, afterId: afterId, limit: 100, cancellationToken: cancellationToken));
            existing.UnionWith(page.Select(report => report.Submission.Reason));
            if (page.Count < 100)
            {
                break;
            }

            afterId = page[^1].Id;
        }

        foreach ((string collection, string recordKey, string kind) in new[]
        {
            ("app.bsky.feed.post", "3myawvej4uk2z", "post"),
            ("app.bsky.feed.post", "3mtmey2zsf32p", "post"),
            ("app.bsky.feed.post", "3mpjmw6imdy2k", "post"),
            ("app.bsky.feed.post", "3mnzf3lvwvq2t", "post"),
            ("app.bsky.graph.list", "3lnxvrbetf32n", "list"),
            ("app.bsky.actor.profile", "self", "profile")
        })
        {
            string reason = $"Local Xenon preview test for bot.idunno.blue {kind} {recordKey}. This is test data, not an allegation.";
            if (existing.Contains(reason))
            {
                continue;
            }

            AtProtoHttpResult<AtProtoRepositoryRecord> record = await AtProtoServer.GetRecord(
                repo: author,
                collection: new(collection),
                rKey: new(recordKey),
                cid: null,
                service: pds,
                accessCredentials: null,
                httpClient: identity,
                cancellationToken: cancellationToken);
            AtUri expected = new($"at://{author}/{collection}/{recordKey}");
            if (!record.Succeeded || record.Result.Uri != expected)
            {
                throw new InvalidOperationException($"The approved seed {kind} {recordKey} could not be read from its PDS. Earlier seed additions are retained; rerun --seed to resume.");
            }

            LabelPolicyService verifier = (LabelPolicyService)services.GetRequiredService<ILabelIssuancePolicyProvider>();
            LabelPolicyState state = Require(await verifier.GetVerifiedStateAsync(cancellationToken: cancellationToken));
            ModerationReportAuthenticationResult authentication = await ServiceAuthentication.VerifyModerationReportAsync(
                token: CreateReportToken(key),
                labeler: FixtureHandler.s_issuer,
                httpClient: identity,
                cancellationToken: cancellationToken);
            Require(await reports.SubmitReportAsync(
                authentication: authentication,
                submission: new(LabelReportSubject.Record(expected, record.Result.Cid), "tools.ozone.report.defs#reasonOther", reason),
                expectedPolicyRevision: state.Revision,
                cancellationToken: cancellationToken));
        }
    }

    /// <summary>Bootstraps a verified administrator and retains a resumable set of fictional reports, cases and labels.</summary>
    internal static async Task InitializeAsync(
        IServiceProvider services,
        Did administrator,
        HttpClient identity,
        LabelSigningKey key,
        CancellationToken cancellationToken)
    {
        LabelAdministrationReceipt setup = Require(await services.BootstrapLocalAdministratorAsync(
            administrator, "xenon-test-bootstrap", cancellationToken));
        ILabelAdministrationStore team = services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller caller = Require(await team.AuthenticateLocalAsync(setup.LocalIdentity!, cancellationToken));
        ILabelPolicyStore policies = services.GetRequiredService<ILabelPolicyStore>();
        LabelPolicyState state = (await policies.ReadPolicyStateAsync(cancellationToken))!;
        LabelPolicyService verifier = (LabelPolicyService)services.GetRequiredService<ILabelIssuancePolicyProvider>();
        if (state.Policy is null)
        {
            using JsonDocument previousDeclaration = JsonDocument.Parse(FixtureHandler.s_declaration);
            Require(await policies.SavePolicyAsync(
                caller: caller,
                operationId: "xenon-test-policy-history-draft",
                policy: new(
                    version: "xenon-history-v1",
                    declaration: previousDeclaration.RootElement,
                    allowAccounts: true,
                    allowRecords: true,
                    rules: "Earlier fictional test policy. No label is distributed publicly."),
                reason: "Seed an earlier independently verified fixture publication.",
                cancellationToken: cancellationToken));
            Require(await policies.BeginPolicyPublicationAsync(
                caller: caller,
                operationId: "xenon-test-policy-history-select",
                version: "xenon-history-v1",
                expectedRevision: state.Revision,
                previousCid: state.VerifiedCid,
                reason: "Select the earlier local fixture without writing to a PDS.",
                cancellationToken: cancellationToken));
            state = Require(await verifier.GetVerifiedStateAsync(forceVerification: true, cancellationToken: cancellationToken));
        }

        using JsonDocument legacyDeclaration = JsonDocument.Parse(FixtureHandler.LegacyDeclaration);
        LabelPolicyDefinition legacy = new(
            version: "test-v1",
            declaration: legacyDeclaration.RootElement,
            allowAccounts: true,
            allowRecords: true,
            rules: "Fictional local test data. No label is distributed publicly.");
        bool upgradeFixture = state.Policy?.Issuance.Version == "test-v1" &&
            state.Policy.DeclarationJson == legacy.DeclarationJson;
        upgradeFixture |= state.Policy?.Issuance.Version == "xenon-history-v1" &&
            state.Policy.Rules == "Earlier fictional test policy. No label is distributed publicly.";
        if (state.Policy is null || upgradeFixture)
        {
            using JsonDocument declaration = JsonDocument.Parse(FixtureHandler.s_declaration);
            Require(await policies.SavePolicyAsync(
                caller: caller,
                operationId: "xenon-test-policy-draft-v2",
                policy: new(
                    version: "test-v2",
                    declaration: declaration.RootElement,
                    allowAccounts: true,
                    allowRecords: true,
                    rules: "Fictional local test data. No label is distributed publicly."),
                reason: "Explicit development environment setup.",
                cancellationToken: cancellationToken));
            Require(await policies.BeginPolicyPublicationAsync(
                caller: caller,
                operationId: "xenon-test-policy-select-v2",
                version: "test-v2",
                expectedRevision: state.Revision,
                previousCid: state.VerifiedCid,
                reason: "Select the fixture declaration without writing to a real PDS.",
                cancellationToken: cancellationToken));
        }

        state = Require(await verifier.GetVerifiedStateAsync(forceVerification: true, cancellationToken: cancellationToken));
        ILabelReportStore reports = services.GetRequiredService<ILabelReportStore>();
        ILabelModerationStore cases = services.GetRequiredService<ILabelModerationStore>();
        HashSet<string?> existing = [];
        long afterId = 0;
        while (true)
        {
            IReadOnlyList<LabelModerationReport> page = Require(await reports.GetReportsAsync(
                caller: caller, afterId: afterId, limit: 100, cancellationToken: cancellationToken));
            existing.UnionWith(page.Select(report => report.Submission.Reason));
            if (page.Count < 100)
            {
                break;
            }

            afterId = page[^1].Id;
        }

        for (int index = 1; index <= FixtureHandler.SubjectCount; index++)
        {
            LabelReportSubject subject = index % 2 == 0
                ? LabelReportSubject.Record(new($"at://{FixtureHandler.Subject(index)}/app.bsky.feed.post/example"), new(FixtureHandler.Cid))
                : LabelReportSubject.Account(FixtureHandler.Subject(index));
            int reportCount = index switch { 1 => 2, 2 => 3, 4 => 4, _ => 1 };
            for (int reportIndex = 0; reportIndex < reportCount; reportIndex++)
            {
                string reason = reportIndex == 0
                    ? $"Xenon fixture report {index:D2}. Please review this fictional subject."
                    : $"Xenon fixture report {index:D2}, additional submission {reportIndex}. Please review this same fictional subject.";
                if (existing.Contains(reason))
                {
                    continue;
                }

                state = Require(await verifier.GetVerifiedStateAsync(cancellationToken: cancellationToken));
                ModerationReportAuthenticationResult authentication = await ServiceAuthentication.VerifyModerationReportAsync(
                    token: CreateReportToken(key),
                    labeler: FixtureHandler.s_issuer,
                    httpClient: identity,
                    cancellationToken: cancellationToken);
                Require(await reports.SubmitReportAsync(
                    authentication: authentication,
                    submission: new(subject, "tools.ozone.report.defs#reasonMisleadingSpam", reason),
                    expectedPolicyRevision: state.Revision,
                    cancellationToken: cancellationToken));
            }

            if (index <= 3)
            {
                LabelCaseReceipt opened = Require(await cases.ChangeCaseAsync(
                    caller,
                    new(
                        operationId: $"xenon-test-case-{index}",
                        subject: subject,
                        action: LabelCaseAction.Open,
                        reason: "Explicit development fixture setup."),
                    cancellationToken));
                Require(await cases.ChangeCaseAsync(
                    caller,
                    new(
                        operationId: $"xenon-test-workflow-{index}",
                        subject: subject,
                        action: index == 1 ? LabelCaseAction.Assign : index == 2 ? LabelCaseAction.StartReview : LabelCaseAction.Resolve,
                        reason: "Seed assigned, in-review and resolved workflow examples.",
                        caseId: opened.Case.Id,
                        expectedRevision: opened.Case.Revision,
                        assignee: index == 1 ? administrator : null),
                    cancellationToken));
            }
        }

        LabelIssuanceService issuance = new(
            issuer: FixtureHandler.s_issuer,
            signingKey: key,
            store: services.GetRequiredService<ILabelStore>(),
            authorizer: new LabelTeamAuthorizer(team, caller),
            policyProvider: verifier,
            httpClient: identity);
        Require(await issuance.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "xenon-test-label",
                target: LabelTarget.Account(FixtureHandler.Subject(1)),
                value: "spam",
                context: new(administrator, "Seed a fictional label and retained appeal provenance.")),
            cancellationToken));
        Require(await issuance.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "xenon-test-post-author-label",
                target: LabelTarget.Account(FixtureHandler.Subject(2)),
                value: "spam",
                context: new(administrator, "Seed an author account label separately from a post label.")),
            cancellationToken));
        Require(await issuance.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "xenon-test-post-label",
                target: LabelTarget.Record(new($"at://{FixtureHandler.Subject(2)}/app.bsky.feed.post/example"), new(FixtureHandler.Cid)),
                value: "scam",
                context: new(administrator, "Seed a label on this exact post version.")),
            cancellationToken));
        Require(await cases.ChangeCaseAsync(
            caller,
            new(
                operationId: "xenon-test-appeal",
                subject: LabelReportSubject.Account(FixtureHandler.Subject(1)),
                action: LabelCaseAction.OpenAppeal,
                reason: "Review this fictional captured label."),
            cancellationToken));

        const string automaticReason = "Xenon automatic decision fixture. Unsupported report reason (test data, not an allegation).";
        if (!existing.Contains(automaticReason))
        {
            const string unsupportedReason = "tools.ozone.report.defs#reasonMisleadingScam";
            if (state.Policy!.ReportScope.Accepts(LabelReportSubject.Account(FixtureHandler.Subject(1)), unsupportedReason))
            {
                throw new InvalidOperationException("The selected test policy accepts the automatic-decision fixture's reason; no example was submitted.");
            }
            state = Require(await verifier.GetVerifiedStateAsync(cancellationToken: cancellationToken));
            ModerationReportAuthenticationResult authentication = await ServiceAuthentication.VerifyModerationReportAsync(
                token: CreateReportToken(key),
                labeler: FixtureHandler.s_issuer,
                httpClient: identity,
                cancellationToken: cancellationToken);
            LabelModerationReport automatic = Require(await reports.SubmitReportAsync(
                authentication: authentication,
                submission: new(LabelReportSubject.Account(FixtureHandler.Subject(1)), unsupportedReason, automaticReason),
                expectedPolicyRevision: state.Revision,
                cancellationToken: cancellationToken));
            if (automatic.ResolutionReason != "Not Applicable")
            {
                throw new InvalidOperationException("The automatic-decision fixture was not handled as Not Applicable.");
            }
        }

        await InitializeAdministrationFixturesAsync(
            team: team,
            policies: policies,
            caller: caller,
            identity: identity,
            administrator: administrator,
            cancellationToken: cancellationToken);
    }

    /// <summary>Adds replay-safe fictional team states and unpublished policies without replacing operator changes.</summary>
    private static async Task InitializeAdministrationFixturesAsync(
        ILabelAdministrationStore team,
        ILabelPolicyStore policies,
        LabelAdministrationCaller caller,
        HttpClient identity,
        Did administrator,
        CancellationToken cancellationToken)
    {
        LabelTeamService enrollment = new(team, identity);
        foreach ((string name, LabelTeamRole role) in new[]
        {
            ("administrator", LabelTeamRole.Administrator),
            ("moderator", LabelTeamRole.Moderator),
            ("viewer", LabelTeamRole.Viewer),
            ("suspended", LabelTeamRole.Moderator),
            ("removed", LabelTeamRole.Viewer),
            ("local", LabelTeamRole.Viewer)
        })
        {
            Did member = FixtureHandler.TeamMember(name);
            if (member == administrator)
            {
                continue;
            }

            Require(await enrollment.EnrollAsync(
                caller: caller,
                member: member,
                role: role,
                operationId: $"xenon-test-team-enroll-{name}",
                reason: "Enroll a fictional local test member.",
                cancellationToken: cancellationToken));
            if (name is "suspended" or "removed" or "local")
            {
                Require(await team.ChangeTeamAsync(
                    caller: caller,
                    change: new(
                        operationId: $"xenon-test-team-state-{name}",
                        action: name switch
                        {
                            "suspended" => LabelTeamAction.Suspend,
                            "removed" => LabelTeamAction.Remove,
                            _ => LabelTeamAction.BindLocal
                        },
                        target: member,
                        reason: "Seed a fictional suspended, removed or local CLI identity example.",
                        credential: name == "local" ? "uid:4294967294" : null),
                    cancellationToken: cancellationToken));
            }
        }

        using JsonDocument declaration = JsonDocument.Parse(FixtureHandler.s_declaration);
        foreach ((string version, bool allowAccounts, string rules) in new[]
        {
            ("xenon-draft-accounts", true, "Fictional draft for account and record moderation. Review evidence before applying labels."),
            ("xenon-draft-records", false, "Fictional records-only draft. Account labels are disabled; review the exact record and optional CID.")
        })
        {
            Require(await policies.SavePolicyAsync(
                caller: caller,
                operationId: version,
                policy: new(
                    version: version,
                    declaration: declaration.RootElement,
                    allowAccounts: allowAccounts,
                    allowRecords: true,
                    rules: rules),
                reason: "Seed an unpublished fictional policy without changing the selected declaration.",
                cancellationToken: cancellationToken));
        }
    }

    /// <summary>Stops setup explicitly if a protected operation fails instead of masking an incomplete fixture.</summary>
    internal static T Require<T>(LabelServiceResult<T> result) where T : class =>
        result.Succeeded ? result.Result : throw new InvalidOperationException($"Test setup failed: {result.Error.Code}: {result.Error.Message}");

    /// <summary>Creates a fixture reporter token that is still verified cryptographically before report acceptance.</summary>
    private static string CreateReportToken(LabelSigningKey key)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string header = Encode("""{"typ":"JWT","alg":"ES256"}"""u8.ToArray());
        string body = Encode(Encoding.UTF8.GetBytes($$"""
            {"iss":"{{FixtureHandler.s_reporter}}","aud":"{{FixtureHandler.s_issuer}}#atproto_labeler",
             "lxm":"com.atproto.moderation.createReport","iat":{{now}},"exp":{{now + 60}},"jti":"{{Guid.NewGuid():N}}"}
            """));
        string input = header + "." + body;
        using ECDsa signer = ECDsa.Create();
        byte[] secret = key.ExportPkcs8PrivateKey();
        try
        {
            signer.ImportPkcs8PrivateKey(secret, out _);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        return input + "." + Encode(signer.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256));
    }

    /// <summary>Encodes one compact JWT segment without padding.</summary>
    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
