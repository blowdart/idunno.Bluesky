// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Firehose;
using idunno.AtProto.Labels;

using Microsoft.Extensions.Logging;

namespace Samples.ModerationLabels;

public sealed class Program
{
    // moderation.bsky.app, the Bluesky moderation service.
    private static readonly Did s_moderationLabeler = new("did:plc:ar7c4by46qjdydhdevvrndac");

    private static readonly Uri s_moderationLabelerService = new("wss://mod.bsky.app");

    private const string LabelSymbol = "\U0001F3F7";

    private const string CircledTimesSymbol = "\u2A02";

    private const string StopwatchSymbol = "\u23F1";

    static async Task<int> Main()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(configure =>
        {
            configure.AddSimpleConsole(options =>
            {
                options.TimestampFormat = "G";
                options.UseUtcTimestamp = false;
            });
            configure.SetMinimumLevel(LogLevel.Warning);
        });

        using CancellationTokenSource cancellationTokenSource = new();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };
        Console.OutputEncoding = Encoding.UTF8;

        await using var firehose = new AtProtoFirehose(
            options: new FirehoseOptions
            {
                LabelerUri = s_moderationLabelerService,
                LoggerFactory = loggerFactory
            });

        Console.WriteLine($"Watching labels from {s_moderationLabeler} at {s_moderationLabelerService}. Press Ctrl+C to stop.");

        try
        {
            await foreach (FirehoseEvent evt in firehose.SubscribeLabelsAsync(cancellationToken: cancellationToken))
            {
                switch (evt)
                {
                    case FirehoseLabelsEvent labelsEvent:
                        foreach (Label label in labelsEvent.Labels)
                        {
                            // A labeler normally only emits its own labels, but only show those issued by the Bluesky moderation service.
                            if (label.Source != s_moderationLabeler)
                            {
                                continue;
                            }

                            WriteLabel(label);
                        }

                        break;

                    case FirehoseInfoEvent infoEvent:
                        Console.WriteLine($"INFO: {infoEvent.Name} {infoEvent.Message}");
                        break;

                    case FirehoseInvalidEvent invalidEvent:
                        Console.WriteLine($"INVALID: Ignored an invalid {invalidEvent.Type} event: {invalidEvent.Reason}");
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
            Console.Error.WriteLine($"The labeler refused the connection: {ex.Message}");
            return 1;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Gave up reconnecting: {ex.Message}");
            return 1;
        }

        return 0;
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
        Console.Write(label.Value);
        Console.ForegroundColor = originalColor;
        Console.Write(' ');
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write(label.Uri);
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
