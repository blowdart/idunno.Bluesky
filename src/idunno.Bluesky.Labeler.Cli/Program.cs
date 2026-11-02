// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Identity;

namespace idunno.Bluesky.Labeler.Cli;

internal static partial class Program
{
    private const string DefaultPds = "https://bsky.social";
    private const string AdminMethodPrefix = "dev.idunno.labeler.admin.";
    private static readonly TimeSpan s_remoteRequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly AsyncLocal<bool> s_verboseOutput = new();

    /// <summary>Builds the command tree and runs the requested labeler administration operation.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            ParseResult parsed = CreateRootCommand().Parse(args);
            s_verboseOutput.Value = parsed.GetValue<bool>("--verbose");
            return await parsed.InvokeAsync(
                new InvocationConfiguration { EnableDefaultExceptionHandler = false }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("⚠   Operation cancelled. No further workflow steps will run.").ConfigureAwait(false);
            return 130;
        }
        catch (OAuthException exception)
        {
            await Console.Error.WriteLineAsync($"✖   OAuth authentication failed ({ClassifyOAuthFailure(exception)}). No further workflow steps will run.").ConfigureAwait(false);
            return 4;
        }
        catch (ArgumentException)
        {
            await Console.Error.WriteLineAsync("✖   Invalid command input. No further workflow steps will run.").ConfigureAwait(false);
            return 2;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException or
                                           InvalidDataException or JsonException or InvalidOperationException or
                                           PlatformNotSupportedException or System.Security.Cryptography.CryptographicException)
        {
            await Console.Error.WriteLineAsync($"✖   Command failed ({exception.GetType().Name}). No further workflow steps will run.").ConfigureAwait(false);
            return 3;
        }
        finally
        {
            s_verboseOutput.Value = false;
        }
    }

    /// <summary>Creates independently parseable commands with the same options used by the executable.</summary>
    internal static RootCommand CreateRootCommand()
    {
        RootCommand root = new(
            "Administer a standalone AT Protocol labeler. Remote OAuth credentials are temporary by default; use --remember-me on a remote command to opt into account- and permission-scoped saving. Windows uses current-user DPAPI; other platforms require separate consent to save credentials unencrypted.");
        root.Options.Add(new Option<bool>("--verbose")
        {
            Description = "Include raw JSON responses instead of human-readable result details.",
            Recursive = true
        });
        root.Subcommands.Add(CreateTeamCommand());
        root.Subcommands.Add(CreateStatusCommand());
        root.Subcommands.Add(CreateCountsCommand());
        root.Subcommands.Add(CreateOperationCommand());
        root.Subcommands.Add(CreateAuditCommand());
        root.Subcommands.Add(CreateIssueCommand(negate: false));
        root.Subcommands.Add(CreateIssueCommand(negate: true));
        root.Subcommands.Add(CreateEnrollCommand());
        root.Subcommands.Add(CreateChangeTeamCommand());
        root.Subcommands.Add(CreateRetirementCommand());
        root.Subcommands.Add(CreateRotationCommand());
        root.Subcommands.Add(CreateCliUpdateCommand());
        root.Subcommands.Add(CreateServiceUpdateCommand());
        root.Subcommands.Add(CreatePolicyCommand());
        root.Subcommands.Add(CreateForgetMeCommand());
        return root;
    }

    /// <summary>Creates a command that removes every locally remembered OAuth credential.</summary>
    private static Command CreateForgetMeCommand()
    {
        Command command = new("forget-me", "Delete all saved OAuth credentials from ~/.xenon.");
        command.SetAction(async (_, cancellationToken) =>
        {
            int deleted = await CliCredentialCache.DeleteAllAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            string message = deleted switch
            {
                0 => "ℹ   No saved OAuth credentials were found.",
                1 => "✔   Deleted 1 saved OAuth credential.",
                _ => $"✔   Deleted {deleted} saved OAuth credentials."
            };
            await Console.Error.WriteLineAsync(message).ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    /// <summary>Creates a team-inspection command.</summary>
    private static Command CreateTeamCommand()
    {
        Command command = new("team", "Show the authorized labeler team.");
        TransportOptions transport = AddTransportOptions(command);
        command.SetAction((parseResult, cancellationToken) => ExecuteAsync(
            method: "getTeam",
            request: new(Method: "getTeam", Reason: "Administrative inspection."),
            transport: transport,
            parseResult: parseResult,
            cancellationToken: cancellationToken));
        return command;
    }

    /// <summary>Creates an operation-receipt lookup command.</summary>
    private static Command CreateOperationCommand()
    {
        Command command = new("operation", "Show the durable receipt for an operation ID.");
        TransportOptions transport = AddTransportOptions(command);
        Option<string> operationId = new("--operation-id")
        {
            Description = "The operation ID to look up.",
            Required = true
        };
        Option<string?> reason = new("--reason") { Description = "The operator's audit reason." };
        command.Options.Add(operationId);
        command.Options.Add(reason);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string? operation = parseResult.GetValue(operationId);
            if (string.IsNullOrWhiteSpace(operation))
            {
                await Console.Error.WriteLineAsync("✖   Supply a non-empty operation ID.").ConfigureAwait(false);
                return 2;
            }

            return await ExecuteAsync(
                method: "getOperation",
                request: new(
                    Method: "getOperation",
                    OperationId: operation,
                    Reason: parseResult.GetValue(reason) ?? "Administrative inspection."),
                transport: transport,
                parseResult: parseResult,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    /// <summary>Creates a label publication-status command.</summary>
    private static Command CreateStatusCommand()
    {
        Command command = new("status", "Show label publication and lifecycle status.");
        TransportOptions transport = AddTransportOptions(command);
        command.SetAction((parseResult, cancellationToken) => ExecuteAsync(
            method: "getPublicationState",
            request: new(Method: "getPublicationState", Reason: "Administrative inspection."),
            transport: transport,
            parseResult: parseResult,
            cancellationToken: cancellationToken));
        return command;
    }

    /// <summary>Creates a command that displays unresolved moderation work counts.</summary>
    private static Command CreateCountsCommand()
    {
        Command command = new("counts", "Show the number of unresolved cases, reports, and appeals.");
        TransportOptions transport = AddTransportOptions(command);
        command.SetAction((parseResult, cancellationToken) => ExecuteAsync(
            method: "getModerationOverview",
            request: new(Method: "getModerationOverview", Reason: "Administrative inspection."),
            transport: transport,
            parseResult: parseResult,
            cancellationToken: cancellationToken));
        return command;
    }

    /// <summary>Creates an audit-feed inspection command.</summary>
    private static Command CreateAuditCommand()
    {
        Command command = new("audit", "Read a page of administrative audit events.");
        TransportOptions transport = AddTransportOptions(command);
        Option<long> afterSequence = new("--after-sequence")
        {
            Description = "Read events after this exclusive sequence number.",
            DefaultValueFactory = _ => 0
        };
        Option<int> limit = new("--limit")
        {
            Description = "The page size from 1 through 16.",
            DefaultValueFactory = _ => 16
        };
        command.Options.Add(afterSequence);
        command.Options.Add(limit);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            long sequence = parseResult.GetValue(afterSequence);
            int pageSize = parseResult.GetValue(limit);
            if (sequence < 0 || pageSize is < 1 or > 16)
            {
                await Console.Error.WriteLineAsync("✖   Audit sequence must be nonnegative and limit must be from 1 through 16.").ConfigureAwait(false);
                return 2;
            }

            return await ExecuteAsync(
                method: "getAudit",
                request: new(Method: "getAudit", Reason: "Administrative inspection.", AfterSequence: sequence, Limit: pageSize),
                transport: transport,
                parseResult: parseResult,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    /// <summary>Creates a label issuance or negation command with explicit confirmation.</summary>
    private static Command CreateIssueCommand(bool negate)
    {
        string method = negate ? "negateLabel" : "issueLabel";
        Command command = new(negate ? "negate" : "issue", negate ? "Withdraw a label." : "Issue a label.");
        TransportOptions transport = AddTransportOptions(command);
        Argument<string> target = new("target") { Description = "A DID/handle or record AT URI." };
        Argument<string> value = new("value") { Description = "The declared label value." };
        Option<string> operationId = new("--operation-id") { Description = "A stable identifier for safe retries.", Required = true };
        Option<string> reason = new("--reason") { Description = "The operator's audit reason.", Required = true };
        Option<string?> cid = new("--cid") { Description = "Optionally pin a record label to this CID." };
        Option<DateTimeOffset?> expiresAt = new("--expires-at") { Description = "An optional absolute expiry time (issue only)." };
        Option<bool> yes = new("--yes") { Description = "Confirm without an interactive prompt." };
        command.Arguments.Add(target);
        command.Arguments.Add(value);
        command.Options.Add(operationId);
        command.Options.Add(reason);
        command.Options.Add(cid);
        if (!negate)
        {
            command.Options.Add(expiresAt);
        }

        command.Options.Add(yes);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string? targetText = parseResult.GetValue(target);
            string? label = parseResult.GetValue(value);
            string? operation = parseResult.GetValue(operationId);
            string? auditReason = parseResult.GetValue(reason);
            string? cidText = parseResult.GetValue(cid);
            DateTimeOffset? expiry = negate ? null : parseResult.GetValue(expiresAt);
            if (targetText is null ||
                label is null ||
                operation is null ||
                auditReason is null ||
                !ValidateLabelTarget(targetText, cidText) ||
                string.IsNullOrWhiteSpace(label) ||
                string.IsNullOrWhiteSpace(operation) ||
                string.IsNullOrWhiteSpace(auditReason))
            {
                await Console.Error.WriteLineAsync("✖   Supply a valid target, declared label value, operation ID, and audit reason.").ConfigureAwait(false);
                return 2;
            }

            if (!await ConfirmAsync(parseResult.GetValue(yes), $"{(negate ? "Withdraw" : "Issue")} '{label}' for '{targetText}'?", cancellationToken).ConfigureAwait(false))
            {
                return 2;
            }

            return await ExecuteAsync(
                method: method,
                request: new(
                    Method: method,
                    OperationId: operation,
                    Reason: auditReason,
                    Target: targetText,
                    Value: label,
                    Cid: cidText,
                    ExpiresAt: expiry),
                transport: transport,
                parseResult: parseResult,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    /// <summary>Creates a verified team-enrollment command.</summary>
    private static Command CreateEnrollCommand()
    {
        Command command = new("enroll", "Enroll a verified DID or handle in the labeler team.");
        TransportOptions transport = AddTransportOptions(command);
        Argument<string> target = new("target") { Description = "The member DID or handle." };
        Option<string> role = new("--role") { Description = "Viewer, Moderator, or Administrator.", Required = true };
        Option<string> operationId = new("--operation-id") { Description = "A stable identifier for safe retries.", Required = true };
        Option<string> reason = new("--reason") { Description = "The operator's audit reason.", Required = true };
        Option<bool> yes = new("--yes") { Description = "Confirm without an interactive prompt." };
        command.Arguments.Add(target);
        command.Options.Add(role);
        command.Options.Add(operationId);
        command.Options.Add(reason);
        command.Options.Add(yes);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string? member = parseResult.GetValue(target);
            string? operation = parseResult.GetValue(operationId);
            string? auditReason = parseResult.GetValue(reason);
            if (member is null ||
                string.IsNullOrWhiteSpace(operation) ||
                string.IsNullOrWhiteSpace(auditReason) ||
                !AtIdentifier.TryParse(member, out _) ||
                !TryParseRole(parseResult.GetValue(role), out LabelTeamRole parsedRole))
            {
                await Console.Error.WriteLineAsync("✖   Supply a valid DID/handle, operation ID, audit reason, and role: Viewer, Moderator, or Administrator.").ConfigureAwait(false);
                return 2;
            }

            if (!await ConfirmAsync(parseResult.GetValue(yes), $"Enroll '{member}' as {parsedRole}?", cancellationToken).ConfigureAwait(false))
            {
                return 2;
            }

            return await ExecuteAsync(
                method: "enroll",
                request: new(
                    Method: "enroll",
                    OperationId: operation,
                    Reason: auditReason,
                    Target: member,
                    Role: parsedRole),
                transport: transport,
                parseResult: parseResult,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    /// <summary>Creates a team role, suspension, reinstatement, removal, or local-binding command.</summary>
    private static Command CreateChangeTeamCommand()
    {
        Command command = new("change-team", "Change a member's role, status, or local binding.");
        TransportOptions transport = AddTransportOptions(command);
        Argument<string> target = new("target") { Description = "The target member's DID." };
        Option<string> action = new("--action")
        {
            Description = "SetRole, Suspend, Reinstate, Remove, BindLocal, or RevokeLocal.",
            Required = true
        };
        Option<string?> role = new("--role") { Description = "The new role for SetRole." };
        Option<string?> credential = new("--credential") { Description = "The OS identity for BindLocal or RevokeLocal." };
        Option<string> operationId = new("--operation-id") { Description = "A stable identifier for safe retries.", Required = true };
        Option<string> reason = new("--reason") { Description = "The operator's audit reason.", Required = true };
        Option<bool> yes = new("--yes") { Description = "Confirm without an interactive prompt." };
        command.Arguments.Add(target);
        command.Options.Add(action);
        command.Options.Add(role);
        command.Options.Add(credential);
        command.Options.Add(operationId);
        command.Options.Add(reason);
        command.Options.Add(yes);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string? member = parseResult.GetValue(target);
            string? operation = parseResult.GetValue(operationId);
            string? auditReason = parseResult.GetValue(reason);
            if (member is null ||
                string.IsNullOrWhiteSpace(operation) ||
                string.IsNullOrWhiteSpace(auditReason) ||
                !Did.TryParse(member, out Did? did) ||
                !TryParseAction(parseResult.GetValue(action), out LabelTeamAction parsedAction))
            {
                await Console.Error.WriteLineAsync("✖   Supply a member DID, operation ID, audit reason, and supported team action.").ConfigureAwait(false);
                return 2;
            }

            LabelTeamRole? parsedRole = null;
            string? roleText = parseResult.GetValue(role);
            string? credentialText = parseResult.GetValue(credential);
            if (!HasValidTeamChangeOptions(parsedAction, roleText is not null, credentialText is not null))
            {
                await Console.Error.WriteLineAsync("✖   SetRole requires --role; BindLocal and RevokeLocal require --credential. These options are not valid for other actions.").ConfigureAwait(false);
                return 2;
            }

            if (roleText is not null)
            {
                if (!TryParseRole(roleText, out LabelTeamRole result))
                {
                    await Console.Error.WriteLineAsync("✖   Role must be Viewer, Moderator, or Administrator.").ConfigureAwait(false);
                    return 2;
                }

                parsedRole = result;
            }

            string prompt = $"{parsedAction} for '{did}'?";
            if (!await ConfirmAsync(parseResult.GetValue(yes), prompt, cancellationToken).ConfigureAwait(false))
            {
                return 2;
            }

            return await ExecuteAsync(
                method: "changeTeam",
                request: new(
                    Method: "changeTeam",
                    OperationId: operation,
                    Reason: auditReason,
                    Target: did.Value,
                    Action: parsedAction,
                    Role: parsedRole,
                    Credential: credentialText),
                transport: transport,
                parseResult: parseResult,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    /// <summary>Creates authorized resumable retirement lifecycle commands.</summary>
    internal static Command CreateRetirementCommand()
    {
        Command command = new("retire", "Inspect and control durable background retirement; issuance never reopens.");
        Command status = new("status", "Show checkpoints, publication, grace, pause and key-removal state.");
        TransportOptions transport = AddTransportOptions(status);
        status.SetAction((parsed, token) => ExecuteAsync(
            method: "getRetirementState",
            request: new(Method: "getRetirementState", Reason: "Administrative retirement inspection."),
            transport: transport,
            parseResult: parsed,
            cancellationToken: token));
        command.Subcommands.Add(status);
        command.Subcommands.Add(CreateRetirementStepCommand("preflight", "Preview immutable intent and obtain an expiring action-specific challenge; does not execute the action.", "prepareRetirement"));
        command.Subcommands.Add(CreateRetirementStepCommand("begin", "Irreversibly close issuance and intake; start the durable worker.", "beginRetirement"));
        command.Subcommands.Add(CreateRetirementStepCommand("advance", "Manual compatibility: process one bounded step, not the normal worker path.", "advanceRetirement"));
        command.Subcommands.Add(CreateRetirementStepCommand("pause", "Pause new worker batches without reopening issuance.", "pauseRetirement"));
        command.Subcommands.Add(CreateRetirementStepCommand("resume", "Renew authority for the saved workflow using a new command operation ID.", "resumeRetirement"));
        command.Subcommands.Add(CreateRetirementStepCommand("grace", "Begin the downstream distribution grace period.", "beginGracePeriod"));
        command.Subcommands.Add(CreateRetirementStepCommand("extend-grace", "Extend, never shorten, the distribution grace deadline.", "extendGracePeriod"));
        command.Subcommands.Add(CreateRetirementStepCommand("complete", "Complete after grace with an explicit distribution-availability attestation.", "completeRetirement"));
        command.Subcommands.Add(CreateRetirementStepCommand("purge", "Delete label history/receipts only; retain moderation, audit, backups and downstream copies.", "purgeRetiredData"));
        command.Subcommands.Add(CreateRetirementStepCommand("request-key-removal", "Fence signing and record the external key/host/copy inventory; does not delete keys.", "requestKeyRemoval"));
        command.Subcommands.Add(CreateRetirementStepCommand("attest-key-removal", "Record operator-performed external key removal, not verified deletion.", "attestKeyRemoval"));
        command.Subcommands.Add(CreateRetirementStepCommand("confirm-host-stopped", "Attest independently verified stopped retirement host; does not detect or stop a process.", "confirmRetirementHostStopped"));
        return command;
    }

    /// <summary>Creates one lifecycle transition with explicit confirmation and a stable audit identity.</summary>
    private static Command CreateRetirementStepCommand(string name, string description, string method)
    {
        Command command = new(name, description);
        TransportOptions transport = AddTransportOptions(command);
        Option<string> operationId = new("--operation-id") { Description = "The operation identity recorded in the maintenance audit.", Required = true };
        Option<string> reason = new("--reason") { Description = "The operator's audit reason.", Required = true };
        bool preview = method == "prepareRetirement";
        Option<string> action = new("--action") { Description = "The exact administration method being previewed, such as beginRetirement.", Required = preview };
        Option<string?> confirmation = new("--confirmation") { Description = "The action-specific server challenge returned by preflight. --yes cannot replace it." };
        Option<string?> attestation = new("--attestation") { Description = "Availability evidence for completion, or external-removal evidence for key attestation." };
        Option<string?> inventory = new("--inventory") { Description = "Key IDs/public fingerprints, signing hosts, copies and backup categories; never secret material." };
        Option<string?> hostId = new("--host-id") { Description = "The exact retirement host registration GUID independently verified as stopped.", Required = method == "confirmRetirementHostStopped" };
        Option<bool> yes = new("--yes") { Description = "Skip the ordinary prompt only; never substitutes for --confirmation or attestation." };
        Option<int> days = new("--days") { Description = "An explicitly selected grace duration or extension in days (1 through 365).", Required = method is "beginGracePeriod" or "extendGracePeriod" };
        Option<int> limit = new("--limit")
        {
            Description = "Legacy purge batch hint (1-1,000). The durable worker controls its bounded batches.",
            DefaultValueFactory = _ => 100
        };
        command.Options.Add(operationId);
        command.Options.Add(reason);
        command.Options.Add(yes);
        if (preview)
        {
            command.Options.Add(action);
        }

        if (preview || RequiresRetirementConfirmation(method))
        {
            command.Options.Add(confirmation);
        }

        if (preview || method is "completeRetirement" or "attestKeyRemoval" or "confirmRetirementHostStopped")
        {
            command.Options.Add(attestation);
        }

        if (preview || method == "confirmRetirementHostStopped")
        {
            command.Options.Add(hostId);
        }

        if (preview || method is "requestKeyRemoval" or "attestKeyRemoval")
        {
            command.Options.Add(inventory);
        }

        if (preview || method is "beginGracePeriod" or "extendGracePeriod")
        {
            command.Options.Add(days);
        }

        if (method == "purgeRetiredData")
        {
            command.Options.Add(limit);
        }

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string? operation = parseResult.GetValue(operationId);
            string? auditReason = parseResult.GetValue(reason);
            string intendedAction = preview ? parseResult.GetValue(action) ?? "" : method;
            int graceDays = preview || method is "beginGracePeriod" or "extendGracePeriod" ? parseResult.GetValue(days) : 0;
            int purgeLimit = method == "purgeRetiredData" ? parseResult.GetValue(limit) : 100;
            string? hostText = preview || method == "confirmRetirementHostStopped" ? parseResult.GetValue(hostId) : null;
            Guid? host = null;
            if (hostText is not null)
            {
                if (!Guid.TryParse(hostText, out Guid parsedHost) || parsedHost == Guid.Empty)
                {
                    await Console.Error.WriteLineAsync("✖   Supply the exact non-empty host registration GUID for --host-id.").ConfigureAwait(false);
                    return 2;
                }

                host = parsedHost;
            }

            AdministrationCommand request = new(
                Method: method,
                OperationId: operation ?? "",
                Reason: auditReason ?? "",
                Target: preview ? intendedAction : "",
                Limit: purgeLimit,
                GracePeriodDays: graceDays,
                Confirmation: preview || RequiresRetirementConfirmation(method) ? parseResult.GetValue(confirmation) : null,
                Attestation: preview || method is "completeRetirement" or "attestKeyRemoval" or "confirmRetirementHostStopped" ? parseResult.GetValue(attestation) : null,
                Inventory: preview || method is "requestKeyRemoval" or "attestKeyRemoval" ? parseResult.GetValue(inventory) : null,
                HostInstanceId: host);
            if (!HasValidRetirementIntent(request, preview) || purgeLimit is < 1 or > 1000)
            {
                await Console.Error.WriteLineAsync(
                    "✖   Supply a supported action, stable operation ID and reason, explicit 1-365 grace days where applicable, " +
                    "and required inventory/attestation/host ID. Irreversible actions and stopped-host attestation require --confirmation from " +
                    "retire preflight with the SAME action, operation ID, reason, days, inventory, attestation and host ID. --yes is insufficient.").ConfigureAwait(false);
                return 2;
            }

            if (!preview && !RequiresRetirementConfirmation(method) && !await ConfirmAsync(
                parseResult.GetValue(yes),
                $"{method} for labeler '{parseResult.GetValue(transport.AudienceText) ?? "configured labeler"}'?",
                cancellationToken).ConfigureAwait(false))
            {
                return 2;
            }

            return await ExecuteAsync(
                method: method,
                request: request,
                transport: transport,
                parseResult: parseResult,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    /// <summary>Identifies actions whose server-issued proof cannot be replaced by a blanket CLI flag.</summary>
    internal static bool RequiresRetirementConfirmation(string method) =>
        method is "beginRetirement" or "purgeRetiredData" or "completeRetirement" or "requestKeyRemoval" or "attestKeyRemoval" or "confirmRetirementHostStopped";

    /// <summary>Validates immutable intent before transport, including the same evidence in preview and execution.</summary>
    internal static bool HasValidRetirementIntent(AdministrationCommand command, bool preview)
    {
        string action = preview ? command.Target : command.Method;
        bool grace = action is "beginGracePeriod" or "extendGracePeriod";
        bool keys = action is "requestKeyRemoval" or "attestKeyRemoval";
        bool attestation = action is "completeRetirement" or "attestKeyRemoval" or "confirmRetirementHostStopped";
        bool stoppedHost = action == "confirmRetirementHostStopped";
        return action is ("beginRetirement" or "advanceRetirement" or "pauseRetirement" or "resumeRetirement" or
                "beginGracePeriod" or "extendGracePeriod" or "completeRetirement" or "purgeRetiredData" or
                "requestKeyRemoval" or "attestKeyRemoval" or "confirmRetirementHostStopped") &&
            !string.IsNullOrWhiteSpace(command.OperationId) &&
            !string.IsNullOrWhiteSpace(command.Reason) &&
            (grace ? command.GracePeriodDays is >= 1 and <= 365 : command.GracePeriodDays == 0) &&
            (keys ? !string.IsNullOrWhiteSpace(command.Inventory) : command.Inventory is null) &&
            (attestation ? !string.IsNullOrWhiteSpace(command.Attestation) : command.Attestation is null) &&
            (stoppedHost ? command.HostInstanceId is Guid host && host != Guid.Empty : command.HostInstanceId is null) &&
            (preview ? command.Confirmation is null :
                !RequiresRetirementConfirmation(action) || !string.IsNullOrWhiteSpace(command.Confirmation));
    }

    /// <summary>Creates resumable host maintenance and signing-key rotation commands.</summary>
    private static Command CreateRotationCommand()
    {
        Command command = new("rotation", "Coordinate a resumable, multi-host label signing-key rotation.");
        Command status = new("status", "Show maintenance and key-rotation progress.");
        TransportOptions statusTransport = AddTransportOptions(status);
        status.SetAction((parseResult, cancellationToken) => ExecuteAsync(
            method: "getMaintenanceState",
            request: new(Method: "getMaintenanceState", Reason: "Administrative inspection."),
            transport: statusTransport,
            parseResult: parseResult,
            cancellationToken: cancellationToken));
        command.Subcommands.Add(status);
        command.Subcommands.Add(CreateRotationStepCommand(
            name: "begin",
            description: "Enter maintenance and snapshot participating hosts.",
            method: "beginRotation",
            includeReplacementKey: true));
        command.Subcommands.Add(CreateRotationStepCommand(
            name: "confirm-host-stopped",
            description: "Acknowledge a host independently verified by the operator as stopped.",
            method: "confirmHostStopped",
            includeHost: true));
        command.Subcommands.Add(CreateRotationStepCommand(
            name: "capture-boundary",
            description: "Fix the published stream boundary after every host has drained.",
            method: "captureRotationBoundary"));
        command.Subcommands.Add(CreateRotationStepCommand(
            name: "verify-publication",
            description: "Resolve and verify the replacement key and configured endpoint.",
            method: "verifyRotationKeyPublication"));
        command.Subcommands.Add(CreateRotationStepCommand(
            name: "resign-batch",
            description: "Re-sign one bounded batch from the durable checkpoint.",
            method: "resignRotationBatch",
            includeBatchLimit: true));
        command.Subcommands.Add(CreateRotationStepCommand(
            name: "complete",
            description: "Release maintenance after every live host activated the replacement key.",
            method: "completeRotation"));
        command.Subcommands.Add(CreateDidWebSnippetCommand());
        command.Subcommands.Add(CreatePlcPublicationCommand());
        command.Subcommands.Add(CreatePlcRecoveryCommand());
        return command;
    }

    /// <summary>Creates one explicitly confirmed rotation operation.</summary>
    private static Command CreateRotationStepCommand(
        string name,
        string description,
        string method,
        bool includeReplacementKey = false,
        bool includeHost = false,
        bool includeBatchLimit = false)
    {
        Command command = new(name, description);
        TransportOptions transport = AddTransportOptions(command);
        Option<string> operationId = new("--operation-id")
        {
            Description = "The stable rotation operation ID (use the same value when resuming).",
            Required = true
        };
        Option<string> reason = new("--reason") { Description = "The operator's audit reason.", Required = true };
        Option<string?> replacementKeyId = new("--replacement-key-id")
        {
            Description = "The preinstalled replacement key identity.",
            Required = includeReplacementKey
        };
        Option<string?> hostId = new("--host-id")
        {
            Description = "The participating host GUID to confirm as stopped.",
            Required = includeHost
        };
        Option<int> limit = new("--limit")
        {
            Description = "The maximum number of labels in this re-signing batch (1 through 1,000).",
            DefaultValueFactory = _ => 250
        };
        Option<bool> yes = new("--yes") { Description = "Confirm without an interactive prompt." };
        command.Options.Add(operationId);
        command.Options.Add(reason);
        if (includeReplacementKey)
        {
            command.Options.Add(replacementKeyId);
        }

        if (includeHost)
        {
            command.Options.Add(hostId);
        }

        if (includeBatchLimit)
        {
            command.Options.Add(limit);
        }

        command.Options.Add(yes);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string? operation = parseResult.GetValue(operationId);
            string? auditReason = parseResult.GetValue(reason);
            string? keyId = includeReplacementKey ? parseResult.GetValue(replacementKeyId) : null;
            string? participantId = includeHost ? parseResult.GetValue(hostId) : null;
            int batchLimit = includeBatchLimit ? parseResult.GetValue(limit) : 250;
            if (string.IsNullOrWhiteSpace(operation) ||
                string.IsNullOrWhiteSpace(auditReason) ||
                includeReplacementKey && !IsValidKeyId(keyId) ||
                includeHost && !Guid.TryParse(participantId, out _) ||
                includeBatchLimit && batchLimit is < 1 or > 1000)
            {
                await Console.Error.WriteLineAsync("✖   Supply valid operation, audit, key, host and batch values for this rotation step.").ConfigureAwait(false);
                return 2;
            }

            string prompt = method switch
            {
                "beginRotation" => $"Enter maintenance and begin rotation '{operation}' using key '{keyId}'?",
                "confirmHostStopped" => $"Confirm host '{participantId}' is stopped and acknowledge it for rotation '{operation}'?",
                "completeRotation" => $"Complete rotation '{operation}' and reopen label distribution?",
                _ => $"Run {name} for rotation '{operation}'?"
            };
            if (!await ConfirmAsync(parseResult.GetValue(yes), prompt, cancellationToken).ConfigureAwait(false))
            {
                return 2;
            }

            return await ExecuteAsync(
                method: method,
                request: new(
                    Method: method,
                    OperationId: operation,
                    Reason: auditReason,
                    Target: participantId ?? "",
                    Limit: batchLimit,
                    Confirmed: method == "confirmHostStopped",
                    ReplacementKeyId: keyId),
                transport: transport,
                parseResult: parseResult,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    /// <summary>Creates a did:web publication snippet from the public replacement key.</summary>
    private static Command CreateDidWebSnippetCommand()
    {
        Command command = new("did-web-snippet", "Print merge-ready did:web identity additions using public key material only.");
        Argument<string> didText = new("did") { Description = "The labeler's did:web identifier." };
        Option<string> publicKey = new("--public-key") { Description = "The replacement public multibase key from rotation status.", Required = true };
        Option<Uri?> endpoint = CreateUriOption("--endpoint", "The configured public HTTPS labeler origin.", required: true);
        command.Arguments.Add(didText);
        command.Options.Add(publicKey);
        command.Options.Add(endpoint);
        command.SetAction(async parseResult =>
        {
            string? text = parseResult.GetValue(didText);
            string? key = parseResult.GetValue(publicKey);
            Uri? publicEndpoint = parseResult.GetValue(endpoint);
            if (text is null || !Did.TryParse(text, out Did? did) || key is null || publicEndpoint is null)
            {
                await Console.Error.WriteLineAsync("✖   Supply a did:web identifier, public multibase key, and HTTPS endpoint.").ConfigureAwait(false);
                return 2;
            }

            try
            {
                await WriteStructuredOutputAsync(LabelerIdentityDocument.CreateDidWebSnippet(did, key, publicEndpoint)).ConfigureAwait(false);
                await Console.Error.WriteLineAsync("✔   DID document snippet generated. Nothing was published.").ConfigureAwait(false);
                return 0;
            }
            catch (ArgumentException exception)
            {
                await Console.Error.WriteLineAsync($"✖   Invalid identity publication input ({exception.GetType().Name}).").ConfigureAwait(false);
                return 2;
            }
        });
        return command;
    }

    /// <summary>Creates an interactive PLC publication command using the authenticated account's PDS.</summary>
    private static Command CreatePlcPublicationCommand()
    {
        Command command = new("publish-plc", "Publish the replacement public key in the signed-in account's PLC DID document.");
        Option<string> handle = new("--account-handle") { Description = "The labeler account whose PLC identity will be changed.", Required = true };
        Option<string> clientId = new("--oauth-client-id") { Description = "The HTTPS native OAuth client metadata URL advertising identity authority.", Required = true };
        Option<string> publicKey = new("--public-key") { Description = "The replacement public multibase key from rotation status.", Required = true };
        Option<Uri?> endpoint = CreateUriOption("--endpoint", "The configured public HTTPS labeler origin.", required: true);
        Option<string?> authorizationToken = new("--authorization-token")
        {
            Description = "The PDS email authorization code, if it was requested separately."
        };
        Option<bool> yes = new("--yes") { Description = "Confirm without an interactive prompt." };
        Option<bool> rememberMe = AddRememberMeOption(command);
        command.Options.Add(handle);
        command.Options.Add(clientId);
        command.Options.Add(publicKey);
        command.Options.Add(endpoint);
        command.Options.Add(authorizationToken);
        command.Options.Add(yes);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string? account = parseResult.GetValue(handle);
            string? metadata = parseResult.GetValue(clientId);
            string? key = parseResult.GetValue(publicKey);
            Uri? publicEndpoint = parseResult.GetValue(endpoint);
            if (account is null ||
                !Handle.TryParse(account, out Handle? parsedHandle) ||
                !Uri.TryCreate(metadata, UriKind.Absolute, out Uri? metadataUri) ||
                metadataUri is null ||
                metadataUri.Scheme != Uri.UriSchemeHttps ||
                !string.IsNullOrEmpty(metadataUri.UserInfo) ||
                !string.IsNullOrEmpty(metadataUri.Query) ||
                !string.IsNullOrEmpty(metadataUri.Fragment) ||
                key is null ||
                publicEndpoint is null ||
                !IsHttpsOrigin(publicEndpoint))
            {
                await Console.Error.WriteLineAsync("✖   Supply a valid handle, HTTPS native OAuth metadata URL, public key, and HTTPS labeler origin.").ConfigureAwait(false);
                return 2;
            }

            if (!await ConfirmAsync(parseResult.GetValue(yes), $"Publish the replacement label key in the PLC identity for '{account}'?", cancellationToken).ConfigureAwait(false))
            {
                return 2;
            }

            return await ExecutePlcPublicationAsync(
                handle: parsedHandle,
                clientId: metadataUri,
                publicKeyMultibase: key,
                labelerEndpoint: publicEndpoint,
                authorizationToken: parseResult.GetValue(authorizationToken),
                rememberMe: parseResult.GetValue(rememberMe),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        });
        return command;
    }

    /// <summary>Creates a signed-operation recovery command for an interrupted PLC publication.</summary>
    private static Command CreatePlcRecoveryCommand()
    {
        Command command = new("recover-plc", "Safely resume a saved signed PLC identity operation.");
        Argument<string> didText = new("did") { Description = "The labeler's PLC DID." };
        Option<string> handle = new("--account-handle") { Description = "The labeler account whose PLC identity will be recovered.", Required = true };
        Option<string> clientId = new("--oauth-client-id") { Description = "The HTTPS native OAuth client metadata URL advertising identity authority.", Required = true };
        Option<string> publicKey = new("--public-key") { Description = "The public label verification key expected in the DID document.", Required = true };
        Option<Uri?> endpoint = CreateUriOption("--endpoint", "The configured public HTTPS labeler origin.", required: true);
        Option<FileInfo> operationFile = new("--operation-file") { Description = "A file containing the saved signed PLC operation.", Required = true };
        Option<bool> yes = new("--yes") { Description = "Confirm without an interactive prompt." };
        Option<bool> rememberMe = AddRememberMeOption(command);
        command.Arguments.Add(didText);
        command.Options.Add(handle);
        command.Options.Add(clientId);
        command.Options.Add(publicKey);
        command.Options.Add(endpoint);
        command.Options.Add(operationFile);
        command.Options.Add(yes);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string? didValue = parseResult.GetValue(didText);
            string? handleValue = parseResult.GetValue(handle);
            string? metadata = parseResult.GetValue(clientId);
            string? key = parseResult.GetValue(publicKey);
            Uri? publicEndpoint = parseResult.GetValue(endpoint);
            FileInfo? operationPath = parseResult.GetValue(operationFile);
            if (didValue is null ||
                !Did.TryParse(didValue, out Did? did) ||
                handleValue is null ||
                !Handle.TryParse(handleValue, out Handle? parsedHandle) ||
                !Uri.TryCreate(metadata, UriKind.Absolute, out Uri? metadataUri) ||
                metadataUri is null ||
                metadataUri.Scheme != Uri.UriSchemeHttps ||
                !string.IsNullOrEmpty(metadataUri.UserInfo) ||
                !string.IsNullOrEmpty(metadataUri.Query) ||
                !string.IsNullOrEmpty(metadataUri.Fragment) ||
                key is null ||
                key.Length is < 2 or > 512 ||
                key[0] != 'z' ||
                key.Any(char.IsControl) ||
                publicEndpoint is null ||
                !IsHttpsOrigin(publicEndpoint) ||
                operationPath is null)
            {
                await Console.Error.WriteLineAsync("✖   Supply a PLC DID, valid handle, HTTPS metadata and endpoint, public key, and signed-operation file.").ConfigureAwait(false);
                return 2;
            }

            if (!await ConfirmAsync(
                parseResult.GetValue(yes),
                $"Submit the saved PLC identity operation for '{did}' after checking its current DID document?",
                cancellationToken).ConfigureAwait(false))
            {
                return 2;
            }

            try
            {
                byte[] operationBytes = await File.ReadAllBytesAsync(operationPath.FullName, cancellationToken).ConfigureAwait(false);
                if (operationBytes.Length is 0 or > 64 * 1024)
                {
                    await Console.Error.WriteLineAsync("✖   The signed PLC operation file must be between 1 byte and 64 KiB.").ConfigureAwait(false);
                    return 2;
                }

                using JsonDocument operationDocument = JsonDocument.Parse(
                    operationBytes,
                    new JsonDocumentOptions { MaxDepth = 16 });
                return await ExecutePlcRecoveryAsync(
                    did: did,
                    handle: parsedHandle,
                    clientId: metadataUri,
                    publicKeyMultibase: key,
                    labelerEndpoint: publicEndpoint,
                    signedOperation: operationDocument.RootElement.Clone(),
                    rememberMe: parseResult.GetValue(rememberMe),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                await Console.Error.WriteLineAsync("✖   The signed PLC operation file is not valid bounded JSON.").ConfigureAwait(false);
                return 2;
            }
            catch (IOException exception)
            {
                await Console.Error.WriteLineAsync($"✖   Could not read the PLC operation file ({exception.GetType().Name}).").ConfigureAwait(false);
                return 3;
            }
        });
        return command;
    }

    /// <summary>Requests identity mutation permission only for PLC publication and recovery sign-ins.</summary>
    internal static OAuthOptions CreatePlcOAuthOptions(Uri clientId) => new()
    {
        ClientId = clientId.AbsoluteUri,
        Scopes = ["atproto", "identity:*"]
    };

    /// <summary>Authenticates, avoids duplicate submission, and verifies a recovered PLC identity operation.</summary>
    private static async Task<int> ExecutePlcRecoveryAsync(
        Did did,
        Handle handle,
        Uri clientId,
        string publicKeyMultibase,
        Uri labelerEndpoint,
        JsonElement signedOperation,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        OAuthOptions oauthOptions = CreatePlcOAuthOptions(clientId);
        CliSignInLifetime signInLifetime = new();
        await using ConfiguredAsyncDisposable signInCleanup = signInLifetime.ConfigureAwait(false);
        using BlueskyAgent agent = new(new BlueskyAgentOptions { OAuthOptions = oauthOptions });
        if (!await SignInAsync(
            agent: agent,
            handle: handle,
            options: oauthOptions,
            purpose: "Labeler-account identity authority to recover the saved PLC identity operation.",
            rememberMe: rememberMe,
            lifetime: signInLifetime,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return 4;
        }
        if (agent.Did != did)
        {
            await Console.Error.WriteLineAsync("✖   The signed-in account does not match the PLC identity being recovered. No operation was submitted.").ConfigureAwait(false);
            return 4;
        }

        DidDocument? current = await agent.ResolveDidDocument(did, cancellationToken).ConfigureAwait(false);
        if (current is null || current.Id != did)
        {
            await Console.Error.WriteLineAsync("✖   The PLC DID document could not be resolved and verified.").ConfigureAwait(false);
            return 5;
        }

        if (HasPublishedPlcIdentity(current, did, publicKeyMultibase, labelerEndpoint))
        {
            await WriteStructuredOutputAsync(JsonSerializer.Serialize(
                new PlcRecoveryCliResult(did.Value, "Verified", null, null),
                AdministrationJsonContext.Default.PlcRecoveryCliResult)).ConfigureAwait(false);
            await Console.Error.WriteLineAsync("✔   PLC identity verified. No submission was needed.").ConfigureAwait(false);
            return 0;
        }

        AtProtoHttpResult<EmptyResponse> submission = await agent.SubmitPlcLabelerIdentityOperation(
            signedOperation,
            cancellationToken).ConfigureAwait(false);
        if (!submission.Succeeded)
        {
            await WriteStructuredOutputAsync(JsonSerializer.Serialize(
                new PlcRecoveryCliResult(
                    did.Value,
                    "SubmissionFailed",
                    (int)submission.StatusCode,
                    "Resolve the DID document before retrying; submit the saved operation only if it has not already published."),
                AdministrationJsonContext.Default.PlcRecoveryCliResult)).ConfigureAwait(false);
            await Console.Error.WriteLineAsync("✖   PLC submission failed. Inspect the recovery guidance before retrying.").ConfigureAwait(false);
            return 5;
        }

        DidDocument? published = await agent.ResolveDidDocument(did, cancellationToken).ConfigureAwait(false);
        bool verified = published is not null && HasPublishedPlcIdentity(
            published,
            did,
            publicKeyMultibase,
            labelerEndpoint);
        await WriteStructuredOutputAsync(JsonSerializer.Serialize(
            new PlcRecoveryCliResult(
                did.Value,
                verified ? "Verified" : "VerificationFailed",
                verified ? null : (int)submission.StatusCode,
                verified ? null : "Resolve the DID document and inspect its label key and service before retrying."),
            AdministrationJsonContext.Default.PlcRecoveryCliResult)).ConfigureAwait(false);
        await Console.Error.WriteLineAsync(verified
            ? "✔   PLC identity publication succeeded and was independently verified."
            : "✖   PLC submission completed, but verification failed. Inspect the recovery guidance before retrying.").ConfigureAwait(false);
        return verified ? 0 : 5;
    }

    /// <summary>Checks that a resolved PLC document publishes the exact requested label key and origin.</summary>
    internal static bool HasPublishedPlcIdentity(
        DidDocument document,
        Did did,
        string publicKeyMultibase,
        Uri labelerEndpoint) =>
        document.Id == did &&
        document.VerificationMethods?.Any(method =>
            method.Id == did.Value + "#atproto_label" &&
            method.Controller == did &&
            method.PublicKeyMultibase == publicKeyMultibase) == true &&
        document.Services?.Any(service =>
            service.Id == did.Value + "#atproto_labeler" &&
            service.Type == "AtprotoLabeler" &&
            service.ServiceEndpoint == labelerEndpoint) == true;

    /// <summary>Authenticates the administrator with the PDS, then asks it to publish and verify the public label key.</summary>
    private static async Task<int> ExecutePlcPublicationAsync(
        Handle handle,
        Uri clientId,
        string publicKeyMultibase,
        Uri labelerEndpoint,
        string? authorizationToken,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        OAuthOptions oauthOptions = CreatePlcOAuthOptions(clientId);
        BlueskyAgentOptions options = new()
        {
            OAuthOptions = oauthOptions
        };
        CliSignInLifetime signInLifetime = new();
        await using ConfiguredAsyncDisposable signInCleanup = signInLifetime.ConfigureAwait(false);
        using BlueskyAgent agent = new(options);
        if (!await SignInAsync(
            agent: agent,
            handle: handle,
            options: oauthOptions,
            purpose: "Labeler-account identity authority to publish the label key and service in its PLC DID document.",
            rememberMe: rememberMe,
            lifetime: signInLifetime,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return 4;
        }

        string? token = authorizationToken;
        if (string.IsNullOrWhiteSpace(token) && !Console.IsInputRedirected)
        {
            AtProtoHttpResult<EmptyResponse> codeRequest = await agent.RequestPlcOperationSignature(cancellationToken).ConfigureAwait(false);
            if (!codeRequest.Succeeded)
            {
                await Console.Error.WriteLineAsync($"✖   The PDS did not request a PLC authorization code ({(int)codeRequest.StatusCode}).").ConfigureAwait(false);
                return 4;
            }

            await Console.Error.WriteLineAsync("⚠   Enter the PLC authorization code sent by the PDS.").ConfigureAwait(false);
            token = await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }

        PlcLabelerIdentityUpdateResult result = await agent.UpdatePlcLabelerIdentity(
            publicKeyMultibase: publicKeyMultibase,
            labelerEndpoint: labelerEndpoint,
            authorizationToken: token,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await WriteStructuredOutputAsync(JsonSerializer.Serialize(
            new PlcPublicationCliResult(
                result.Did.Value,
                result.Stage.ToString(),
                result.SignedOperation,
                result.RecoveryAction),
            AdministrationJsonContext.Default.PlcPublicationCliResult)).ConfigureAwait(false);
        await Console.Error.WriteLineAsync(result.Stage == PlcLabelerIdentityUpdateStage.Verified
            ? "✔   PLC identity publication succeeded and was independently verified."
            : "✖   PLC identity publication was not verified. Inspect the stage and recovery guidance before retrying.").ConfigureAwait(false);
        return result.Stage == PlcLabelerIdentityUpdateStage.Verified ? 0 : 5;
    }

    /// <summary>Parses absolute URI options explicitly rather than relying on unsupported framework conversions.</summary>
    internal static Option<Uri?> CreateUriOption(string name, string description, bool required = false) =>
        new(name)
        {
            Description = description,
            Required = required,
            Arity = ArgumentArity.ExactlyOne,
            CustomParser = result =>
            {
                if (result.Tokens.Count == 0)
                {
                    return null;
                }

                if (Uri.TryCreate(result.Tokens[0].Value, UriKind.Absolute, out Uri? uri) &&
                    uri.Scheme is "http" or "https")
                {
                    return uri;
                }

                result.AddError($"Supply an absolute HTTP or HTTPS URI for {name}.");
                return null;
            }
        };

    /// <summary>Adds transport-selection and remote-authentication options to a command.</summary>
    private static TransportOptions AddTransportOptions(Command command)
    {
        Option<string?> localEndpoint = new("--local-endpoint") { Description = "Use the configured protected local pipe/socket." };
        Option<Uri?> adminUrl = CreateUriOption("--labeler-url", "The labeler's HTTPS origin, without a path, for remote administration.");
        Option<string?> audienceText = new("--admin-service") { Description = "Override the administration DID#service reference discovered from native OAuth metadata." };
        Option<string?> handle = new("--admin-handle") { Description = "The administrator's Bluesky handle, not the labeler account." };
        Option<string?> clientId = new("--oauth-client-id") { Description = "The native OAuth metadata URL; defaults to <labeler-url>/native-client-metadata.json." };
        Option<bool> rememberMe = AddRememberMeOption(command);
        command.Options.Add(localEndpoint);
        command.Options.Add(adminUrl);
        command.Options.Add(audienceText);
        command.Options.Add(handle);
        command.Options.Add(clientId);
        return new(localEndpoint, adminUrl, audienceText, handle, clientId, rememberMe);
    }

    /// <summary>Executes one bounded command over the chosen authorized transport.</summary>
    private static async Task<int> ExecuteAsync(
        string method,
        AdministrationCommand request,
        TransportOptions transport,
        ParseResult parseResult,
        CancellationToken cancellationToken,
        AdministrationWorkflow? workflow = null)
    {
        try
        {
            string? localEndpoint = parseResult.GetValue(transport.LocalEndpoint);
            Uri? adminUrl = parseResult.GetValue(transport.AdminUrl);
            if (!string.IsNullOrWhiteSpace(localEndpoint) && adminUrl is null)
            {
                if (workflow is not null)
                {
                    return await workflow(
                        async (command, token) => new(
                            await LabelerLocalAdministration.SendLocalCommandAsync(
                                endpoint: localEndpoint,
                                command: JsonSerializer.Serialize(command, AdministrationJsonContext.Default.AdministrationCommand),
                                cancellationToken: token).ConfigureAwait(false),
                            HttpStatusCode.OK),
                        cancellationToken).ConfigureAwait(false);
                }

                string command = JsonSerializer.Serialize(request, AdministrationJsonContext.Default.AdministrationCommand);
                string localResponse = await LabelerLocalAdministration.SendLocalCommandAsync(
                    endpoint: localEndpoint,
                    command: command,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                return await WriteResponseAsync(localResponse, HttpStatusCode.OK).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(localEndpoint) && adminUrl is not null)
            {
                return await ExecuteRemoteAsync(
                    method: method,
                    request: request,
                    adminUrl: adminUrl,
                    audienceText: parseResult.GetValue(transport.AudienceText),
                    handleText: parseResult.GetValue(transport.Handle),
                    clientId: parseResult.GetValue(transport.ClientId),
                    rememberMe: parseResult.GetValue(transport.RememberMe),
                    cancellationToken: cancellationToken,
                    workflow: workflow).ConfigureAwait(false);
            }

            await Console.Error.WriteLineAsync("✖   Select exactly one transport: --local-endpoint or --labeler-url.").ConfigureAwait(false);
            return 2;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await Console.Error.WriteLineAsync("⚠   Operation cancelled.").ConfigureAwait(false);
            return 130;
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("⚠   Administration request timed out. Sign-in may have succeeded, but no command success was confirmed. Check service availability before retrying.").ConfigureAwait(false);
            return 3;
        }
        catch (ArgumentException exception)
        {
            await Console.Error.WriteLineAsync($"✖   Invalid administration input ({exception.GetType().Name}).").ConfigureAwait(false);
            return 2;
        }
        catch (OAuthException exception)
        {
            await Console.Error.WriteLineAsync($"✖   Administration OAuth failed ({ClassifyOAuthFailure(exception)}). No further workflow steps will run.").ConfigureAwait(false);
            return 4;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException or
                                           InvalidDataException or JsonException or
                                           InvalidOperationException or PlatformNotSupportedException)
        {
            await Console.Error.WriteLineAsync($"✖   Administration failed ({exception.GetType().Name}).").ConfigureAwait(false);
            return 3;
        }
    }

    /// <summary>Authenticates with the user's PDS, requests a method-bound service JWT, then calls HTTPS administration.</summary>
    private static async Task<int> ExecuteRemoteAsync(
        string method,
        AdministrationCommand request,
        Uri adminUrl,
        string? audienceText,
        string? handleText,
        string? clientId,
        bool rememberMe,
        CancellationToken cancellationToken,
        AdministrationWorkflow? workflow = null)
    {
        if (!IsHttpsOrigin(adminUrl) ||
            handleText is null ||
            !Handle.TryParse(handleText, out Handle? handle) ||
            !Uri.TryCreate(clientId ?? new Uri(adminUrl, "/native-client-metadata.json").AbsoluteUri, UriKind.Absolute, out Uri? metadataUri) ||
            metadataUri is null ||
            !IsNativeClientMetadataUri(metadataUri, adminUrl) ||
            handle is null)
        {
            await Console.Error.WriteLineAsync("✖   Remote mode requires --labeler-url with an HTTPS origin and --admin-handle. An explicit --oauth-client-id must share the labeler origin.").ConfigureAwait(false);
            return 2;
        }

        AtProtoServiceReference? audience;
        if (audienceText is not null)
        {
            if (!TryParseServiceReference(audienceText, out audience) || audience is null)
            {
                await Console.Error.WriteLineAsync("✖   --admin-service must be an exact DID#service reference.").ConfigureAwait(false);
                return 2;
            }
        }
        else
        {
            audience = await DiscoverAdminServiceAsync(metadataUri, cancellationToken).ConfigureAwait(false);
            if (audience is null)
            {
                await Console.Error.WriteLineAsync("✖   OAuth metadata does not advertise one unambiguous labeler administration service. Supply --admin-service explicitly.").ConfigureAwait(false);
                return 2;
            }
        }

        CliSignInLifetime signInLifetime = new();
        await using ConfiguredAsyncDisposable signInCleanup = signInLifetime.ConfigureAwait(false);
        if (workflow is not null)
        {
            Dictionary<string, AtProtoAgent> methodAgents = new(StringComparer.Ordinal);
            try
            {
                return await workflow(
                    async (command, token) =>
                    {
                        if (!s_policyPublicationMethods.Contains(command.Method, StringComparer.Ordinal))
                        {
                            return new(
                                """{"succeeded":false,"error":"InvalidRequest"}""",
                                HttpStatusCode.BadRequest);
                        }

                        AtProtoAgent methodAgent;
                        if (methodAgents.TryGetValue(command.Method, out AtProtoAgent? existingAgent))
                        {
                            methodAgent = existingAgent;
                        }
                        else
                        {
                            OAuthOptions methodOptions = CreateRemoteOAuthOptions(metadataUri, audience, command.Method);
                            methodAgent = new(
                                new Uri(DefaultPds),
                                new AtProtoAgentOptions
                                {
                                    OAuthOptions = methodOptions,
                                    HttpClientOptions = new(timeout: s_remoteRequestTimeout)
                                });
                            methodAgents.Add(command.Method, methodAgent);
                            if (!await SignInAsync(
                                agent: methodAgent,
                                handle: handle,
                                options: methodOptions,
                                purpose: null,
                                rememberMe: rememberMe,
                                lifetime: signInLifetime,
                                cancellationToken: token).ConfigureAwait(false))
                            {
                                throw new OAuthException("Administrator sign-in did not complete.");
                            }
                        }

                        return await SendRemoteCommandAsync(
                            agent: methodAgent,
                            adminUrl: adminUrl,
                            audience: audience,
                            request: command,
                            cancellationToken: token).ConfigureAwait(false);
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                foreach (AtProtoAgent methodAgent in methodAgents.Values)
                {
                    methodAgent.Dispose();
                }
            }
        }

        OAuthOptions oauthOptions = CreateRemoteOAuthOptions(metadataUri, audience, method);
        AtProtoAgentOptions options = new()
        {
            OAuthOptions = oauthOptions,
            HttpClientOptions = new(timeout: s_remoteRequestTimeout)
        };
        using AtProtoAgent agent = new(new Uri(DefaultPds), options);
        if (!await SignInAsync(
            agent: agent,
            handle: handle,
            options: oauthOptions,
            purpose: null,
            rememberMe: rememberMe,
            lifetime: signInLifetime,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return 4;
        }

        AdministrationReply reply = await SendRemoteCommandAsync(
            agent: agent, adminUrl: adminUrl, audience: audience, request: request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await WriteResponseAsync(reply.Body, reply.StatusCode).ConfigureAwait(false);
    }

    /// <summary>Creates OAuth options bound to one remote administration method.</summary>
    internal static OAuthOptions CreateRemoteOAuthOptions(Uri clientId, AtProtoServiceReference audience, string method)
    {
        string lxm = AdminMethodPrefix + method;
        string encodedAudience = Uri.EscapeDataString(audience.ToString());
        return new()
        {
            ClientId = clientId.AbsoluteUri,
            Scopes = ["atproto", $"rpc:{lxm}?aud={encodedAudience}"]
        };
    }

    /// <summary>Sends one independently method-bound request without coupling transport to console output.</summary>
    private static async Task<AdministrationReply> SendRemoteCommandAsync(
        AtProtoAgent agent,
        Uri adminUrl,
        AtProtoServiceReference audience,
        AdministrationCommand request,
        CancellationToken cancellationToken)
    {
        string lxm = AdminMethodPrefix + request.Method;
        await Console.Error.WriteLineAsync("🔒  Requesting a method-bound service token from the authenticated PDS.").ConfigureAwait(false);
        AtProtoHttpResult<ServiceCredential> tokenResult = await agent.GetServiceAuth(
            service: agent.Service,
            audience: audience,
            lxm: new Nsid(lxm),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!tokenResult.Succeeded)
        {
            await Console.Error.WriteLineAsync($"✖   The PDS could not issue the requested service token ({(int)tokenResult.StatusCode}).").ConfigureAwait(false);
            return new(
                """{"succeeded":false,"error":"Unauthorized","detail":"The PDS could not issue the requested method-bound service token."}""",
                HttpStatusCode.Forbidden);
        }

        Uri requestUri = BuildRemoteRequestUri(
            adminUrl: adminUrl,
            lxm: lxm,
            method: request.Method,
            operationId: request.OperationId,
            reason: request.Reason,
            afterSequence: request.AfterSequence,
            limit: request.Limit);
        using SocketsHttpHandler handler = new() { AllowAutoRedirect = false };
        using HttpClient httpClient = new(handler) { Timeout = s_remoteRequestTimeout };
        await Console.Error.WriteLineAsync("➜   Calling the labeler administration endpoint.").ConfigureAwait(false);
        using HttpRequestMessage httpRequest = new(
            IsRemoteGetMethod(request.Method) ? HttpMethod.Get : HttpMethod.Post,
            requestUri);
        if (!IsRemoteGetMethod(request.Method))
        {
            httpRequest.Method = HttpMethod.Post;
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(request, AdministrationJsonContext.Default.AdministrationCommand),
                System.Text.Encoding.UTF8,
                "application/json");
        }
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Result.AccessJwt);
        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new(responseBody, response.StatusCode);
    }

    /// <summary>Writes the service's JSON result and maps authorization and availability failures to process exit codes.</summary>
    private static Task<int> WriteResponseAsync(string body, HttpStatusCode statusCode) =>
        WriteResponseAsync(
            body: body,
            statusCode: statusCode,
            output: Console.Out,
            diagnostics: Console.Error,
            verbose: s_verboseOutput.Value);

    /// <summary>Keeps machine-readable output separate from human-readable confirmations and errors.</summary>
    internal static async Task<int> WriteResponseAsync(
        string body,
        HttpStatusCode statusCode,
        TextWriter output,
        TextWriter diagnostics,
        bool verbose = false)
    {
        if (verbose)
        {
            await output.WriteLineAsync(body).ConfigureAwait(false);
        }
        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            await diagnostics.WriteLineAsync("✖   Administration failed: access was denied.").ConfigureAwait(false);
            return 4;
        }

        if ((int)statusCode >= 500)
        {
            await diagnostics.WriteLineAsync("✖   Administration failed: the service is unavailable. Use --verbose to inspect the response before retrying.").ConfigureAwait(false);
            return 5;
        }

        try
        {
            using JsonDocument response = JsonDocument.Parse(body);
            if (response.RootElement.ValueKind != JsonValueKind.Object ||
                !response.RootElement.TryGetProperty("succeeded", out JsonElement succeeded) ||
                succeeded.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                await diagnostics.WriteLineAsync("✖   Administration failed: the response has no valid success receipt.").ConfigureAwait(false);
                return 5;
            }
            if (succeeded.ValueKind == JsonValueKind.False)
            {
                string? error = response.RootElement.TryGetProperty("error", out JsonElement errorValue)
                    && errorValue.ValueKind == JsonValueKind.String
                    ? errorValue.GetString()
                    : null;
                await diagnostics.WriteLineAsync($"✖   Administration failed ({SafeServiceError(error)}). Use --verbose to inspect the response for details.").ConfigureAwait(false);
                return MapServiceErrorToExitCode(error);
            }
            if ((int)statusCode is >= 200 and < 300)
            {
                string message = response.RootElement.TryGetProperty("changed", out JsonElement changed) &&
                    changed.ValueKind == JsonValueKind.False
                    ? "✔   Administration succeeded. No change was needed."
                    : "✔   Administration succeeded.";
                await diagnostics.WriteLineAsync(message).ConfigureAwait(false);
                if (!verbose)
                {
                    await WriteReadableResultAsync(response.RootElement, output, 0).ConfigureAwait(false);
                }
                return 0;
            }
        }
        catch (JsonException)
        {
            await diagnostics.WriteLineAsync("✖   The administration service returned an invalid JSON response.").ConfigureAwait(false);
            return 5;
        }

        await diagnostics.WriteLineAsync($"✖   Administration failed (HTTP {(int)statusCode}).").ConfigureAwait(false);
        return 5;
    }

    /// <summary>Formats retained PLC outcomes without enabling raw response output by default.</summary>
    private static async Task WriteStructuredOutputAsync(string body)
    {
        if (s_verboseOutput.Value)
        {
            await Console.Out.WriteLineAsync(body).ConfigureAwait(false);
            return;
        }

        using JsonDocument document = JsonDocument.Parse(body);
        await WriteReadableResultAsync(document.RootElement, Console.Out, 0).ConfigureAwait(false);
    }

    /// <summary>Shows inspection fields as indented text rather than hiding useful command results with their raw JSON.</summary>
    internal static async Task WriteReadableResultAsync(JsonElement element, TextWriter output, int depth)
    {
        string indent = new(' ', depth * 2);
        if (element.ValueKind == JsonValueKind.Object)
        {
            bool active = element.TryGetProperty("phase", out JsonElement phase) &&
                phase.ValueKind == JsonValueKind.String && phase.GetString() == "Active";
            foreach (JsonProperty property in element.EnumerateObject().OrderBy(property => property.Name switch
            {
                "issuer" => 0,
                "phase" => 1,
                _ => 2
            }))
            {
                if (property.Name == "revision")
                {
                    continue;
                }
                if (active && property.Name is "retirementUpperBound" or "retirementThrough")
                {
                    continue;
                }
                if (depth == 0 && property.Name is "succeeded" or "changed")
                {
                    continue;
                }
                if (property.Name == "openCounts" && property.Value.ValueKind == JsonValueKind.Object)
                {
                    await WriteReadableResultAsync(property.Value, output, depth).ConfigureAwait(false);
                    continue;
                }
                string name = property.Name switch
                {
                    "issuedThrough" => "Labels issued",
                    "publishedThrough" => "Labels published",
                    "replayFloor" => "Label history",
                    _ => property.Name
                };
                if (name.Length > 0)
                {
                    name = char.ToUpperInvariant(name[0]) + name[1..];
                }
                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    await output.WriteLineAsync($"{indent}{ResultIcon(property.Name)}{name}:").ConfigureAwait(false);
                    await WriteReadableResultAsync(property.Value, output, depth + 1).ConfigureAwait(false);
                }
                else
                {
                    string value = ReadableValue(property.Value);
                    if (property.Name == "replayFloor" && property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetInt64(out long floor))
                    {
                        value = floor == 0 ? "Full label history available." : $"Available from label {(decimal)floor + 1} onwards.";
                    }
                    await output.WriteLineAsync($"{indent}{ResultIcon(property.Name)}{name}: {value}").ConfigureAwait(false);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                await output.WriteLineAsync($"{indent}•   Item {++index}:").ConfigureAwait(false);
                await WriteReadableResultAsync(item, output, depth + 1).ConfigureAwait(false);
            }
            if (index == 0)
            {
                await output.WriteLineAsync($"{indent}ℹ   None").ConfigureAwait(false);
            }
        }
        else
        {
            await output.WriteLineAsync($"{indent}•   {ReadableValue(element)}").ConfigureAwait(false);
        }
    }

    /// <summary>Selects recognizable four-column prefixes, padding narrow symbols differently from two-column emoji.</summary>
    private static string ResultIcon(string name) => name switch
    {
        "cases" or "case" => "📋  ",
        "reports" or "report" => "📨  ",
        "appeals" or "appeal" => "⚖   ",
        "members" or "team" or "actor" => "👤  ",
        "labels" or "label" or "value" => "🏷️  ",
        "issuer" or "did" => "🪪  ",
        "streamId" => "🌊  ",
        "issuedThrough" => "🏷️  ",
        "publishedThrough" => "📡  ",
        "replayFloor" => "⏮   ",
        "phase" => "🚦  ",
        "retirementUpperBound" => "🎯  ",
        "retirementThrough" => "🏁  ",
        "sequence" or "revision" => "#   ",
        "state" or "moderation" or "openCounts" => "ℹ   ",
        _ => "•   "
    };

    /// <summary>Keeps primitive field values readable without preserving terminal-control sequences from remote strings.</summary>
    private static string ReadableValue(JsonElement value) =>
        string.Concat((value.ValueKind == JsonValueKind.Null ? "(none)" : value.ToString())
            .Select(character => char.IsControl(character) ? ' ' : character));

    /// <summary>Limits terminal diagnostics to short protocol error identifiers rather than untrusted server text.</summary>
    private static string SafeServiceError(string? error) =>
        error is { Length: > 0 and <= 80 } && error.All(character => char.IsAsciiLetterOrDigit(character) || character == '_')
            ? error
            : "service error";

    /// <summary>Requires deliberate confirmation for mutations unless the user explicitly supplied <c>--yes</c>.</summary>
    private static async Task<bool> ConfirmAsync(bool confirmed, string prompt, CancellationToken cancellationToken)
    {
        if (confirmed)
        {
            return true;
        }

        if (Console.IsInputRedirected)
        {
            await Console.Error.WriteLineAsync("⚠   Mutation requires interactive confirmation or --yes.").ConfigureAwait(false);
            return false;
        }

        await Console.Error.WriteAsync($"⚠   {prompt} [y/N] ").ConfigureAwait(false);
        string? answer = await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Validates an account or record AT target and ensures CIDs are used only with record URIs.</summary>
    internal static bool ValidateLabelTarget(string target, string? cid)
    {
        if (target.StartsWith("at://", StringComparison.Ordinal))
        {
            if (!AtUri.TryParse(target, out _) || cid is not null && !Cid.TryParse(cid, out _))
            {
                return false;
            }

            return true;
        }

        return cid is null && AtIdentifier.TryParse(target, out _);
    }

    /// <summary>Validates a configured signing-key identifier before sending a rotation request.</summary>
    internal static bool IsValidKeyId(string? keyId) =>
        !string.IsNullOrWhiteSpace(keyId) &&
        keyId.Length <= 128 &&
        keyId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    /// <summary>Identifies read-only methods that use the remote administration GET routes.</summary>
    internal static bool IsRemoteGetMethod(string method) =>
        method is "getTeam" or "getPublicationState" or "getMaintenanceState" or "getRetirementState" or "getAudit" or "getOperation";

    /// <summary>Builds a remote administration URI with the required query for a read method.</summary>
    internal static Uri BuildRemoteRequestUri(
        Uri adminUrl,
        string lxm,
        string method,
        string operationId,
        string reason,
        long afterSequence,
        int limit)
    {
        Uri requestUri = new(adminUrl, $"/xrpc/{lxm}");
        string? query = method switch
        {
            "getAudit" => $"?afterSequence={afterSequence}&limit={limit}",
            "getOperation" => $"?operationId={Uri.EscapeDataString(operationId)}&reason={Uri.EscapeDataString(reason)}",
            _ => null
        };
        return query is null ? requestUri : new Uri(requestUri + query, UriKind.Absolute);
    }

    /// <summary>Parses the exact DID service reference required by the remote authorization protocol.</summary>
    internal static bool TryParseServiceReference(string? text, out AtProtoServiceReference? reference)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        int fragmentIndex = text.IndexOf('#', StringComparison.Ordinal);
        return fragmentIndex > 0 &&
            fragmentIndex < text.Length - 1 &&
            Did.TryParse(text[..fragmentIndex], out Did? did) &&
            TryCreateServiceReference(did, text[(fragmentIndex + 1)..], out reference);
    }

    /// <summary>Creates a validated service reference without allowing malformed fragments through parsing.</summary>
    private static bool TryCreateServiceReference(Did did, string fragment, out AtProtoServiceReference? reference)
    {
        try
        {
            reference = new AtProtoServiceReference(did, fragment);
            return true;
        }
        catch (ArgumentException)
        {
            reference = null;
            return false;
        }
    }

    /// <summary>Accepts only an HTTPS origin for authenticated administrative calls.</summary>
    internal static bool IsHttpsOrigin(Uri uri) =>
        uri.IsAbsoluteUri &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        uri.AbsolutePath is "" or "/" &&
        string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment);

    /// <summary>Restricts native OAuth metadata to an HTTPS URL on the configured labeler origin.</summary>
    internal static bool IsNativeClientMetadataUri(Uri uri, Uri adminOrigin) =>
        uri.IsAbsoluteUri &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment) &&
        string.Equals(uri.IdnHost, adminOrigin.IdnHost, StringComparison.OrdinalIgnoreCase) &&
        uri.Port == adminOrigin.Port;

    /// <summary>Discovers one administration service from the configured native OAuth metadata document.</summary>
    private static async Task<AtProtoServiceReference?> DiscoverAdminServiceAsync(Uri metadataUri, CancellationToken cancellationToken)
    {
        using SocketsHttpHandler handler = new() { AllowAutoRedirect = false };
        using HttpClient client = new(handler) { Timeout = TimeSpan.FromSeconds(15) };
        using HttpResponseMessage response = await client.GetAsync(
            metadataUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode ||
            response.Content.Headers.ContentLength is > MaximumOAuthMetadataSize)
        {
            return null;
        }

        Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable streamLifetime = stream.ConfigureAwait(false);
        using MemoryStream buffer = new();
        byte[] chunk = new byte[4096];
        while (true)
        {
            int count = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            if (buffer.Length + count > MaximumOAuthMetadataSize)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }

        OAuthMetadataDocument? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize(buffer.ToArray(), AdministrationJsonContext.Default.OAuthMetadataDocument);
        }
        catch (JsonException)
        {
            return null;
        }

        return metadata is not null &&
            string.Equals(metadata.ClientId, metadataUri.AbsoluteUri, StringComparison.Ordinal) &&
            TryDiscoverAdminService(metadata.Scope, out AtProtoServiceReference? audience)
                ? audience
                : null;
    }

    /// <summary>Accepts only administration RPC scopes that identify one exact, unambiguous DID service.</summary>
    internal static bool TryDiscoverAdminService(string? scope, out AtProtoServiceReference? audience)
    {
        const string rpcPrefix = "rpc:" + AdminMethodPrefix;
        audience = null;
        if (string.IsNullOrWhiteSpace(scope))
        {
            return false;
        }

        HashSet<string> candidates = new(StringComparer.Ordinal);
        foreach (string value in scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!value.StartsWith(rpcPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            int audienceStart = value.IndexOf("?aud=", StringComparison.Ordinal);
            if (audienceStart <= rpcPrefix.Length ||
                value.IndexOf('&', audienceStart) >= 0 ||
                value.IndexOf('?', audienceStart + 1) >= 0)
            {
                return false;
            }

            string encodedAudience = value[(audienceStart + "?aud=".Length)..];
            string decodedAudience = Uri.UnescapeDataString(encodedAudience);
            if (TryParseServiceReference(decodedAudience, out AtProtoServiceReference? candidate) &&
                candidate is not null)
            {
                candidates.Add(candidate.ToString());
            }
        }

        if (candidates.Count != 1)
        {
            return false;
        }

        return TryParseServiceReference(candidates.Single(), out audience) && audience is not null;
    }

    private const int MaximumOAuthMetadataSize = 64 * 1024;

    /// <summary>Parses a case-insensitive named role while rejecting undefined numeric values.</summary>
    internal static bool TryParseRole(string? text, out LabelTeamRole role) =>
        Enum.TryParse(text, ignoreCase: true, out role) && Enum.IsDefined(role);

    /// <summary>Parses a supported team action without accepting undefined enum values.</summary>
    internal static bool TryParseAction(string? text, out LabelTeamAction action) =>
        Enum.TryParse(text, ignoreCase: true, out action) &&
        Enum.IsDefined(action) &&
        action is not LabelTeamAction.Enroll;

    /// <summary>Requires action-specific role and OS credential options and rejects irrelevant arguments.</summary>
    internal static bool HasValidTeamChangeOptions(LabelTeamAction action, bool hasRole, bool hasCredential) =>
        (action == LabelTeamAction.SetRole) == hasRole &&
        (action is LabelTeamAction.BindLocal or LabelTeamAction.RevokeLocal) == hasCredential;

    /// <summary>Maps the shared administration response's classified errors to stable process exit codes.</summary>
    internal static int MapServiceErrorToExitCode(string? error) =>
        error switch
        {
            "Unauthorized" => 4,
            "InvalidRequest" => 2,
            _ => 5
        };

    /// <summary>Holds options shared by one command's local or remote transport.</summary>
    private sealed record TransportOptions(
        Option<string?> LocalEndpoint,
        Option<Uri?> AdminUrl,
        Option<string?> AudienceText,
        Option<string?> Handle,
        Option<string?> ClientId,
        Option<bool> RememberMe);

    /// <summary>Describes the existing administration protocol command without accepting caller identity.</summary>
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(AdministrationCommand))]
    [JsonSerializable(typeof(PlcPublicationCliResult))]
    [JsonSerializable(typeof(PlcRecoveryCliResult))]
    [JsonSerializable(typeof(OAuthMetadataDocument))]
    private sealed partial class AdministrationJsonContext : JsonSerializerContext;

    private sealed record OAuthMetadataDocument(
        [property: JsonPropertyName("client_id")] string? ClientId,
        [property: JsonPropertyName("scope")] string? Scope);

    /// <summary>Contains only public PLC publication recovery data.</summary>
    private sealed record PlcPublicationCliResult(string Did, string Stage, JsonElement? SignedOperation, string? RecoveryAction);

    /// <summary>Contains the outcome and recovery guidance for a saved public PLC operation.</summary>
    private sealed record PlcRecoveryCliResult(string Did, string Stage, int? StatusCode, string? RecoveryAction);

    /// <summary>Contains one command understood by the shared local and remote administration dispatchers.</summary>
    internal sealed record AdministrationCommand(
        string Method,
        string OperationId = "",
        string Reason = "",
        string Target = "",
        LabelTeamRole? Role = null,
        LabelTeamAction? Action = null,
        string? Credential = null,
        string Value = "",
        string? Cid = null,
        DateTimeOffset? ExpiresAt = null,
        long AfterSequence = 0,
        int Limit = 16,
        int GracePeriodDays = 0,
        bool Confirmed = false,
        string? ReplacementKeyId = null,
        PolicyCommand? Moderation = null,
        string? Confirmation = null,
        string? Attestation = null,
        string? Inventory = null,
        Guid? HostInstanceId = null);

    /// <summary>Separates a bounded administrative response from its presentation.</summary>
    internal sealed record AdministrationReply(string Body, HttpStatusCode StatusCode);

    /// <summary>Sends commands through one authenticated administration connection.</summary>
    internal delegate Task<AdministrationReply> AdministrationSender(AdministrationCommand command, CancellationToken token);

    /// <summary>Coordinates a bounded workflow without conflating administrator and labeler-account credentials.</summary>
    private delegate Task<int> AdministrationWorkflow(AdministrationSender sender, CancellationToken token);
}
