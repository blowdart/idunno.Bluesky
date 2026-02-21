// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using idunno.AtProto;
using idunno.Bluesky;

namespace idunno.TrimmingTest
{
    public sealed class Program
    {
        static async Task<int> Main()
        {
            // Necessary to render emojis.
            Console.OutputEncoding = Encoding.UTF8;

            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (sender, args) =>
                {
                    args.Cancel = true;
                    cancellationTokenSource.Cancel(true);
                };

                CancellationToken cancellationToken = cancellationTokenSource.Token;

                // Change the log level in the ConfigureConsoleLogging() to enable logging
                using (var agent = new AtProtoAgent(new Uri("https://api.bsky.app")))
                {
                    var describeServerResult = await agent.DescribeServer(
                        server: new Uri("https://porcini.us-east.host.bsky.network"),
                        cancellationToken: cancellationToken);

                    describeServerResult.EnsureSucceeded();

                    Console.WriteLine(describeServerResult.Result.Did);

                    var getRawRecordResult = await agent.GetRawRecord(
                        uri: new AtUri("at://blowdart.me/app.bsky.actor.profile/self"), cancellationToken: cancellationToken);
                    getRawRecordResult.EnsureSucceeded();

                    Console.WriteLine(getRawRecordResult.Result.Value!.ToJsonString());
                }

                using (var blueskyAgent = new BlueskyAgent())
                {
                    var getProfileResult = await blueskyAgent.GetProfile(AtIdentifier.Create("blowdart.me"), cancellationToken: cancellationToken);
                    getProfileResult.EnsureSucceeded();

                    Console.WriteLine(getProfileResult.Result.DisplayName);
                }
            }

            return 0;
        }
    }
}

