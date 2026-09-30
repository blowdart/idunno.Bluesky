// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Globalization;
using System.Net;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Firehose;
using idunno.AtProto.Labels;
using idunno.AtProto.Sync;
using idunno.Bluesky;
using idunno.Bluesky.Labeler;

using Microsoft.Extensions.Logging.Abstractions;

namespace Samples.ModerationLabels;

public sealed class Program
{
    // moderation.bsky.app, the Bluesky moderation service.
    private const string ModerationLabelerDid = "did:plc:ar7c4by46qjdydhdevvrndac";

    // The service entry in a labeler's DID document which carries its subscribeLabels endpoint.
    private const string LabelerServiceType = "AtprotoLabeler";

    private const string LabelerServiceId = "#atproto_labeler";

    // Every labeler publishes a declaration in this collection, so a relay can enumerate them.
    private const string LabelerServiceCollection = "app.bsky.labeler.service";

    // listReposByCollection is only implemented by relays and collection directories, not by personal data servers.
    private static readonly Uri s_relay = new("https://relay1.us-west.bsky.network");

    // Dead labelers are common, so a liveness check needs a short timeout of its own.
    private static readonly TimeSpan s_livenessTimeout = TimeSpan.FromSeconds(5);

    // Console.ReadKey() blocks, so the quit key is polled for instead, leaving the firehose free to run.
    private static readonly TimeSpan s_quitKeyPollInterval = TimeSpan.FromMilliseconds(100);

    private const int MaximumConcurrentLookups = 16;

    // Used when the console has no width of its own, because its output is redirected.
    private const int DefaultConsoleWidth = 80;

    private const string LabelSymbol = "\U0001F3F7";

    private const string CircledTimesSymbol = "\u2A02";

    private const string StopwatchSymbol = "\u23F1";

    private const string EllipsisSymbol = "\u2026";

    // Shown in place of a character in remote text which is not safe to write to a terminal.
    private const string ReplacementSymbol = "\uFFFD";

    // U+FE0F, the variation selector which asks for a character to be drawn as an emoji rather than as text.
    private const int EmojiPresentationSelector = 0xFE0F;

    // U+20E3, which turns the character before it into a keycap.
    private const int CombiningEnclosingKeycap = 0x20E3;

    // A pair of characters from this range is drawn as a single flag.
    private const int RegionalIndicatorFirst = 0x1F1E6;

    private const int RegionalIndicatorLast = 0x1F1FF;

    static async Task<int> Main(string[] args)
    {
        Option<string> labelerOption = new("--labeler", "-l", "/l")
        {
            Description = $"The DID or handle of the labeler to watch (defaults to the Bluesky moderation service, {ModerationLabelerDid}).",
            DefaultValueFactory = _ => ModerationLabelerDid,
            HelpName = "did|handle"
        };
        labelerOption.Validators.Add(result =>
        {
            string? value = result.GetValue(labelerOption);

            if (string.IsNullOrWhiteSpace(value) || !AtIdentifier.TryParse(value, out _))
            {
                result.AddError($"'{value}' is not a valid DID or handle.");
            }
        });

        Option<bool> listOption = new("--list")
        {
            Description = "List every labeler which has published a labeler declaration, with its handle and label values, then exit."
        };

        Option<bool> liveOption = new("--live")
        {
            Description = "With --list, query each labeler and list only those which answer."
        };

        Option<bool> labelsOption = new("--labels")
        {
            Description = "Print every label the labeler declares it can emit, one per line, then exit."
        };

        RootCommand rootCommand = new("Watch the labels a labeler applies and negates, or list the labelers which publish them.")
        {
            labelerOption,
            listOption,
            liveOption,
            labelsOption
        };
        rootCommand.Validators.Add(result =>
        {
            bool listing = result.GetValue(listOption);

            if (result.GetValue(liveOption) && !listing)
            {
                result.AddError("--live can only be used with --list.");
            }

            // An explicit labeler is meaningless when listing them all; an implicit default is not.
            if (listing && result.GetResult(labelerOption) is { Implicit: false })
            {
                result.AddError("--labeler cannot be used with --list.");
            }

            if (listing && result.GetValue(labelsOption))
            {
                result.AddError("--labels cannot be used with --list.");
            }
        });
        rootCommand.SetAction((parseResult, cancellationToken) =>
            parseResult.GetValue(listOption)
                ? ListLabelersAsync(parseResult.GetValue(liveOption), cancellationToken)
                : parseResult.GetValue(labelsOption)
                    ? PrintLabelsAsync(AtIdentifier.Create(parseResult.GetValue(labelerOption)!), cancellationToken)
                    : WatchLabelsAsync(AtIdentifier.Create(parseResult.GetValue(labelerOption)!), cancellationToken));

        return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
    }

    private static async Task<int> WatchLabelsAsync(AtIdentifier labeler, CancellationToken parseCancellationToken)
    {
        using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parseCancellationToken);
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };
        Console.OutputEncoding = Encoding.UTF8;

        Did labelerDid;
        Uri labelerService;

        try
        {
            (labelerDid, labelerService) = await ResolveLabelerAsync(labeler, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(Sanitize(ex.Message));
            return 1;
        }

        await using var firehose = new AtProtoFirehose(
            options: new FirehoseOptions
            {
                LabelerUri = labelerService,
                LoggerFactory = NullLoggerFactory.Instance
            });

        // Ctrl+C stops the sample, but it also tears down the pipeline of the shell which launched it, which some
        // prompts then report as a failed command. Quitting with a key press lets the sample exit normally instead.
        bool listenForQuitKey = !Console.IsInputRedirected;

        Console.WriteLine(
            $"Watching labels from {labelerDid} at {Sanitize(labelerService.ToString())}. " +
            $"Press {(listenForQuitKey ? "Q" : "Ctrl+C")} to stop.");

        IReadOnlyList<string>? declaredLabelValues;

        try
        {
            declaredLabelValues = await GetDeclaredLabelValuesAsync(labelerDid, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }

        if (declaredLabelValues is null)
        {
            Console.WriteLine($"Could not read the declaration for {labelerDid}, so the labels it can emit are unknown.");
        }
        else if (declaredLabelValues.Count != 0)
        {
            string prefix = $"{labelerDid} declares it can emit: ";

            Console.WriteLine($"{prefix}{FormatLabelValues(declaredLabelValues, DisplayWidth(prefix))}");
        }
        else
        {
            Console.WriteLine($"{labelerDid} does not declare the labels it can emit.");
        }

        Task quitKeyWatcher = listenForQuitKey
            ? WatchForQuitKeyAsync(cancellationTokenSource, cancellationToken)
            : Task.CompletedTask;

        try
        {
            await foreach (FirehoseEvent evt in firehose.SubscribeLabelsAsync(cancellationToken: cancellationToken))
            {
                switch (evt)
                {
                    case FirehoseLabelsEvent labelsEvent:
                        foreach (Label label in labelsEvent.Labels)
                        {
                            // A labeler normally only emits its own labels, but only show those issued by the labeler being watched.
                            if (label.Source != labelerDid)
                            {
                                continue;
                            }

                            WriteLabel(label);
                        }

                        break;

                    case FirehoseInfoEvent infoEvent:
                        Console.WriteLine($"INFO: {Sanitize(infoEvent.Name)} {Sanitize(infoEvent.Message)}");
                        break;

                    case FirehoseInvalidEvent invalidEvent:
                        Console.WriteLine(
                            $"INVALID: Ignored an invalid {Sanitize(invalidEvent.Type)} event: {Sanitize(invalidEvent.Reason)}");
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (FirehoseConnectionException ex)
        {
            Console.Error.WriteLine($"The labeler refused the connection: {Sanitize(ex.Message)}");
            return 1;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Gave up reconnecting: {Sanitize(ex.Message)}");
            return 1;
        }
        finally
        {
            // Stop the key watcher, whether the firehose ended because the user quit or because it failed.
            await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            await quitKeyWatcher.ConfigureAwait(false);
        }

        return 0;
    }

    // Prints the labels a single labeler declares it can emit, one per line, then exits.
    private static async Task<int> PrintLabelsAsync(AtIdentifier labeler, CancellationToken parseCancellationToken)
    {
        using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parseCancellationToken);
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };
        Console.OutputEncoding = Encoding.UTF8;

        Did labelerDid;

        try
        {
            (labelerDid, _) = await ResolveLabelerAsync(labeler, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(Sanitize(ex.Message));
            return 1;
        }

        IReadOnlyList<string>? declaredLabelValues;

        try
        {
            declaredLabelValues = await GetDeclaredLabelValuesAsync(labelerDid, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }

        if (declaredLabelValues is null)
        {
            Console.Error.WriteLine($"Could not read the declaration for {labelerDid}, so the labels it can emit are unknown.");
            return 1;
        }

        foreach (string labelValue in declaredLabelValues)
        {
            Console.WriteLine(Sanitize(labelValue));
        }

        return 0;
    }

    // Polls for a quit key, rather than blocking on Console.ReadKey(), so the firehose keeps being read.
    private static async Task WatchForQuitKeyAsync(CancellationTokenSource cancellationTokenSource, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key is ConsoleKey.Q or ConsoleKey.Escape)
                {
                    await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
                    return;
                }

                await Task.Delay(s_quitKeyPollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The firehose stopped for its own reasons, so there is nothing left to quit.
        }
        catch (InvalidOperationException)
        {
            // The console input was redirected after the check which started this, so no key can arrive.
        }
    }

    // A labeler publishes its subscribeLabels endpoint as an AtprotoLabeler service in its DID document.
    private static async Task<(Did Did, Uri Service)> ResolveLabelerAsync(
        AtIdentifier labeler,
        CancellationToken cancellationToken)
    {
        Did? did = labeler switch
        {
            Did labelerDid => labelerDid,
            Handle handle => await Resolution.ResolveHandle(handle, loggerFactory: NullLoggerFactory.Instance, cancellationToken: cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"'{labeler}' is not a DID or a handle.")
        };

        cancellationToken.ThrowIfCancellationRequested();
        did = did ?? throw new InvalidOperationException($"Could not resolve the handle '{labeler}' to a DID.");

        DidDocument? didDocument = await Resolution.ResolveDidDocument(did, loggerFactory: NullLoggerFactory.Instance, cancellationToken: cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        didDocument = didDocument ?? throw new InvalidOperationException($"Could not resolve the DID document for {did}.");

        // A DID document comes from whoever controls the DID, and neither JsonRequired nor RespectNullableAnnotations
        // reaches inside a collection, so an entry can be null however the property is annotated. The same limitation
        // is handled in the SDK by AtProtoServer.WithoutNullEntries. A missing serviceEndpoint lands the same way,
        // because nothing is assigned to check. Treat either as a malformed entry rather than a labeler service.
        foreach (DidDocService? service in didDocument.Services ?? [])
        {
            if (service?.ServiceEndpoint is not Uri endpoint || service.Id is null)
            {
                continue;
            }

            if (service.Type == LabelerServiceType || service.Id.EndsWith(LabelerServiceId, StringComparison.Ordinal))
            {
                // A DID document is published by whoever controls the DID, so its endpoint is untrusted. Anything which
                // is not an absolute HTTP(S) URL with a host cannot be queried or subscribed to, and passing one on
                // would throw from somewhere further away, so refuse it here as a labeler which cannot be used.
                if (!endpoint.IsAbsoluteUri ||
                    (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps) ||
                    string.IsNullOrEmpty(endpoint.Host) ||
                    !string.IsNullOrEmpty(endpoint.UserInfo))
                {
                    throw new InvalidOperationException(
                        $"{did} declares a labeler service endpoint which is not an absolute http or https URL, so it cannot be used.");
                }

                return (did, endpoint);
            }
        }

        throw new InvalidOperationException($"{did} does not declare a labeler service, so it is not a labeler.");
    }

    private static async Task<int> ListLabelersAsync(bool liveOnly, CancellationToken parseCancellationToken)
    {
        using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parseCancellationToken);
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };
        Console.OutputEncoding = Encoding.UTF8;

        try
        {
            Console.WriteLine($"Asking {s_relay.Host} which repositories publish {LabelerServiceCollection} records...");

            IReadOnlyList<Did> labelerDids = await ListLabelerDidsAsync(cancellationToken).ConfigureAwait(false);

            if (labelerDids.Count == 0)
            {
                Console.Error.WriteLine("The relay returned no labeler declarations.");
                return 1;
            }

            Console.WriteLine($"Found {labelerDids.Count} labeler declarations. Getting their details...");

            (IReadOnlyDictionary<Did, LabelerView> views, IReadOnlySet<Did> unavailable) =
                await GetLabelerViewsAsync(labelerDids, cancellationToken).ConfigureAwait(false);

            if (unavailable.Count != 0)
            {
                Console.Error.WriteLine(
                    $"The appview did not answer for {unavailable.Count} labeler{(unavailable.Count == 1 ? string.Empty : "s")}, so their details are unknown.");
            }

            // A labeler whose handle no longer resolves in both directions is reported as handle.invalid, which cannot be used
            // to reach it, so there is nothing useful to show for it. A labeler the appview did not answer for is not excluded,
            // because nothing is known about its handle either way.
            IReadOnlyList<Did> resolvedDids = [.. labelerDids.Where(did => views.GetValueOrDefault(did)?.Creator.Handle.IsValid != false)];

            int excludedCount = labelerDids.Count - resolvedDids.Count;

            if (excludedCount != 0)
            {
                Console.WriteLine($"Excluding {excludedCount} labeler{(excludedCount == 1 ? string.Empty : "s")} whose handle does not resolve.");
            }

            IReadOnlyList<Did> didsToShow = resolvedDids;

            if (liveOnly)
            {
                Console.WriteLine($"Querying each labeler, with a {s_livenessTimeout.TotalSeconds:N0} second timeout, to see which are live...");
                didsToShow = await FilterToLiveLabelersAsync(resolvedDids, cancellationToken).ConfigureAwait(false);
            }

            Console.WriteLine();

            foreach (Did did in didsToShow)
            {
                WriteLabelerSummary(did, views.GetValueOrDefault(did), unavailable.Contains(did));
            }

            Console.WriteLine();

            if (liveOnly)
            {
                Console.WriteLine($"{didsToShow.Count} of {resolvedDids.Count} declared labelers answered.");
            }
            else
            {
                Console.WriteLine($"{didsToShow.Count} labelers have published a declaration. Not all of them are still running; use --live to check.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(Sanitize(ex.Message));
            return 1;
        }

        return 0;
    }

    // Only relays and collection directories implement listReposByCollection; a personal data server returns an error.
    private static async Task<IReadOnlyList<Did>> ListLabelerDidsAsync(CancellationToken cancellationToken)
    {
        using AtProtoAgent agent = new(s_relay, new AtProtoAgentOptions { LoggerFactory = NullLoggerFactory.Instance });

        List<Did> dids = [];
        string? cursor = null;

        do
        {
            AtProtoHttpResult<PagedDidCollection> page = await agent.ListReposByCollection(
                collection: LabelerServiceCollection,
                limit: 2000,
                cursor: cursor,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!page.Succeeded)
            {
                // The error name and message come from the server, and the caller prints them, so sanitize them here
                // rather than at the point they are written, where their origin is no longer obvious.
                throw new InvalidOperationException(
                    $"{s_relay} could not list {LabelerServiceCollection} repositories: {page.StatusCode} " +
                    $"{Sanitize(page.AtErrorDetail?.Error)} {Sanitize(page.AtErrorDetail?.Message)}".TrimEnd());
            }

            dids.AddRange(page.Result);

            // The cursor, not the size of the page, says whether there is more to come: a relay can return an empty
            // page and still continue. Stopping when a cursor repeats guards the one hazard that introduces, which is
            // a server which never advances it turning this into an endless loop.
            string? nextCursor = page.Result.Cursor;

            cursor = nextCursor != cursor ? nextCursor : null;
        } while (!string.IsNullOrEmpty(cursor));

        return dids;
    }

    // app.bsky.labeler.getServices needs no authentication, so the agent calls the public appview anonymously.
    // A chunk which fails is reported separately from one which simply had nothing to say, because the two mean
    // different things: the appview not knowing a labeler is a fact about that labeler, whereas a failed request
    // says nothing about it at all, and treating the second as the first quietly reports guesses as findings.
    private static async Task<(IReadOnlyDictionary<Did, LabelerView> Views, IReadOnlySet<Did> Unavailable)> GetLabelerViewsAsync(
        IReadOnlyList<Did> labelers,
        CancellationToken cancellationToken)
    {
        using BlueskyAgent agent = new(new BlueskyAgentOptions { LoggerFactory = NullLoggerFactory.Instance });

        Dictionary<Did, LabelerView> views = [];
        HashSet<Did> unavailable = [];

        foreach (Did[] chunk in labelers.Chunk(Maximum.ProfilesToGet))
        {
            // A single request covers a whole chunk, so one transient failure would otherwise misreport every labeler
            // in it. Retry once before giving up, which is enough for the occasional failure seen in practice.
            AtProtoHttpResult<ICollection<LabelerView>>? result = null;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    result = await agent.GetLabelerServices(chunk, getDetailedViews: true, cancellationToken).ConfigureAwait(false);

                    if (result.Succeeded)
                    {
                        break;
                    }
                }
                catch (HttpRequestException)
                {
                    // Treat a failed connection like an unsuccessful response so the next attempt can retry it.
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // An HTTP timeout is not a request to stop listing labelers.
                }
            }

            if (result is null || !result.Succeeded)
            {
                unavailable.UnionWith(chunk);
                continue;
            }

            foreach (LabelerView view in result.Result)
            {
                views[view.Creator.Did] = view;
            }
        }

        return (views, unavailable);
    }

    // A labeler which still answers queryLabels is running; a declaration on its own only proves it once was.
    // Most declared labelers are gone, so the probes log nothing: a failure here is an answer, not a problem.
    private static async Task<IReadOnlyList<Did>> FilterToLiveLabelersAsync(
        IReadOnlyList<Did> labelers,
        CancellationToken cancellationToken)
    {
        System.Collections.Concurrent.ConcurrentBag<Did> live = [];

        await Parallel.ForEachAsync(
            labelers,
            new ParallelOptions { MaxDegreeOfParallelism = MaximumConcurrentLookups, CancellationToken = cancellationToken },
            async (labeler, token) =>
            {
                if (await IsLabelerLiveAsync(labeler, token).ConfigureAwait(false))
                {
                    live.Add(labeler);
                }
            }).ConfigureAwait(false);

        // Parallel completion order is arbitrary, so restore the relay's ordering.
        HashSet<Did> liveSet = [.. live];

        return [.. labelers.Where(liveSet.Contains)];
    }

    private static async Task<bool> IsLabelerLiveAsync(Did labeler, CancellationToken cancellationToken)
    {
        // Resolving a DID document reaches out to a directory, or to the labeler's own host for did:web, and that
        // lookup is as likely to hang as the query which follows it, so both share the one liveness timeout.
        using CancellationTokenSource timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutTokenSource.CancelAfter(s_livenessTimeout);

        CancellationToken timeoutToken = timeoutTokenSource.Token;

        try
        {
            (_, Uri labelerService) = await ResolveLabelerAsync(labeler, timeoutToken).ConfigureAwait(false);

            using AtProtoAgent agent = new(
                labelerService,
                new AtProtoAgentOptions
                {
                    LoggerFactory = NullLoggerFactory.Instance,
                    HttpClientOptions = new HttpClientOptions { Timeout = s_livenessTimeout }
                });

            AtProtoHttpResult<PagedReadOnlyCollection<Label>> result = await agent.QueryLabels(
                uriPatterns: ["*"],
                sources: null,
                limit: 1,
                cursor: null,
                service: labelerService,
                cancellationToken: timeoutToken).ConfigureAwait(false);

            // A refusal still proves something is listening; only a dead host or a broken endpoint does not.
            return result.Succeeded || result.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The liveness timeout expired, so nothing answered in time.
            return false;
        }
        catch (InvalidOperationException)
        {
            // The labeler does not declare a labeler service, so there is nothing to query.
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private static void WriteLabelerSummary(Did did, LabelerView? view, bool detailsUnavailable)
    {
        ConsoleColor originalColor = Console.ForegroundColor;

        if (view is not null)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"@{view.Creator.Handle}");
            Console.ForegroundColor = originalColor;

            if (view.Creator.DisplayName is string displayName && !string.IsNullOrWhiteSpace(displayName))
            {
                Console.Write($" ({Sanitize(displayName)})");
            }

            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"  {did}");
            Console.ForegroundColor = originalColor;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(did);
            Console.ForegroundColor = originalColor;
            Console.WriteLine(detailsUnavailable
                ? " (the appview did not answer, so its details are unknown)"
                : " (the appview has no record of this labeler)");
        }

        if (view is LabelerViewDetailed detailed)
        {
            IReadOnlyList<string> labelValues = [.. detailed.Policies.LabelValues.Order(StringComparer.Ordinal)];

            if (labelValues.Count != 0)
            {
                string prefix = $"  {LabelSymbol}  {labelValues.Count} label {(labelValues.Count == 1 ? "value" : "values")}: ";

                Console.WriteLine($"{prefix}{FormatLabelValues(labelValues, DisplayWidth(prefix))}");
            }
        }
    }

    // The console wraps a line longer than its width, so only show the label values which fit on the rest of the line.
    private static string FormatLabelValues(IReadOnlyList<string> labelValues, int prefixWidth)
    {
        // Writing to the final column wraps in some terminals, so stop one short of it.
        int budget = ConsoleWidth - prefixWidth - 1;

        // There is no room for anything at all, or only room to say that everything was left out.
        if (budget <= 0)
        {
            return string.Empty;
        }

        if (budget == 1)
        {
            return EllipsisSymbol;
        }

        StringBuilder builder = new();
        int shown = 0;
        int width = 0;

        foreach (string labelValue in labelValues)
        {
            // A label value is remote text, so what is measured has to be what is written.
            string sanitized = Sanitize(labelValue);
            int sanitizedWidth = DisplayWidth(sanitized);

            int remaining = labelValues.Count - shown - 1;
            int remainingWidth = remaining == 0 ? 0 : $", and {remaining} more".Length;

            if (shown != 0 && width + ", ".Length + sanitizedWidth + remainingWidth > budget)
            {
                break;
            }

            if (shown != 0)
            {
                builder.Append(", ");
                width += ", ".Length;
            }

            builder.Append(sanitized);
            width += sanitizedWidth;
            shown++;
        }

        if (shown != labelValues.Count)
        {
            builder.Append(CultureInfo.CurrentCulture, $", and {labelValues.Count - shown} more");
        }

        string formatted = builder.ToString();

        // The first value is always shown, even when it is longer than the budget on its own, so trim whatever is left over.
        if (DisplayWidth(formatted) > budget)
        {
            formatted = TruncateToWidth(formatted, budget - 1) + EllipsisSymbol;
        }

        return formatted;
    }

    // A terminal lays text out in cells, not in UTF-16 code units. A CJK ideograph or an emoji occupies two of them,
    // and a combining mark none at all, so counting string.Length would both wrap lines and truncate short.
    private static int DisplayWidth(string value)
    {
        int width = 0;

        foreach (string grapheme in EnumerateGraphemes(value))
        {
            width += GraphemeWidth(grapheme);
        }

        return width;
    }

    // Cuts text to a number of terminal cells, never splitting a grapheme cluster, and so never splitting a surrogate
    // pair or orphaning a combining mark from the character it belongs to.
    private static string TruncateToWidth(string value, int maximumWidth)
    {
        StringBuilder builder = new();
        int width = 0;

        foreach (string grapheme in EnumerateGraphemes(value))
        {
            int graphemeWidth = GraphemeWidth(grapheme);

            if (width + graphemeWidth > maximumWidth)
            {
                break;
            }

            builder.Append(grapheme);
            width += graphemeWidth;
        }

        return builder.ToString();
    }

    private static IEnumerable<string> EnumerateGraphemes(string value)
    {
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);

        while (enumerator.MoveNext())
        {
            yield return enumerator.GetTextElement();
        }
    }

    // A cluster is drawn as one glyph. Emoji presentation is a property of the cluster rather than of any one rune in
    // it: a variation selector, a keycap combiner or a pair of regional indicators all turn characters which are one
    // cell on their own into a single double width glyph, so those forms are recognised before falling back to the
    // width of the character the cluster is built around.
    private static int GraphemeWidth(string grapheme)
    {
        bool firstRune = true;

        foreach (Rune rune in grapheme.EnumerateRunes())
        {
            switch (rune.Value)
            {
                // A variation selector requesting emoji presentation, or the combiner which builds a keycap.
                case EmojiPresentationSelector:
                case CombiningEnclosingKeycap:
                    return 2;

                // A flag is a pair of regional indicators drawn as one double width glyph.
                case >= RegionalIndicatorFirst and <= RegionalIndicatorLast when firstRune:
                    return 2;
            }

            firstRune = false;
        }

        foreach (Rune rune in grapheme.EnumerateRunes())
        {
            int runeWidth = RuneWidth(rune);

            if (runeWidth != 0)
            {
                return runeWidth;
            }
        }

        return 0;
    }

    private static int RuneWidth(Rune rune)
    {
        // A mark is drawn onto the character before it, and a format character is not drawn at all.
        if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
        {
            return 0;
        }

        // The East Asian Wide and Fullwidth ranges, plus the emoji blocks which are drawn at the same size. This is the
        // Unicode East Asian Width property reduced to the ranges which occur in practice, rather than the full table.
        return rune.Value switch
        {
            >= 0x1100 and <= 0x115F => 2,
            0x231A or 0x231B => 2,
            >= 0x23E9 and <= 0x23EC => 2,
            0x23F0 or 0x23F3 => 2,
            0x25FD or 0x25FE => 2,
            0x2614 or 0x2615 => 2,
            >= 0x2648 and <= 0x2653 => 2,
            0x267F or 0x2693 or 0x26A1 => 2,
            0x26AA or 0x26AB or 0x26BD or 0x26BE or 0x26C4 or 0x26C5 => 2,
            0x26CE or 0x26D4 or 0x26EA or 0x26F2 or 0x26F3 or 0x26F5 or 0x26FA or 0x26FD => 2,
            0x2705 or 0x270A or 0x270B or 0x2728 or 0x274C or 0x274E => 2,
            >= 0x2753 and <= 0x2755 => 2,
            0x2757 => 2,
            >= 0x2795 and <= 0x2797 => 2,
            0x27B0 or 0x27BF => 2,
            0x2B1B or 0x2B1C or 0x2B50 or 0x2B55 => 2,
            >= 0x2E80 and <= 0x303E => 2,
            >= 0x3041 and <= 0x33FF => 2,
            >= 0x3400 and <= 0x4DBF => 2,
            >= 0x4E00 and <= 0x9FFF => 2,
            >= 0xA000 and <= 0xA4CF => 2,
            >= 0xA960 and <= 0xA97F => 2,
            >= 0xAC00 and <= 0xD7A3 => 2,
            >= 0xF900 and <= 0xFAFF => 2,
            >= 0xFE10 and <= 0xFE19 => 2,
            >= 0xFE30 and <= 0xFE6F => 2,
            >= 0xFF00 and <= 0xFF60 => 2,
            >= 0xFFE0 and <= 0xFFE6 => 2,
            0x16FE0 or 0x16FE1 or 0x16FE2 or 0x16FE3 => 2,
            >= 0x17000 and <= 0x18AFF => 2,
            0x1F004 or 0x1F0CF or 0x1F18E => 2,
            >= 0x1F191 and <= 0x1F19A => 2,
            >= 0x1F200 and <= 0x1F2FF => 2,
            >= 0x1F300 and <= 0x1F64F => 2,
            >= 0x1F680 and <= 0x1F6FF => 2,
            >= 0x1F7E0 and <= 0x1F7EB => 2,
            >= 0x1F90C and <= 0x1F9FF => 2,
            >= 0x1FA70 and <= 0x1FAFF => 2,
            >= 0x20000 and <= 0x3FFFD => 2,
            _ => 1
        };
    }

    // Remote text is written straight to the terminal, where an escape sequence can move the cursor or recolour the
    // line, and a bidirectional override can reorder what the rest of it appears to say. Neither belongs in a name,
    // a label value, a service endpoint or a server's error text, so replace both. Zero width joiners are kept,
    // because emoji are built out of them. Optional fields are accepted so that a caller does not have to decide
    // whether an absent value is worth sanitizing; a null becomes empty, which prints as nothing.
    private static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new(value.Length);

        foreach (Rune rune in value.EnumerateRunes())
        {
            bool safeToDisplay = Rune.GetUnicodeCategory(rune) switch
            {
                UnicodeCategory.Format => rune.Value is 0x200C or 0x200D,
                UnicodeCategory.Control or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator => false,
                _ => true
            };

            builder.Append(safeToDisplay ? rune.ToString() : ReplacementSymbol);
        }

        return builder.ToString();
    }

    // A console whose output is redirected has no width, and reports either zero or throws, depending on the platform.
    private static int ConsoleWidth
    {
        get
        {
            try
            {
                return Console.WindowWidth > 0 ? Console.WindowWidth : DefaultConsoleWidth;
            }
            catch (IOException)
            {
                return DefaultConsoleWidth;
            }
        }
    }

    // A labeler publishes the label values it can emit in the policies of its app.bsky.labeler.service record.
    // Returns null when the record could not be read, which is not the same as a labeler which declares no values.
    private static async Task<IReadOnlyList<string>?> GetDeclaredLabelValuesAsync(
        Did labeler,
        CancellationToken cancellationToken)
    {
        // The PDS controls error text logged by the SDK; this optional lookup must not write it to the terminal.
        using BlueskyAgent agent = new(new BlueskyAgentOptions { LoggerFactory = NullLoggerFactory.Instance });

        try
        {
            AtProtoHttpResult<Service> declarationResult = await agent.GetLabelerDeclaration(labeler, cancellationToken).ConfigureAwait(false);

            if (declarationResult.Succeeded && declarationResult.Result is not null)
            {
                return [.. declarationResult.Result.Policies.LabelValues.Order(StringComparer.Ordinal)];
            }
        }
        catch (ArgumentException)
        {
            // A malformed or unavailable PDS cannot supply optional declaration metadata.
        }
        catch (HttpRequestException)
        {
            // The label stream can still work when the PDS cannot be reached.
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // An HTTP-client timeout is not a request to stop watching labels.
        }

        return null;
    }

    // label.Uri is the AT URI of a labelled record, or the DID of a labelled account.
    private static void WriteLabel(Label label)
    {
        ConsoleColor originalColor = Console.ForegroundColor;

        if (label.IsNegationLabel)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"{CircledTimesSymbol} NEG");
            Console.ForegroundColor = originalColor;
            Console.Write(' ');
        }
        else
        {
            Console.Write($"{LabelSymbol}  ");
        }

        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(Sanitize(label.Value));
        Console.ForegroundColor = originalColor;
        Console.Write(' ');
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write(Sanitize(label.Uri));
        Console.ForegroundColor = originalColor;
        Console.Write($" @ {FormatTime(label.CreationTimestamp)}");

        if (label.ExpiresAt is DateTimeOffset expiresAt)
        {
            Console.Write($" - {StopwatchSymbol}  {FormatTime(expiresAt)}");
        }

        Console.WriteLine();
    }

    private static string FormatTime(DateTimeOffset time) =>
        time.ToLocalTime().ToString("G", CultureInfo.DefaultThreadCurrentUICulture);
}
