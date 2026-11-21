// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Cli;

internal static partial class Program
{
    private static readonly string[] s_policyPublicationMethods = ["beginPolicyPublication", "resumePolicyPublication", "getPolicy", "verifyPolicy"];

    /// <summary>Includes both SDK request/response envelopes and the raw declaration's extension-data contract.</summary>
    internal static JsonSerializerOptions PolicyRecordJsonOptions { get; } = AtProtoJsonSerializerOptions.Create(options =>
    {
        options.TypeInfoResolverChain.Add(PolicyJsonContext.Default);
        options.RespectNullableAnnotations = true;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

    /// <summary>Creates bounded policy inspection, explicit publication and conditional recovery commands.</summary>
    internal static Command CreatePolicyCommand()
    {
        Command command = new("policy", "Inspect and administer optional Xenon policy publication.");
        command.Subcommands.Add(CreatePolicyReadCommand("status", "getPolicy"));
        command.Subcommands.Add(CreatePolicyReadCommand("version", "getPolicyVersion"));
        command.Subcommands.Add(CreatePolicyReadCommand("versions", "getPolicyVersions"));
        command.Subcommands.Add(CreatePolicyReadCommand("history", "getPolicyHistory"));
        command.Subcommands.Add(CreatePolicyReadCommand("operation", "getPolicyOperation"));
        command.Subcommands.Add(CreatePolicyMutationCommand("save", "savePolicy"));
        command.Subcommands.Add(CreatePolicyMutationCommand("begin", "beginPolicyPublication"));
        command.Subcommands.Add(CreatePolicyMutationCommand("publish", "beginPolicyPublication"));
        command.Subcommands.Add(CreatePolicyMutationCommand("recover", "beginPolicyPublication"));
        command.Subcommands.Add(CreatePolicyMutationCommand("resume", "resumePolicyPublication"));
        command.Subcommands.Add(CreatePolicyMutationCommand("verify", "verifyPolicy"));
        command.Subcommands.Add(CreatePolicyMutationCommand("abandon", "abandonPolicyPublication"));
        return command;
    }

    /// <summary>Creates POST-based inspection without placing confidential filters in request URLs.</summary>
    private static Command CreatePolicyReadCommand(string name, string method)
    {
        Command command = new(name, name switch
        {
            "status" => "Show the current policy and its publication and verification state.",
            "version" => "Show one retained policy definition by its version ID.",
            "versions" => "List retained policy definitions, one page at a time.",
            "history" => "List policy administration and verification events in sequence.",
            "operation" => "Show the recorded result of a policy operation by its operation ID.",
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        });
        TransportOptions transport = AddTransportOptions(command);
        Option<string?> version = new("--version")
        {
            Description = "The exact retained policy version.",
            Required = name == "version"
        };
        Option<string?> operationId = new("--operation-id")
        {
            Description = "The policy operation identifier to inspect.",
            Required = name == "operation"
        };
        Option<string?> afterVersion = new("--after-version")
        {
            Description = "The exclusive policy version cursor."
        };
        Option<long> afterSequence = new("--after-sequence")
        {
            Description = "The nonnegative exclusive history sequence cursor; defaults to 0."
        };
        Option<int> limit = new("--limit")
        {
            Description = "The page size from 1 through 16; defaults to 8.",
            DefaultValueFactory = _ => 8
        };
        if (name == "version")
        {
            command.Options.Add(version);
        }
        if (name == "operation")
        {
            command.Options.Add(operationId);
        }
        if (name is "versions" or "history")
        {
            command.Options.Add(limit);
            command.Options.Add(name == "versions" ? afterVersion : afterSequence);
        }

        command.SetAction(async (parsed, token) =>
        {
            int pageSize = name is "versions" or "history" ? parsed.GetValue(limit) : 8;
            long after = name == "history" ? parsed.GetValue(afterSequence) : 0;
            string? selectedVersion = name == "version" ? parsed.GetValue(version) : null;
            string? operation = name == "operation" ? parsed.GetValue(operationId) : null;
            if (pageSize is < 1 or > 16 || after < 0 ||
                name == "version" && string.IsNullOrWhiteSpace(selectedVersion) ||
                name == "operation" && string.IsNullOrWhiteSpace(operation))
            {
                await Console.Error.WriteLineAsync("✖   Supply the requested identifier, a nonnegative sequence, and a page size from 1 through 16.").ConfigureAwait(false);
                return 2;
            }

            return await ExecuteAsync(
                method: method,
                request: new(
                    Method: method,
                    OperationId: operation ?? "",
                    Reason: "Policy inspection.",
                    AfterSequence: after,
                    Limit: pageSize,
                    Moderation: new(
                        PolicyVersion: selectedVersion,
                        AfterVersion: name == "versions" ? parsed.GetValue(afterVersion) : null)),
                transport: transport,
                parseResult: parsed,
                cancellationToken: token).ConfigureAwait(false);
        });
        return command;
    }

    /// <summary>Requires explicit intent, optimistic revision and confirmation before policy mutations.</summary>
    private static Command CreatePolicyMutationCommand(string name, string method)
    {
        bool publisher = name is "publish" or "recover";
        bool publication = publisher || name == "begin";
        bool requiresExistingOperation = name is "recover" or "resume";
        Command command = new(name, name switch
        {
            "save" => "Save or update an unpublished policy draft from a JSON file.",
            "begin" => "Begin publication, block reports and new issuance, and authorize withdrawal of removed label values; no PDS write.",
            "publish" => "Publish a saved policy with a conditional PDS write and separate labeler-account sign-in.",
            "recover" => "Recover an existing publication by its original operation ID; conditionally write or verify the PDS declaration.",
            "resume" => "Resume paused label withdrawal by renewing authorization; does not write to the PDS.",
            "verify" => "Verify the public declaration against the selected policy and update local state; --check-only is read-only.",
            "abandon" => "Abandon a pending publication; reports and new label issuance remain blocked. Does not undo withdrawals or PDS writes.",
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        });
        TransportOptions transport = AddTransportOptions(command);
        Option<string> reason = new("--reason")
        {
            Description = name == "verify"
                ? "A nonblank audit reason for state-changing verification; optional with --check-only. Do not include secrets."
                : "A nonblank audit reason for this policy operation; do not include secrets.",
            Required = name != "verify"
        };
        Option<bool> yes = new("--yes")
        {
            Description = "Skip the confirmation prompt; does not bypass authorization or credential-storage consent."
        };
        Option<bool> checkOnly = new("--check-only")
        {
            Description = "Compare the retained declaration with the public PDS record without changing local verification state."
        };
        Option<string> operationId = new("--operation-id")
        {
            Description = requiresExistingOperation
                ? "The original publication ID to resume or recover."
                : "A stable operation identifier; generated and displayed when omitted.",
            Required = requiresExistingOperation
        };
        Option<long> revision = new("--expected-revision")
        {
            Description = "The positive current policy state revision from policy status --verbose.",
            Required = publication || name == "abandon"
        };
        Option<string> version = new("--version")
        {
            Description = "The exact retained policy version to publish.",
            Required = publication
        };
        Option<string?> previousCid = new("--previous-cid")
        {
            Description = "The expected current declaration CID for an update; cannot be combined with --create."
        };
        Option<bool> create = new("--create")
        {
            Description = "Create the first declaration instead of specifying --previous-cid."
        };
        Option<FileInfo> file = new("--file")
        {
            Description = "The policy definition JSON file, at most 1 MiB.",
            Required = name == "save"
        };
        Option<string> labelerHandle = new("--labeler-handle")
        {
            Description = "The labeler repository account handle, separate from --admin-handle.",
            Required = publisher
        };
        Option<Uri?> labelerClientId = CreateUriOption("--labeler-oauth-client-id", "An optional native OAuth metadata URL for the labeler account; defaults to --oauth-client-id or <labeler-url>/native-client-metadata.json.");
        command.Options.Add(reason);
        command.Options.Add(yes);
        if (name == "verify")
        {
            command.Options.Add(checkOnly);
        }
        if (name != "verify")
        {
            command.Options.Add(operationId);
        }
        if (publication || name == "abandon")
        {
            command.Options.Add(revision);
        }
        if (publication)
        {
            command.Options.Add(version);
            command.Options.Add(previousCid);
            command.Options.Add(create);
        }
        if (name == "save")
        {
            command.Options.Add(file);
        }
        if (publisher)
        {
            command.Options.Add(labelerHandle);
            command.Options.Add(labelerClientId);
        }

        command.SetAction(async (parsed, token) =>
        {
            string? auditReason = parsed.GetValue(reason);
            bool readOnlyCheck = name == "verify" && parsed.GetValue(checkOnly);
            string operation = "";
            if (name != "verify")
            {
                string? suppliedOperation = parsed.GetValue(operationId);
                operation = requiresExistingOperation
                    ? suppliedOperation ?? ""
                    : await GetOrCreateOperationIdAsync(suppliedOperation, Console.Out).ConfigureAwait(false);
            }
            long expectedRevision = publication || name == "abandon" ? parsed.GetValue(revision) : 0;
            string? selectedVersion = publication ? parsed.GetValue(version) : null;
            string? previous = publication ? parsed.GetValue(previousCid) : null;
            bool creating = publication && parsed.GetValue(create);
            Handle? owner = null;
            string? ownerText = publisher ? parsed.GetValue(labelerHandle) : null;
            Uri? labelerOrigin = parsed.GetValue(transport.AdminUrl);
            Uri? metadata = publisher ? SelectPolicyClientMetadata(
                labelerMetadata: parsed.GetValue(labelerClientId),
                administratorMetadata: parsed.GetValue(transport.ClientId),
                origin: labelerOrigin) : null;
            if (!readOnlyCheck && string.IsNullOrWhiteSpace(auditReason))
            {
                await Console.Error.WriteLineAsync("✖   Supply a nonblank --reason for this policy operation.").ConfigureAwait(false);
                return 2;
            }
            if (readOnlyCheck && auditReason is not null && string.IsNullOrWhiteSpace(auditReason) ||
                name != "verify" && string.IsNullOrWhiteSpace(operation) ||
                (publication || name == "abandon") && expectedRevision <= 0 ||
                publication && (string.IsNullOrWhiteSpace(selectedVersion) ||
                    creating == (previous is not null) || previous is not null && !Cid.TryParse(previous, out _)) ||
                publisher && (ownerText is null || !Handle.TryParse(ownerText, out owner) ||
                    metadata is null || !IsNativeClientMetadataUri(metadata, new Uri(metadata.GetLeftPart(UriPartial.Authority)))))
            {
                await Console.Error.WriteLineAsync("✖   Supply complete policy intent; publication requires exactly one of --create or --previous-cid and a valid labeler OAuth identity.").ConfigureAwait(false);
                return 2;
            }

            PolicyCommand input = new(PolicyVersion: selectedVersion, ExpectedRevision: expectedRevision, PreviousCid: previous);
            if (name == "save")
            {
                try
                {
                    FileInfo? path = parsed.GetValue(file);
                    if (path is null)
                    {
                        return 2;
                    }
                    PolicyDefinition definition = await ReadPolicyFileAsync(path.FullName, token).ConfigureAwait(false);
                    LabelPolicyDefinition validated = definition.ToPolicy();
                    using JsonDocument declaration = JsonDocument.Parse(validated.DeclarationJson);
                    input = new(
                        PolicyVersion: definition.Version,
                        Declaration: declaration.RootElement.Clone(),
                        AllowAccounts: definition.AllowAccounts,
                        AllowRecords: definition.AllowRecords,
                        AllowedRecordCollections: definition.AllowedRecordCollections,
                        Rules: definition.Rules);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidDataException)
                {
                    await Console.Error.WriteLineAsync($"✖   Invalid policy file ({exception.GetType().Name}).").ConfigureAwait(false);
                    return 2;
                }
            }

            if (!readOnlyCheck && !await ConfirmAsync(parsed.GetValue(yes), GetPolicyConfirmationPrompt(name), token).ConfigureAwait(false))
            {
                return 2;
            }

            AdministrationCommand request = new(
                Method: readOnlyCheck ? "getPolicy" : method,
                OperationId: operation,
                Reason: auditReason ?? (readOnlyCheck ? "Read-only policy comparison." : ""),
                Moderation: input);
            AdministrationWorkflow? workflow = publisher
                ? (sender, cancellationToken) => PublishPolicyAsync(
                    sender: sender,
                    request: request,
                    owner: owner!,
                    clientId: metadata!,
                    labelerOrigin: labelerOrigin,
                    rememberMe: parsed.GetValue(transport.RememberMe),
                    cancellationToken: cancellationToken)
                : null;
            if (readOnlyCheck)
            {
                workflow = async (sender, cancellationToken) =>
                {
                    try
                    {
                        (PolicyCheckResult? result, int failure) = await CheckPublishedPolicyAsync(
                            sender: sender,
                            readPublicPolicy: ReadPublicPolicyDeclarationAsync,
                            reason: request.Reason,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (result is not null)
                        {
                            await WritePolicyCheckResultAsync(
                                result: result,
                                output: Console.Out,
                                verbose: s_verboseOutput.Value).ConfigureAwait(false);
                        }
                        return failure;
                    }
                    catch (HttpRequestException exception)
                    {
                        string status = exception.StatusCode is { } statusCode ? $" (HTTP {(int)statusCode})" : "";
                        await Console.Error.WriteLineAsync($"🌐⚠️  Public policy lookup failed{status}. Local verification state was not changed.").ConfigureAwait(false);
                        return 5;
                    }
                    catch (InvalidDataException exception)
                    {
                        await Console.Error.WriteLineAsync($"🌐⚠️  {exception.Message} Local verification state was not changed.").ConfigureAwait(false);
                        return 5;
                    }
                };
            }
            return await ExecuteAsync(
                method: readOnlyCheck ? "getPolicy" : method,
                request: request,
                transport: transport,
                parseResult: parsed,
                cancellationToken: token,
                workflow: workflow,
                workflowMethods: readOnlyCheck ? ["getPolicy"] : null).ConfigureAwait(false);
        });
        return command;
    }

    /// <summary>Distinguishes verification from publication and its potentially irreversible label withdrawals.</summary>
    internal static string GetPolicyConfirmationPrompt(string name) =>
        name switch
        {
            "verify" => "Verify the published policy and update Xenon's local verification state without changing the published policy?",
            "begin" or "publish" or "recover" or "resume" => "Authorize publication and irreversible batched negation of removed label values, even if publication later fails?",
            _ => "Change Xenon policy publication state?"
        };

    /// <summary>Reads the retained declaration and compares it with the public record without observing or persisting verification.</summary>
    internal static async Task<(PolicyCheckResult? Result, int Failure)> CheckPublishedPolicyAsync(
        AdministrationSender sender,
        Func<Did, CancellationToken, Task<PublicPolicyDeclaration>> readPublicPolicy,
        string reason,
        CancellationToken cancellationToken)
    {
        AdministrationReply reply = await sender(
            new(Method: "getPolicy", Reason: reason),
            cancellationToken).ConfigureAwait(false);
        (PolicyState? state, int failure) = await ReadPolicyReplyAsync(reply).ConfigureAwait(false);
        if (state is null)
        {
            return (null, failure);
        }
        if (state.Definition is not { } definition)
        {
            return (new("NoSelectedPolicy", state.Issuer, null), 5);
        }

        Did issuer = new(state.Issuer);
        PublicPolicyDeclaration observed = await readPublicPolicy(issuer, cancellationToken).ConfigureAwait(false);
        bool matches = observed.Exists &&
            JsonNode.DeepEquals(JsonNode.Parse(definition.Declaration.GetRawText()), observed.Declaration);
        string status;
        if (!observed.Exists)
        {
            status = "Missing";
        }
        else
        {
            status = matches ? "Matches" : "Mismatch";
        }
        return (
            new(status, state.Issuer, observed.Cid),
            matches ? 0 : 5);
    }

    /// <summary>Displays the read-only comparison with outcome-specific icons or as structured JSON.</summary>
    internal static async Task WritePolicyCheckResultAsync(PolicyCheckResult result, TextWriter output, bool verbose)
    {
        if (verbose)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(result, PolicyJsonContext.Default.PolicyCheckResult)).ConfigureAwait(false);
            return;
        }

        string status = result.Status switch
        {
            "Matches" => "✅  Status: Matches",
            "Mismatch" => "📄⚠️  Status: Mismatch",
            "Missing" => "🔎❌  Status: Public declaration missing",
            "NoSelectedPolicy" => "📋❌  Status: No selected policy",
            _ => throw new InvalidDataException("Unknown policy check status.")
        };
        await output.WriteLineAsync(status).ConfigureAwait(false);
        await output.WriteLineAsync($"🔗  Issuer DID: {SanitizeConsoleText(result.Issuer)}").ConfigureAwait(false);
        await output.WriteLineAsync($"🆔  Observed CID: {SanitizeConsoleText(result.ObservedCid ?? "(none)")}").ConfigureAwait(false);
    }

    /// <summary>Resolves the issuer's current PDS record using public, unauthenticated reads only.</summary>
    private static async Task<PublicPolicyDeclaration> ReadPublicPolicyDeclarationAsync(Did issuer, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = new() { Timeout = s_remoteRequestTimeout };
        Uri pds = await IdentityResolution.ResolvePdsAsync(
            did: issuer,
            httpClient: httpClient,
            timeout: s_remoteRequestTimeout,
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The labeler account's PDS could not be resolved.");

        AtUri expectedUri = new($"at://{issuer}/app.bsky.labeler.service/self");
        AtProtoHttpResult<AtProtoRepositoryRecord> response = await AtProtoServer.GetRecord(
            repo: issuer,
            collection: new("app.bsky.labeler.service"),
            rKey: new("self"),
            cid: null,
            service: pds,
            accessCredentials: null,
            httpClient: httpClient,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        bool missing = !response.Succeeded &&
            (response.StatusCode == HttpStatusCode.NotFound || response.AtErrorDetail?.Error == "RecordNotFound");
        if (missing)
        {
            return new(false, null, null);
        }
        if (!response.Succeeded)
        {
            throw new HttpRequestException("The public policy record request failed.", null, response.StatusCode);
        }
        if (response.Result.Uri != expectedUri || response.Result.Value is null)
        {
            throw new InvalidDataException("The PDS returned a policy record that does not match the labeler account.");
        }

        return new(true, response.Result.Cid.ToString(), response.Result.Value);
    }

    /// <summary>Defaults labeler publication to the same native metadata without hiding malformed explicit configuration.</summary>
    internal static Uri? SelectPolicyClientMetadata(Uri? labelerMetadata, string? administratorMetadata, Uri? origin)
    {
        if (labelerMetadata is not null)
        {
            return labelerMetadata;
        }
        if (administratorMetadata is not null)
        {
            return Uri.TryCreate(administratorMetadata, UriKind.Absolute, out Uri? metadata) ? metadata : null;
        }

        return origin is not null && IsHttpsOrigin(origin)
            ? new Uri(origin, "/native-client-metadata.json")
            : null;
    }

    /// <summary>Reads bounded operator-authored intent and rejects ambiguous envelope fields.</summary>
    private static async Task<PolicyDefinition> ReadPolicyFileAsync(string path, CancellationToken token)
    {
        const int maximumBytes = 1024 * 1024;
        FileStream stream = File.OpenRead(path);
        await using ConfiguredAsyncDisposable streamLifetime = stream.ConfigureAwait(false);
        byte[] bytes = new byte[maximumBytes + 1];
        int count = await stream.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellationToken: token).ConfigureAwait(false);
        if (count > maximumBytes)
        {
            throw new InvalidDataException("The policy file exceeds one MiB.");
        }

        using JsonDocument document = JsonDocument.Parse(bytes.AsMemory(0, count), new() { MaxDepth = 34 });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The policy envelope must be an object.");
        }
        HashSet<string> fields = [with(StringComparer.Ordinal)];
        if (document.RootElement.EnumerateObject().Any(field => !fields.Add(field.Name)))
        {
            throw new InvalidDataException("Duplicate policy envelope fields are not supported.");
        }
        return document.RootElement.Deserialize(PolicyJsonContext.Default.PolicyDefinition)
            ?? throw new InvalidDataException("A complete policy definition is required.");
    }

    /// <summary>Requests the declaration actions required by createRecord or putRecord, never delete, account, identity or general repository authority.</summary>
    internal static OAuthOptions CreatePolicyOAuthOptions(Uri clientId, bool create) => new()
    {
        ClientId = clientId.AbsoluteUri,
        Scopes = create
            ? ["atproto", "repo:app.bsky.labeler.service?action=create"]
            : ["atproto", "repo:app.bsky.labeler.service?action=create", "repo:app.bsky.labeler.service?action=update"]
    };

    /// <summary>Preserves the exact validated record fields instead of injecting typed DTO defaults.</summary>
    internal static AtProtoRecord CreatePolicyPublicationRecord(LabelPolicyDefinition policy) =>
        JsonSerializer.Deserialize(policy.DeclarationJson, PolicyJsonContext.Default.AtProtoRecord)
            ?? throw new InvalidDataException("The declaration could not be encoded.");

    /// <summary>Closes the durable gate before conditional PDS mutation and leaves uncertainty explicitly recoverable.</summary>
    private static async Task<int> PublishPolicyAsync(
        AdministrationSender sender,
        AdministrationCommand request,
        Handle owner,
        Uri clientId,
        Uri? labelerOrigin,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        (PolicyState? intent, int failure) = await ReadPolicyReplyAsync(await sender(request, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        if (intent is null)
        {
            return failure;
        }

        (PolicyState? current, failure) = await WaitForPolicyWithdrawalAsync(
            sender: sender,
            reason: request.Reason,
            intent: intent,
            clock: TimeProvider.System,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return failure;
        }
        if (!MatchesPolicyPublication(current, intent))
        {
            await Console.Error.WriteLineAsync("✖   Publication intent was abandoned or superseded; inspect current policy and retained history. No PDS write was attempted.").ConfigureAwait(false);
            return 5;
        }
        if (current.Phase == nameof(LabelPolicyPhase.VerifiedActive))
        {
            return await VerifyPublishedPolicyAsync(sender, request.Reason, intent, cancellationToken).ConfigureAwait(false);
        }

        OAuthOptions oauth = CreatePolicyOAuthOptions(clientId, intent.PreviousCid is null);
        CliSignInLifetime signInLifetime = new();
        await using ConfiguredAsyncDisposable signInCleanup = signInLifetime.ConfigureAwait(false);
        using AtProtoAgent labeler = new(new Uri(DefaultPds), new AtProtoAgentOptions { OAuthOptions = oauth });
        if (!await SignInAsync(
            agent: labeler,
            handle: owner,
            options: oauth,
            purpose: "Labeler-account authority to conditionally publish app.bsky.labeler.service/self. This is separate from administrator sign-in.",
            rememberMe: rememberMe,
            labelerOrigin: labelerOrigin,
            lifetime: signInLifetime,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return 4;
        }
        if (labeler.Did?.Value != intent.Issuer)
        {
            await Console.Error.WriteLineAsync("✖   The authenticated labeler account does not match the issuer bound to the moderation database. No PDS write was attempted.").ConfigureAwait(false);
            return 4;
        }

        // Recheck live administrator authority and gate selection after the separate browser sign-in.
        (PolicyState? authorized, failure) = await ReadPolicyReplyAsync(await sender(request, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        if (authorized is null)
        {
            return failure;
        }
        (current, failure) = await ReadPolicyReplyAsync(await sender(
            new(Method: "getPolicy", Reason: request.Reason), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        if (current is null)
        {
            return failure;
        }
        if (!MatchesPolicyPublication(current, intent))
        {
            await Console.Error.WriteLineAsync("✖   Current publication selection changed during sign-in. No PDS write was attempted.").ConfigureAwait(false);
            return 5;
        }
        if (current.Phase == nameof(LabelPolicyPhase.VerifiedActive))
        {
            return await VerifyPublishedPolicyAsync(sender, request.Reason, intent, cancellationToken).ConfigureAwait(false);
        }

        LabelPolicyDefinition policy = intent.Definition!.ToPolicy();
        AtProtoHttpResult<AtProtoRepositoryRecord> existing = await labeler.GetRawRecord(
            repo: labeler.Did, collection: new("app.bsky.labeler.service"), rKey: new("self"),
            cid: null, service: labeler.Service, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (existing.Succeeded)
        {
            if (existing.Result.Value is null)
            {
                throw new InvalidDataException("The PDS returned no declaration value.");
            }
            if (JsonNode.DeepEquals(existing.Result.Value, JsonNode.Parse(policy.DeclarationJson)))
            {
                return await VerifyPublishedPolicyAsync(sender, request.Reason, intent, cancellationToken).ConfigureAwait(false);
            }
            if (intent.PreviousCid != existing.Result.Cid.ToString())
            {
                await Console.Error.WriteLineAsync("✖   The authoritative declaration differs from the expected previous CID. No overwrite was attempted.").ConfigureAwait(false);
                return 5;
            }
        }
        else if (existing.AtErrorDetail?.Error != "RecordNotFound" || intent.PreviousCid is not null)
        {
            await Console.Error.WriteLineAsync($"✖   The expected declaration could not be read ({(int)existing.StatusCode}). No PDS write was attempted.").ConfigureAwait(false);
            return 5;
        }

        (authorized, failure) = await ReadPolicyReplyAsync(await sender(request, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        if (authorized is null)
        {
            return failure;
        }
        (current, failure) = await ReadPolicyReplyAsync(await sender(
            new(Method: "getPolicy", Reason: request.Reason), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        if (current is null)
        {
            return failure;
        }
        if (!MatchesPolicyPublication(current, intent))
        {
            await Console.Error.WriteLineAsync("✖   Publication selection changed during the authoritative read. No PDS write was attempted.").ConfigureAwait(false);
            return 5;
        }
        if (current.Phase == nameof(LabelPolicyPhase.VerifiedActive))
        {
            return await VerifyPublishedPolicyAsync(sender, request.Reason, intent, cancellationToken).ConfigureAwait(false);
        }

        await Console.Error.WriteLineAsync("➜   Submitting a conditional declaration write. If interrupted, inspect the retained operation and authoritative record before recovery; cancellation does not undo a PDS write.").ConfigureAwait(false);
        (bool succeeded, HttpStatusCode status, string? error) = await WritePolicyRecordAsync(
            labeler: labeler, policy: policy, previousCid: intent.PreviousCid, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!succeeded)
        {
            await Console.Error.WriteLineAsync($"✖   The conditional PDS write did not confirm success ({(int)status}, {error ?? "no AT error"}). Inspect the retained gate and declaration, and recover the same operation; never overwrite a conflict.").ConfigureAwait(false);
            return 5;
        }

        return await VerifyPublishedPolicyAsync(sender, request.Reason, intent, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Uses create-only or record-CID compare-and-swap, never an unconditional replacement.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "PolicyRecordJsonOptions uses source-generated metadata for the exact AtProtoRecord contract.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "PolicyRecordJsonOptions uses source-generated metadata for the exact AtProtoRecord contract.")]
    internal static async Task<(bool Succeeded, HttpStatusCode Status, string? Error)> WritePolicyRecordAsync(
        AtProtoAgent labeler,
        LabelPolicyDefinition policy,
        string? previousCid,
        CancellationToken cancellationToken)
    {
        AtProtoRecord record = CreatePolicyPublicationRecord(policy);
        if (previousCid is null)
        {
            AtProtoHttpResult<CreateRecordResult> created = await labeler.CreateRecord(
                record: record,
                jsonSerializerOptions: PolicyRecordJsonOptions,
                collection: new("app.bsky.labeler.service"),
                rKey: new("self"),
                validate: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return (created.Succeeded, created.StatusCode, created.AtErrorDetail?.Error);
        }

        AtProtoHttpResult<PutRecordResult> updated = await labeler.PutRecord(
            record: record,
            jsonSerializerOptions: PolicyRecordJsonOptions,
            collection: new("app.bsky.labeler.service"),
            rKey: new("self"),
            validate: true,
            swapRecord: new(previousCid),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return (updated.Succeeded, updated.StatusCode, updated.AtErrorDetail?.Error);
    }

    /// <summary>Rejects abandoned or replaced publication intent even when the selected version is unchanged.</summary>
    internal static bool MatchesPolicyPublication(PolicyState current, PolicyState intent) =>
        MatchesPolicySelection(current, intent) &&
        current.Phase is nameof(LabelPolicyPhase.Publishing) or nameof(LabelPolicyPhase.VerifiedActive);

    /// <summary>Compares exact publication identity independently of whether cleanup has completed.</summary>
    private static bool MatchesPolicySelection(PolicyState current, PolicyState intent) =>
        intent.PublicationOperationId is not null &&
        current.PublicationOperationId == intent.PublicationOperationId &&
        current.Issuer == intent.Issuer &&
        current.Definition is not null && intent.Definition is not null &&
        current.Definition.Version == intent.Definition.Version &&
        current.PreviousCid == intent.PreviousCid;

    /// <summary>Waits for durable batch publication without signing in as the labeler or writing the declaration early.</summary>
    internal static async Task<(PolicyState? State, int Failure)> WaitForPolicyWithdrawalAsync(
        AdministrationSender sender,
        string reason,
        PolicyState intent,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            (PolicyState? current, int failure) = await ReadPolicyReplyAsync(await sender(
                new(Method: "getPolicy", Reason: reason), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
            if (current is null)
            {
                return (null, failure);
            }
            if (!MatchesPolicySelection(current, intent) || current.Phase != nameof(LabelPolicyPhase.Withdrawing))
            {
                return (current, 0);
            }
            if (current.Withdrawal?.PauseReason is not null)
            {
                (_, failure) = await ReadPolicyReplyAsync(await sender(new(
                    Method: "resumePolicyPublication",
                    OperationId: intent.PublicationOperationId!,
                    Reason: reason), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
                if (failure != 0)
                {
                    return (null, failure);
                }
            }
            await Console.Error.WriteLineAsync("⚠   Waiting for removed-label negations to be committed and published. Interrupting does not undo committed negations; recover the same publication operation.").ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(5), clock, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Requires independent verification of this exact publication rather than accepting any active policy.</summary>
    private static async Task<int> VerifyPublishedPolicyAsync(
        AdministrationSender sender,
        string reason,
        PolicyState intent,
        CancellationToken token)
    {
        AdministrationReply reply = await sender(new(Method: "verifyPolicy", Reason: reason), token).ConfigureAwait(false);
        (PolicyState? verified, int failure) = await ReadPolicyReplyAsync(reply).ConfigureAwait(false);
        if (verified is null)
        {
            return failure;
        }
        if (!MatchesPolicyPublication(verified, intent) || verified.Phase != nameof(LabelPolicyPhase.VerifiedActive))
        {
            await Console.Error.WriteLineAsync("✖   Independent verification did not activate this exact intent. Inspect retained publication history; do not repeat an unconditional write.").ConfigureAwait(false);
            return 5;
        }
        return await WriteResponseAsync(reply.Body, reply.StatusCode, token).ConfigureAwait(false);
    }

    /// <summary>Checks classified administrative outcomes before consuming their typed policy payload.</summary>
    private static async Task<(PolicyState? State, int Failure)> ReadPolicyReplyAsync(AdministrationReply reply)
    {
        using JsonDocument document = JsonDocument.Parse(reply.Body);
        if ((int)reply.StatusCode is < 200 or >= 300 ||
            !document.RootElement.TryGetProperty("succeeded", out JsonElement succeeded) || succeeded.ValueKind != JsonValueKind.True)
        {
            int failure = await WriteResponseAsync(reply.Body, reply.StatusCode).ConfigureAwait(false);
            return (null, failure == 0 ? 5 : failure);
        }
        if (!document.RootElement.TryGetProperty("moderation", out JsonElement moderation) ||
            moderation.ValueKind != JsonValueKind.Object ||
            !moderation.TryGetProperty("policy", out JsonElement policy) || policy.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The administration service returned no policy state.");
        }
        PolicyState state = policy
            .Deserialize(PolicyJsonContext.Default.PolicyState) ?? throw new InvalidDataException("A policy state is required.");
        return (state, 0);
    }

    /// <summary>Contains policy-only protocol inputs without caller identity or credentials.</summary>
    internal sealed record PolicyCommand(
        string? PolicyVersion = null,
        JsonElement? Declaration = null,
        bool AllowAccounts = false,
        bool AllowRecords = false,
        string[]? AllowedRecordCollections = null,
        string? Rules = null,
        string? PreviousCid = null,
        long ExpectedRevision = 0,
        string? AfterVersion = null);

    /// <summary>Retains complete declaration intent and independent issuance permissions.</summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    internal sealed record PolicyDefinition(
        [property: JsonRequired] string Version,
        [property: JsonRequired] JsonElement Declaration,
        [property: JsonRequired] bool AllowAccounts,
        [property: JsonRequired] bool AllowRecords,
        [property: JsonRequired] string Rules,
        string[]? AllowedRecordCollections = null)
    {
        /// <summary>Applies the same explicit publication checks used by the host before closing its gate.</summary>
        internal LabelPolicyDefinition ToPolicy() => new(
            version: Version, declaration: Declaration, allowAccounts: AllowAccounts, allowRecords: AllowRecords,
            rules: Rules, allowedRecordCollections: AllowedRecordCollections?.Select(value => new Nsid(value)).ToArray());
    }

    /// <summary>Reads live selection identity separately from historical operation receipts.</summary>
    internal sealed record PolicyState(
        [property: JsonRequired] string Issuer,
        [property: JsonRequired] string Phase,
        string? PublicationOperationId = null,
        PolicyDefinition? Definition = null,
        string? PreviousCid = null,
        PolicyWithdrawal? Withdrawal = null);

    /// <summary>Reports a read-only comparison with the public declaration.</summary>
    internal sealed record PolicyCheckResult(string Status, string Issuer, string? ObservedCid);

    /// <summary>Contains a public PDS declaration read without changing the local policy gate.</summary>
    internal sealed record PublicPolicyDeclaration(bool Exists, string? Cid, JsonNode? Declaration);

    /// <summary>Reads resumable authorization failures without treating cleanup as a declaration-ready publication.</summary>
    internal sealed record PolicyWithdrawal(string? PauseReason = null);

    /// <summary>Provides reflection-free record and policy serialization without inserting declaration defaults.</summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
    [JsonSerializable(typeof(PolicyDefinition))]
    [JsonSerializable(typeof(PolicyState))]
    [JsonSerializable(typeof(PolicyWithdrawal))]
    [JsonSerializable(typeof(PolicyCheckResult))]
    [JsonSerializable(typeof(AtProtoRecord))]
    internal sealed partial class PolicyJsonContext : JsonSerializerContext;
}
