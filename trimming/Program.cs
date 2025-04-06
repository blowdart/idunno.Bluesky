// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

using System.Text;

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
                }
            }

            return 0;
        }
    }
}

