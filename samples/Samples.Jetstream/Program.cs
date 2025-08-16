// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Jetstream;

namespace Samples.Jetstream
{
    public sealed class Program
    {
        static async Task<int> Main()
        {
            var loggerFactory = LoggerFactory.Create(configure =>
            {
                configure.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "G";
                    options.UseUtcTimestamp = false;
                });
                configure.SetMinimumLevel(LogLevel.Debug);
            });

            CancellationTokenSource cancellationTokenSource = new();
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            Console.CancelKeyPress += (sender, e) =>
            {
                cancellationTokenSource?.Cancel();
                e.Cancel = true;
            };

            Console.OutputEncoding = Encoding.UTF8;

            AtProtoAgent agent = new(new Uri("https://public.api.bsky.app"));

            using (var jetStream = new AtProtoJetstream(
                options: new JetstreamOptions()
                {
                    UseCompression = true,
                    LoggerFactory = loggerFactory
                }))
            {
                jetStream.ConnectionStateChanged += (sender, e) =>
                {
                    Console.WriteLine($"CONNECTION: status changed to {e.State}");
                };

                jetStream.MessageReceived += (sender, e) =>
                {
                    Console.WriteLine($"MESSAGE: Received message {e.Message}");
                };

                jetStream.RecordReceived += async (sender, e) =>
                {
                    string timeStamp = e.ParsedEvent.DateTimeOffset.ToLocalTime().ToString("G", CultureInfo.DefaultThreadCurrentUICulture);

                    switch (e.ParsedEvent)
                    {
                        case AtJetstreamCommitEvent commitEvent:
                            Console.WriteLine($"COMMIT: {commitEvent.Did} executed a {commitEvent.Commit.Operation} in {commitEvent.Commit.Collection} at {timeStamp}");
                            break;

                        case AtJetstreamAccountEvent accountEvent:
                            string eventBelongsTo = accountEvent.Did;

                            DidDocument? didDoc = await agent.ResolveDidDocument(accountEvent.Did).ConfigureAwait(false);

                            if (didDoc is not null)
                            {
                                foreach (string alsoKnownAs in didDoc.AlsoKnownAs)
                                {
                                    if (alsoKnownAs.StartsWith("at://", StringComparison.InvariantCulture))
                                    {
                                        eventBelongsTo += "/";
                                        eventBelongsTo += alsoKnownAs[5..];
                                        break;
                                    }
                                }
                            }

                            if (accountEvent.Account.Active)
                            {
                                Console.WriteLine($"ACCOUNT: {eventBelongsTo} activated at {timeStamp}");
                            }
                            else if (accountEvent.Account.Status == AccountStatus.Deactivated)
                            {
                                Console.WriteLine($"ACCOUNT: {eventBelongsTo} deactivated at {timeStamp}");
                            }
                            else if (accountEvent.Account.Status == AccountStatus.Deleted)
                            {
                                Console.WriteLine($"ACCOUNT: {eventBelongsTo} deleted at {timeStamp}");
                            }
                            else
                            {
                                Console.WriteLine($"ACCOUNT: {eventBelongsTo} was {accountEvent.Account.Status.ToString()!.ToLowerInvariant()} at {timeStamp}");
                            }
                            break;

                        case AtJetstreamIdentityEvent identityEvent:
                            Console.WriteLine($"IDENTITY: {identityEvent.Did} changed handle to {identityEvent.Identity.Handle} at {timeStamp}");
                            break;

                        default:
                            break;
                    }
                };

                const int maximumRetries = 5;
                const int retryWaitPeriod = 10000;
                TimeSpan resetRetryCountAfter = new(0, 5, 0);

                int currentRetryCount = 0;
                DateTimeOffset? lastConnectionAttemptedAt = null;

                do
                {
                    if (currentRetryCount > maximumRetries)
                    {
                        Console.WriteLine("RECONNECT: Failed");
                        break;
                    }

                    if (DateTimeOffset.UtcNow > lastConnectionAttemptedAt + resetRetryCountAfter)
                    {
                        currentRetryCount = 0;
                    }

                    lastConnectionAttemptedAt = DateTimeOffset.UtcNow;
                    await jetStream.ConnectAsync(startFrom: jetStream.MessageLastReceived, cancellationToken: cancellationToken);
                    while (jetStream.IsConnected && !cancellationToken.IsCancellationRequested)
                    {
                        // Let it run and process
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        await jetStream.CloseAsync(statusDescription: "Cancellation requested at console.", cancellationToken: cancellationToken);
                        break;
                    }
                    else
                    {
                        await jetStream.CloseAsync(statusDescription: "Force closed on error", cancellationToken: cancellationToken);

                        // The jet stream is no longer connected, but a cancellation isn't the reason.
                        Console.WriteLine($"FORCE DISCONNECTED: state == {jetStream.State}");

                        // Try to reconnect
                        currentRetryCount++;

                        if (currentRetryCount > maximumRetries)
                        {
                            Console.WriteLine("RECONNECT: Too many times, failed");
                            break;
                        }

                        Console.WriteLine("RECONNECT: Waiting ...");
                        await Task.Delay(retryWaitPeriod);
                        Console.WriteLine($"RECONNECT: Reconnecting {currentRetryCount}/{maximumRetries}");
                    }

                } while (!cancellationToken.IsCancellationRequested);

                await jetStream.CloseAsync();
            }

            return 0;
        }
    }
}
