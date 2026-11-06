// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.CommandLine;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Agent;

using RemoteLabelerAgent = idunno.Bluesky.Labeler.Agent.LabelerAgent;

namespace Samples.LabelerAgent;

public sealed class Program
{
    private const string LabelSymbol = "\U0001F3F7";
    private const string NegationSymbol = "\U0001F6AB";
    private const string ExpirySymbol = "\u23F1\uFE0F";
    private const string SourceSymbol = "\U0001F4E1";
    private const string TargetSymbol = "\U0001F3AF";
    private const string CreatedSymbol = "\U0001F4C5";
    private const string CidSymbol = "\U0001F50E";
    private const string ErrorSymbol = "\u274C";

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        return await CreateRootCommand().Parse(args).InvokeAsync().ConfigureAwait(false);
    }

    /// <summary>Creates the sample commands and their argument validation.</summary>
    internal static RootCommand CreateRootCommand(TimeProvider? timeProvider = null)
    {
        timeProvider ??= TimeProvider.System;
        RootCommand rootCommand = new("Issue, negate or query labels, or list a labeler's supported labels.");

        (Command labelCommand, Option<string?> labelRecordOption, Option<string?> labelAccountOption, Option<string?> labelValueOption) = CreateMutationCommand(
            name: "label",
            description: "Issue a label for an account or record.",
            labelValueDescription: "The declared label value to issue.");
        (Command negateCommand, Option<string?> negateRecordOption, Option<string?> negateAccountOption, Option<string?> negateValueOption) = CreateMutationCommand(
            name: "negate",
            description: "Negate a label for an account or record.",
            labelValueDescription: "The declared label value to negate.");
        negateCommand.Aliases.Add("remove");
        Command listCommand = new("list", "List the labels supported by the labeler.");
        (Command queryCommand, Option<string?> queryRecordOption, Option<string?> queryAccountOption) = CreateTargetCommand(
            name: "query",
            description: "Query published labels for an account or record.");
        Option<string?> queryHostOption = AddLabelerHostOption(queryCommand);
        (Option<string?> labelUsernameOption, Option<string?> labelApiKeyOption, Option<string?> labelHostOption) =
            AddCommonOptions(labelCommand);
        (Option<string?> negateUsernameOption, Option<string?> negateApiKeyOption, Option<string?> negateHostOption) =
            AddCommonOptions(negateCommand);
        (Option<string?> listUsernameOption, Option<string?> listApiKeyOption, Option<string?> listHostOption) =
            AddCommonOptions(listCommand);
        Option<string?> expiresInOption = new("--expires-in")
        {
            Description = "An optional positive duration, in [d.]hh:mm:ss format, starting when the label is issued.",
            HelpName = "TimeSpan"
        };
        Option<string?> expiresAtOption = new("--expires-at")
        {
            Description = "An optional future date and time. Include Z or an offset; without one, local time is assumed.",
            HelpName = "DateTime"
        };
        labelCommand.Options.Add(expiresInOption);
        labelCommand.Options.Add(expiresAtOption);
        labelCommand.Validators.Add(result =>
        {
            if (!TryResolveExpiry(
                expiresIn: result.GetValue(expiresInOption),
                expiresAt: result.GetValue(expiresAtOption),
                timeProvider: timeProvider,
                expiry: out _,
                error: out string error))
            {
                result.AddError(FormatError(error));
            }
        });

        labelCommand.SetAction((parseResult, cancellationToken) => RunCommandAsync(
            command: LabelerCommand.Label,
            username: parseResult.GetValue(labelUsernameOption) ?? Environment.GetEnvironmentVariable("_BlueskyHandle"),
            apiKey: parseResult.GetValue(labelApiKeyOption) ?? Environment.GetEnvironmentVariable("_BlueskyPassword"),
            labelerHost: ParseLabelerHost(parseResult.GetValue(labelHostOption)),
            recordText: parseResult.GetValue(labelRecordOption),
            accountText: parseResult.GetValue(labelAccountOption),
            labelValue: parseResult.GetValue(labelValueOption),
            cancellationToken: cancellationToken,
            expiresIn: parseResult.GetValue(expiresInOption),
            expiresAt: parseResult.GetValue(expiresAtOption),
            timeProvider: timeProvider));
        negateCommand.SetAction((parseResult, cancellationToken) => RunCommandAsync(
            command: LabelerCommand.Negate,
            username: parseResult.GetValue(negateUsernameOption) ?? Environment.GetEnvironmentVariable("_BlueskyHandle"),
            apiKey: parseResult.GetValue(negateApiKeyOption) ?? Environment.GetEnvironmentVariable("_BlueskyPassword"),
            labelerHost: ParseLabelerHost(parseResult.GetValue(negateHostOption)),
            recordText: parseResult.GetValue(negateRecordOption),
            accountText: parseResult.GetValue(negateAccountOption),
            labelValue: parseResult.GetValue(negateValueOption),
            cancellationToken: cancellationToken));
        listCommand.SetAction((parseResult, cancellationToken) => RunCommandAsync(
            command: LabelerCommand.List,
            username: parseResult.GetValue(listUsernameOption) ?? Environment.GetEnvironmentVariable("_BlueskyHandle"),
            apiKey: parseResult.GetValue(listApiKeyOption) ?? Environment.GetEnvironmentVariable("_BlueskyPassword"),
            labelerHost: ParseLabelerHost(parseResult.GetValue(listHostOption)),
            recordText: null,
            accountText: null,
            labelValue: null,
            cancellationToken: cancellationToken));
        queryCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            using AtProtoAgent agent = AtProtoAgentBuilder.Create().Build();
            return await RunQueryAsync(
                agent: agent,
                labelerHost: ParseLabelerHost(parseResult.GetValue(queryHostOption)),
                recordText: parseResult.GetValue(queryRecordOption),
                accountText: parseResult.GetValue(queryAccountOption),
                output: Console.Out,
                error: Console.Error,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        });

        rootCommand.Subcommands.Add(labelCommand);
        rootCommand.Subcommands.Add(negateCommand);
        rootCommand.Subcommands.Add(listCommand);
        rootCommand.Subcommands.Add(queryCommand);

        return rootCommand;
    }

    /// <summary>Adds required issuer credentials and labeler host options to a command.</summary>
    private static (
        Option<string?> UsernameOption,
        Option<string?> ApiKeyOption,
        Option<string?> LabelerHostOption) AddCommonOptions(Command command)
    {
        Option<string?> usernameOption = new("--username", "-u")
        {
            Description = "The Bluesky handle or DID for the account that will issue labels. Defaults to _BlueskyHandle."
        };
        Option<string?> apiKeyOption = new("--api-key", "--apikey", "-p")
        {
            Description = "The app password for the Bluesky account. Defaults to _BlueskyPassword."
        };

        Option<string?> labelerHostOption = AddLabelerHostOption(command);

        command.Options.Add(usernameOption);
        command.Options.Add(apiKeyOption);
        command.Validators.Add(result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValue(usernameOption)) &&
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("_BlueskyHandle")))
            {
                result.AddError(FormatError("Specify --username or set _BlueskyHandle."));
            }

            if (string.IsNullOrWhiteSpace(result.GetValue(apiKeyOption)) &&
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("_BlueskyPassword")))
            {
                result.AddError(FormatError("Specify --api-key or set _BlueskyPassword."));
            }
        });

        return (usernameOption, apiKeyOption, labelerHostOption);
    }

    /// <summary>Adds the required labeler origin without requiring issuer credentials.</summary>
    private static Option<string?> AddLabelerHostOption(Command command)
    {
        Option<string?> option = new("--labeler-host")
        {
            Description = "The labeler's root HTTPS origin.",
            HelpName = "Uri",
            Required = true
        };
        option.Validators.Add(result =>
        {
            string? value = result.GetValue(option);
            if (value is not null && !IsValidLabelerHost(value))
            {
                result.AddError(FormatError("The labeler host must be a root HTTPS origin without credentials, query, or fragment."));
            }
        });
        command.Options.Add(option);

        return option;
    }

    /// <summary>Checks that a value is a root HTTPS origin without credentials, query, or fragment.</summary>
    private static bool IsValidLabelerHost(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
           uri is not null &&
           uri.Scheme == Uri.UriSchemeHttps &&
           uri.AbsolutePath == "/" &&
           uri.UserInfo.Length == 0 &&
           uri.Query.Length == 0 &&
           uri.Fragment.Length == 0;

    /// <summary>Parses the validated labeler host option.</summary>
    private static Uri ParseLabelerHost(string? value)
    {
        if (value is null || !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || uri is null)
        {
            throw new InvalidOperationException("The required labeler host option could not be parsed.");
        }

        return uri;
    }

    /// <summary>Creates a mutation command with the target and declared label options.</summary>
    private static (
        Command Command,
        Option<string?> RecordOption,
        Option<string?> AccountOption,
        Option<string?> LabelValueOption) CreateMutationCommand(
            string name,
            string description,
            string labelValueDescription)
    {
        (Command command, Option<string?> recordOption, Option<string?> accountOption) = CreateTargetCommand(name, description);
        Option<string?> labelValueOption = new("--label-value")
        {
            Description = labelValueDescription,
            HelpName = "value",
            Required = true
        };
        command.Options.Add(labelValueOption);

        return (command, recordOption, accountOption, labelValueOption);
    }

    /// <summary>Creates a command requiring exactly one complete record URI or account identifier.</summary>
    private static (
        Command Command,
        Option<string?> RecordOption,
        Option<string?> AccountOption) CreateTargetCommand(string name, string description)
    {
        Option<string?> recordOption = new("--aturi", "--post")
        {
            Description = "The complete AT URI of the target record.",
            HelpName = "AtUri"
        };
        recordOption.Validators.Add(result =>
        {
            string? value = result.GetValue(recordOption);
            if (!string.IsNullOrWhiteSpace(value) &&
                (!AtUri.TryParse(value, out AtUri? atUri) ||
                 atUri is null ||
                 atUri.Collection is null ||
                 atUri.RecordKey is null))
            {
                result.AddError(FormatError("The record must be a complete AT URI, such as at://did:plc:.../app.bsky.feed.post/..."));
            }
        });

        Option<string?> accountOption = new("--account")
        {
            Description = "The DID or handle of the target account.",
            HelpName = "DidOrHandle"
        };
        accountOption.Validators.Add(result =>
        {
            string? value = result.GetValue(accountOption);
            if (!string.IsNullOrWhiteSpace(value) && !AtIdentifier.TryParse(value, out _))
            {
                result.AddError(FormatError("The account must be a valid DID or handle."));
            }
        });

        Command command = new(name, description)
        {
            recordOption,
            accountOption
        };
        command.Validators.Add(result =>
        {
            bool hasRecord = !string.IsNullOrWhiteSpace(result.GetValue(recordOption));
            bool hasAccount = !string.IsNullOrWhiteSpace(result.GetValue(accountOption));
            if (hasRecord == hasAccount)
            {
                result.AddError(FormatError("Specify exactly one of --aturi or --account."));
            }
        });

        return (command, recordOption, accountOption);
    }

    /// <summary>Queries all published label pages without logging in or sending account credentials.</summary>
    internal static async Task<int> RunQueryAsync(
        AtProtoAgent agent,
        Uri labelerHost,
        string? recordText,
        string? accountText,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null,
        Func<Did, CancellationToken, Task<Handle>>? resolveHandle = null)
    {
        timeProvider ??= TimeProvider.System;
        resolveHandle ??= (identifier, token) => IdentityResolution.ResolveVerifiedHandleAsync(
            did: identifier,
            httpClient: agent.HttpClient,
            cancellationToken: token);
        Dictionary<Did, string> handles = [];
        AtUri? record = recordText is not null ? new AtUri(recordText) : null;
        AtIdentifier account = record?.Repo ?? AtIdentifier.Create(
            accountText ?? throw new InvalidOperationException("The query has no target."));
        Did? did = account as Did;
        if (did is null && account is Handle handle)
        {
            did = await agent.ResolveHandle(handle, cancellationToken).ConfigureAwait(false);
        }

        if (did is null)
        {
            await error.WriteLineAsync($"Could not resolve account '{account}' to a DID.").ConfigureAwait(false);
            return 1;
        }

        string subject = record is not null ? $"at://{did}/{record.Collection}/{record.RecordKey}" : did.Value;
        string? cursor = null;
        HashSet<string> cursors = new(StringComparer.Ordinal);
        bool foundLabels = false;
        do
        {
            AtProtoHttpResult<PagedReadOnlyCollection<Label>> result = await agent.QueryLabels(
                uriPatterns: [subject],
                sources: null,
                limit: 250,
                cursor: cursor,
                service: labelerHost,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                await error.WriteLineAsync(
                    $"Label query failed ({(int)result.StatusCode} {result.StatusCode}): " +
                    $"{result.AtErrorDetail?.Error ?? "no protocol error"}.").ConfigureAwait(false);
                return 1;
            }

            foreach (Label label in result.Result)
            {
                foundLabels = true;
                string source = await ResolveDisplayHandleAsync(
                    did: label.Source,
                    handles: handles,
                    resolveHandle: resolveHandle,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                string targetHandle = await ResolveDisplayHandleAsync(
                    did: did,
                    handles: handles,
                    resolveHandle: resolveHandle,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                string target = record is null ? targetHandle
                    : targetHandle.StartsWith('@')
                        ? $"at://{targetHandle[1..]}/{record.Collection}/{record.RecordKey}"
                        : $"Record (handle unavailable): {record.Collection}/{record.RecordKey}";
                await output.WriteLineAsync(FormatQueryLabel(
                    label: label,
                    source: source,
                    target: target,
                    timeProvider: timeProvider,
                    useAnsi: !Console.IsOutputRedirected)).ConfigureAwait(false);
            }

            cursor = result.Result.Cursor;
            if (!string.IsNullOrWhiteSpace(cursor) && !cursors.Add(cursor))
            {
                await error.WriteLineAsync("The labeler returned a repeated pagination cursor; the query is incomplete.").ConfigureAwait(false);
                return 1;
            }
        }
        while (!string.IsNullOrWhiteSpace(cursor));

        if (!foundLabels)
        {
            await output.WriteLineAsync("No labels found.").ConfigureAwait(false);
        }

        return 0;
    }

    /// <summary>Resolves each displayed identity once per query, without presenting unverified handles or raw DIDs.</summary>
    private static async Task<string> ResolveDisplayHandleAsync(
        Did did,
        Dictionary<Did, string> handles,
        Func<Did, CancellationToken, Task<Handle>> resolveHandle,
        CancellationToken cancellationToken)
    {
        if (!handles.TryGetValue(did, out string? display))
        {
            Handle handle = await resolveHandle(did, cancellationToken).ConfigureAwait(false);
            display = handle.IsValid ? $"@{handle}" : "Handle unavailable";
            handles.Add(did, display);
        }

        return display;
    }

    /// <summary>Formats a published label with readable identities, local dates and explicit withdrawal or expiry.</summary>
    internal static string FormatQueryLabel(
        Label label,
        string source,
        string target,
        TimeProvider timeProvider,
        bool useAnsi = false)
    {
        string created = FormatQueryTime(label.CreationTimestamp, timeProvider.LocalTimeZone);
        string expiry = "Does not expire";
        if (label.ExpiresAt is DateTimeOffset expiresAt)
        {
            string date = FormatQueryTime(expiresAt, timeProvider.LocalTimeZone);
            expiry = $"{(expiresAt <= timeProvider.GetUtcNow() ? "Expired" : "Expires")}: {date}";
        }

        StringBuilder text = new();
        string value = label.IsNegationLabel && useAnsi ? $"\u001b[9m{label.Value}\u001b[29m" : label.Value;
        text.AppendLine(CultureInfo.CurrentCulture, $"{LabelSymbol}  {value}");
        text.AppendLine(CultureInfo.CurrentCulture, $"{SourceSymbol}  Issued by: {source}");
        text.AppendLine(CultureInfo.CurrentCulture, $"{TargetSymbol}  Targeted at: {target}");
        if (label.Cid is not null)
        {
            text.AppendLine(CultureInfo.CurrentCulture, $"{CidSymbol}  CID: {label.Cid}");
        }

        if (label.IsNegationLabel)
        {
            text.AppendLine(CultureInfo.CurrentCulture, $"{NegationSymbol}  Negated");
        }

        string timestampCaption = label.IsNegationLabel ? "Negated at" : "Created";
        text.AppendLine(CultureInfo.CurrentCulture, $"{CreatedSymbol}  {timestampCaption}: {created}");

        if (!label.IsNegationLabel)
        {
            text.AppendLine(CultureInfo.CurrentCulture, $"{ExpirySymbol}  {expiry}");
        }

        return text.ToString();
    }

    /// <summary>Formats a friendly local timestamp followed by its explicitly identified UTC equivalent.</summary>
    private static string FormatQueryTime(DateTimeOffset timestamp, TimeZoneInfo localTimeZone)
    {
        string local = TimeZoneInfo.ConvertTime(timestamp, localTimeZone).ToString("f", CultureInfo.CurrentCulture);
        string utc = timestamp.UtcDateTime.ToString("f", CultureInfo.CurrentCulture);

        return $"{local} / {utc} UTC";
    }

    /// <summary>Authenticates to the labeler and runs the selected operation.</summary>
    private static async Task<int> RunCommandAsync(
        LabelerCommand command,
        string? username,
        string? apiKey,
        Uri labelerHost,
        string? recordText,
        string? accountText,
        string? labelValue,
        CancellationToken cancellationToken,
        string? expiresIn = null,
        string? expiresAt = null,
        TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine(FormatError("Specify --username or _BlueskyHandle, and --api-key or _BlueskyPassword."));
            return 2;
        }

        LabelTarget? target = null;
        string? targetDescription = null;
        if (command is LabelerCommand.Label or LabelerCommand.Negate)
        {
            if (string.IsNullOrWhiteSpace(labelValue) ||
                string.IsNullOrWhiteSpace(recordText) == string.IsNullOrWhiteSpace(accountText))
            {
                Console.Error.WriteLine(
                    FormatError($"The {command.ToString().ToLowerInvariant()} command requires --label-value and exactly one of --aturi or --account."));
                return 2;
            }

            if (recordText is not null)
            {
                if (!AtUri.TryParse(recordText, out AtUri? record) ||
                    record is null ||
                    record.Collection is null ||
                    record.RecordKey is null)
                {
                    Console.Error.WriteLine(FormatError("--aturi must identify a complete AT Protocol record."));
                    return 2;
                }

                target = LabelTarget.Record(record);
                targetDescription = record.ToString();
            }
            else
            {
                if (accountText is null || !AtIdentifier.TryParse(accountText, out AtIdentifier? account) || account is null)
                {
                    Console.Error.WriteLine(FormatError("--account must be a valid DID or handle."));
                    return 2;
                }

                target = LabelTarget.Account(account);
                targetDescription = account.ToString();
            }
        }

        // The labeler agent uses the account's PDS to request short-lived service-authentication
        // tokens. Create the account agent first so login, token refresh, and HTTP ownership are
        // handled by the same AT Protocol client used for those requests.
        using AtProtoAgent accountAgent = AtProtoAgentBuilder.Create().Build();
        AtProtoHttpResult<bool> login = await accountAgent.Login(
            identifier: username,
            password: apiKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!login.Succeeded)
        {
            Console.Error.WriteLine(FormatError($"Login failed ({(int)login.StatusCode} {login.StatusCode}): {login.AtErrorDetail?.Error ?? "no protocol error"}."));
            return 1;
        }

        // Discover the labeler's DID from its own server description. Service-authentication
        // audiences identify a service by DID, so the sample must not infer the issuer from the
        // host name or accept a separate, potentially mismatched DID from the command line.
        AtProtoHttpResult<idunno.AtProto.Server.ServerDescription> description =
            await accountAgent.DescribeServer(labelerHost, cancellationToken).ConfigureAwait(false);
        if (!description.Succeeded)
        {
            Console.Error.WriteLine(
                FormatError($"Could not describe labeler host ({(int)description.StatusCode} {description.StatusCode}): " +
                $"{description.AtErrorDetail?.Error ?? "no protocol error"}."));
            return 1;
        }

        // The builder combines this verified issuer DID with Audience.Issuance. All sample
        // commands therefore use the labeler_issuance audience, separate from labeler_admin.
        using RemoteLabelerAgent labeler = LabelerAgentBuilder.Create()
            .WithLabelerService(labelerHost, description.Result.Did)
            .WithAccountAgent(accountAgent)
            .Build();

        AtProtoHttpResult<IReadOnlyList<string>> availableLabels = await labeler.GetAvailableLabels(
            reason: "Read the labels supported by this labeler.",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!availableLabels.Succeeded)
        {
            Console.Error.WriteLine(
                FormatError($"Could not read available labels ({(int)availableLabels.StatusCode} {availableLabels.StatusCode}): " +
                $"{availableLabels.AtErrorDetail?.Error ?? "no protocol error"}."));
            return 1;
        }

        if (command is LabelerCommand.List)
        {
            foreach (string availableLabel in availableLabels.Result)
            {
                Console.WriteLine(availableLabel);
            }

            return 0;
        }

        string requestedLabel = labelValue ?? throw new InvalidOperationException("The selected mutation has no label value.");
        LabelTarget resolvedTarget = target ?? throw new InvalidOperationException("The selected mutation has no target.");
        if (!availableLabels.Result.Contains(requestedLabel, StringComparer.Ordinal))
        {
            Console.Error.WriteLine(
                FormatError($"Label '{requestedLabel}' is not declared by this labeler. Available labels: " +
                $"{string.Join(", ", availableLabels.Result)}."));
            return 2;
        }

        if (command is LabelerCommand.Negate)
        {
            LabelTarget? negationTarget = await ResolveNegationTargetAsync(
                target: resolvedTarget,
                resolveHandle: accountAgent.ResolveHandle,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (negationTarget is null)
            {
                Console.Error.WriteLine(FormatError("Could not resolve the target account's handle. No label was negated."));
                return 1;
            }

            resolvedTarget = negationTarget;
        }

        // The host still checks live team membership, issuance permission, target eligibility, and
        // policy. Supplying an available value is only an early validation, not authorization.
        // A stable operation ID lets a worker retry an uncertain call and recover its original receipt.
        if (!TryResolveExpiry(
            expiresIn: expiresIn,
            expiresAt: expiresAt,
            timeProvider: timeProvider ?? TimeProvider.System,
            expiry: out DateTimeOffset? expiry,
            error: out string expiryError))
        {
            Console.Error.WriteLine(FormatError(expiryError));
            return 2;
        }

        string operationId = Guid.NewGuid().ToString("N");
        AtProtoHttpResult<LabelOperationReceipt> result = command switch
        {
            LabelerCommand.Label => await labeler.IssueLabel(
                operationId: operationId,
                target: resolvedTarget,
                value: requestedLabel,
                reason: "Applied by the Samples.LabelerAgent command-line sample.",
                expiresAt: expiry,
                cancellationToken: cancellationToken).ConfigureAwait(false),
            LabelerCommand.Negate => await labeler.NegateLabel(
                operationId: operationId,
                target: resolvedTarget,
                value: requestedLabel,
                reason: "Negated by the Samples.LabelerAgent command-line sample.",
                cancellationToken: cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException("The selected command is not a label mutation.")
        };
        if (!result.Succeeded)
        {
            Console.Error.WriteLine(
                FormatError($"Label operation failed ({(int)result.StatusCode} {result.StatusCode}): " +
                $"{result.AtErrorDetail?.Error ?? "no protocol error"}."));
            return 1;
        }

        Console.WriteLine(FormatMutationResult(
            negate: command is LabelerCommand.Negate,
            changed: result.Result.Changed,
            labelValue: requestedLabel,
            target: targetDescription ?? throw new InvalidOperationException("The selected mutation has no target description.")));
        return 0;
    }

    /// <summary>Validates an optional expiry and converts a relative duration using the current issuance time.</summary>
    internal static bool TryResolveExpiry(
        string? expiresIn,
        string? expiresAt,
        TimeProvider timeProvider,
        out DateTimeOffset? expiry,
        out string error)
    {
        expiry = null;
        error = string.Empty;
        if (expiresIn is not null && expiresAt is not null)
        {
            error = "Specify only one of --expires-in or --expires-at.";
            return false;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (expiresIn is not null)
        {
            if (!TimeSpan.TryParse(expiresIn, CultureInfo.InvariantCulture, out TimeSpan duration))
            {
                error = "--expires-in must be a duration in [d.]hh:mm:ss format.";
                return false;
            }

            if (duration <= TimeSpan.Zero)
            {
                error = "--expires-in must be a positive duration.";
                return false;
            }

            if (duration > DateTimeOffset.MaxValue - now)
            {
                error = "--expires-in exceeds the supported date range.";
                return false;
            }

            expiry = now.Add(duration);
        }
        else if (expiresAt is not null)
        {
            if (!DateTimeOffset.TryParse(
                expiresAt,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out DateTimeOffset absolute))
            {
                error = "--expires-at must be a valid date and time.";
                return false;
            }

            expiry = absolute;
        }

        if (expiry is DateTimeOffset timestamp && timestamp <= now)
        {
            error = "--expires-at must be in the future.";
            return false;
        }

        return true;
    }

    /// <summary>Resolves handle targets for negation while preserving the original record and version scope.</summary>
    internal static async Task<LabelTarget?> ResolveNegationTargetAsync(
        LabelTarget target,
        Func<Handle, CancellationToken, Task<Did?>> resolveHandle,
        CancellationToken cancellationToken)
    {
        if (target.Repository is not Handle handle)
        {
            return target;
        }

        Did? did = await resolveHandle(handle, cancellationToken).ConfigureAwait(false);
        if (did is null)
        {
            return null;
        }

        return target.Kind == LabelTargetKind.Record
            ? LabelTarget.Record(new AtUri($"at://{did}/{target.RecordUri!.Collection}/{target.RecordUri.RecordKey}"), target.Cid)
            : LabelTarget.Account(did);
    }

    /// <summary>Describes a successful label mutation or no-op without exposing receipt internals.</summary>
    internal static string FormatMutationResult(bool negate, bool changed, string labelValue, string target)
    {
        string outcome = (negate, changed) switch
        {
            (false, true) => "Label applied",
            (false, false) => "Already applied",
            (true, true) => "Label negated",
            (true, false) => "Nothing to negate"
        };

        string symbol = negate ? NegationSymbol : LabelSymbol;

        return $"{symbol}  {outcome}: '{labelValue}' for {target}.";
    }

    /// <summary>Prefixes a command error with a consistently spaced error symbol.</summary>
    internal static string FormatError(string message) => $"{ErrorSymbol}  {message}";

    /// <summary>Names the operations exposed by this sample.</summary>
    private enum LabelerCommand
    {
        Label,
        Negate,
        List
    }
}
