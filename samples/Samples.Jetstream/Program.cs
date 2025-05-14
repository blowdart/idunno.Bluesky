// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto.Jetstream;


namespace Samples.Jetstream
{
    public sealed class Program
    {
        static async Task<int> Main(string[] args)
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

            ConsoleColor savedColor = Console.ForegroundColor;

            using (var jetStream = new AtProtoJetstream(
                options: new JetstreamOptions()
                {
                    UseCompression = true,
                    LoggerFactory = loggerFactory
                }))
            {
                jetStream.ConnectionStateChanged += (sender, e) =>
                {
                    Console.ForegroundColor = savedColor;
                    Console.WriteLine($"Connection status changed to {e.State}");
                };

                jetStream.MessageReceived += (sender, e) =>
                {
                    Console.ForegroundColor = savedColor;
                    Console.WriteLine($"Received message {e.Message}");
                };

                jetStream.RecordReceived += (sender, e) =>
                {
                    string timeStamp = e.ParsedEvent.DateTimeOffset.ToLocalTime().ToString("G", CultureInfo.DefaultThreadCurrentUICulture);

                    ConsoleColor savedColor = Console.ForegroundColor;

                    switch (e.ParsedEvent)
                    {
                        case AtJetstreamAccountEvent accountEvent:
                            if (accountEvent.Account.Active)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"ACCOUNT: {accountEvent.Did} activated at {timeStamp}");
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.Write($"ACCOUNT: {accountEvent.Did} deactivated at {timeStamp}");

                                if (accountEvent.Account.Status is not null)
                                {
                                    Console.Write($"{accountEvent.Account.Status}");
                                }

                                Console.WriteLine();
                            }
                            break;

                        case AtJetstreamCommitEvent commitEvent:
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"COMMIT: {commitEvent.Did} executed a {commitEvent.Commit.Operation} in {commitEvent.Commit.Collection} at {timeStamp}");
                            break;

                        case AtJetstreamIdentityEvent identityEvent:
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"IDENTITY: {identityEvent.Did} changed handle to {identityEvent.Identity.Handle} at {timeStamp}");
                            break;

                        default:
                            break;
                    }
                };

                await jetStream.ConnectAsync(cancellationToken: cancellationToken);

                while (!cancellationToken.IsCancellationRequested)
                {
                }

                Console.ForegroundColor = savedColor;

                await jetStream.CloseAsync();
            }

            return 0;
        }
    }
}
