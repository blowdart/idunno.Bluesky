using System.Globalization;
using System.Text;
using System.Text.Json;

using idunno.AtProto.Jetstream;
using idunno.Bluesky;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using WatcherBot;

Console.OutputEncoding = Encoding.UTF8;

HostApplicationBuilder builder = new(args);

builder.Services
    .AddOptions<BotOptions>()
    .Bind(builder.Configuration.GetSection(BotOptions.ConfigurationSectionName));

builder.Services
    .AddSingleton<IValidateOptions<BotOptions>, ValidateBotOptions>();

builder.Services.AddHostedService<Worker>();

using IHost host = builder.Build();
await host.RunAsync();

internal sealed class Worker(IOptionsMonitor<BotOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var jetStream = new AtProtoJetstream(collections: ["app.bsky.feed.post"]);

        try
        {
            await foreach (JetstreamEvent evt in jetStream.StreamAsync(cancellationToken: stoppingToken))
            {
                if (evt is not JetstreamCommitEvent { Commit.Operation: JetstreamCommitOperation.Create } commitEvent ||
                    commitEvent.Commit.Record is not JsonElement record)
                {
                    continue;
                }

                Post? post;
                try
                {
                    post = JsonSerializer.Deserialize<Post>(record, BlueskyServer.BlueskyJsonSerializerOptions);
                }
                catch (JsonException ex)
                {
                    Console.Error.WriteLine($"Skipping invalid post from {commitEvent.Did}: {ex.Message}");
                    continue;
                }
                catch (ArgumentException ex)
                {
                    Console.Error.WriteLine($"Skipping invalid post from {commitEvent.Did}: {ex.Message}");
                    continue;
                }

                if (post is not null && !string.IsNullOrEmpty(post.Text) &&
                    options.CurrentValue.WatchWords.Any(word =>
                        post.Text.Contains(word, StringComparison.OrdinalIgnoreCase)))
                {
                    string timeStamp = evt.DateTimeOffset.ToLocalTime().ToString("G", CultureInfo.CurrentCulture);
                    Console.WriteLine($"{commitEvent.Did} posted in {commitEvent.Commit.Collection} at {timeStamp}");
                    Console.WriteLine(post.Text);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown stops the live stream.
        }
    }
}
