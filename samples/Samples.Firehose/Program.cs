// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using idunno.AtProto;
using idunno.AtProto.FireHose;
using Microsoft.Extensions.Logging;

namespace Samples.Firehose;

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

        using (var fireHose = new AtProtoFireHose())
        {
            fireHose.ConnectionStateChanged += (sender, e) =>
            {
                Console.WriteLine($"CONNECTION: status changed to {e.State}");
            };

            fireHose.MessageReceived += (sender, e) =>
            {
                //Console.WriteLine($"MESSAGE: Received message {Convert.ToHexString(e.Message.ToArray())}");
            };

            fireHose.RepoMessageReceived += (sender, e) =>
            {
                switch (e.Payload)
                {
                    //case CommitPayload commitPayload:
                    //    {
                    //        string timeStamp = commitPayload.Time.ToLocalTime().ToString("G", CultureInfo.DefaultThreadCurrentUICulture);

                    //        foreach (var op in commitPayload.Ops)
                    //        {
                    //            Console.WriteLine($"REPO MESSAGE: {e.Header.Operation} / {e.Header.Type} \n  COMMIT: {commitPayload.Repo} => {op.Action} in {op.Path} @ {timeStamp}");
                    //        }

                    //        break;
                    //    }

                    case AccountPayload accountPayload:
                        {
                            string timeStamp = accountPayload.Time.ToLocalTime().ToString("G", CultureInfo.DefaultThreadCurrentUICulture);

                            if (accountPayload.Active)
                            {
                                Console.WriteLine($"REPO MESSAGE: {e.Header.Operation} / {e.Header.Type} \n  ACCOUNT: {accountPayload.Did} => Activated @ {timeStamp} seq: {accountPayload.Seq}");
                            }
                            else
                            {
                                Console.WriteLine($"REPO MESSAGE: {e.Header.Operation} / {e.Header.Type}\n  ACCOUNT: {accountPayload.Did} => Deactivated {accountPayload.Status} @ {timeStamp} seq: {accountPayload.Seq}");
                            }
                            break;
                        }

                    case IdentityPayload identityPayload:
                        {
                            string? handle = identityPayload.Handle?.ToString();

                            if (handle is not null)
                            {
                                handle = $" => ({handle})";
                            }    

                            string timeStamp = identityPayload.Time.ToLocalTime().ToString("G", CultureInfo.DefaultThreadCurrentUICulture);
                            Console.WriteLine($"REPO MESSAGE: {e.Header.Operation} / {e.Header.Type}\n  IDENTITY: {identityPayload.Did}{handle} @ {timeStamp} seq: {identityPayload.Seq}");
                            break;
                        }

                    case InfoPayload infoPayload:
                        {
                            Console.WriteLine($"REPO MESSAGE: {e.Header.Operation} / {e.Header.Type}\n  INFORMATION: {infoPayload.Name} {infoPayload.Message}");
                            break;
                        }

                    case SyncPayload syncPayload:
                        {
                            string timeStamp = syncPayload.Time.ToLocalTime().ToString("G", CultureInfo.DefaultThreadCurrentUICulture);
                            Console.WriteLine($"REPO MESSAGE: {e.Header.Operation} / {e.Header.Type}\n  SYNC OPERATION: {syncPayload.Did} => Rev: {syncPayload.Rev} @ {timeStamp} seq: {syncPayload.Seq}");
                            break;
                        }

                    case FrameError frameError:
                        {
                            Console.WriteLine($"REPO MESSAGE: {e.Header.Operation} / {e.Header.Type}\n  ERROR: {frameError.Error} {frameError.Message}");
                            break;
                        }

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
                await fireHose.ConnectToRepoMessagesAsync(cancellationToken: cancellationToken);
                while (fireHose.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    // Let it run and process.
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    await fireHose.CloseAsync(statusDescription: "Cancellation requested at console.", cancellationToken: cancellationToken);
                    break;
                }
                else
                {
                    await fireHose.CloseAsync(statusDescription: "Force closed on error", cancellationToken: cancellationToken);

                    // The fire hose is no longer connected, but a cancellation isn't the reason.
                    Console.WriteLine($"FORCE DISCONNECTED: state == {fireHose.State}");

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

            await fireHose.CloseAsync();
        }

        return 0;
    }
}

