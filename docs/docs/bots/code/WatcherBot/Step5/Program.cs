using System.Globalization;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Events;
using idunno.Bluesky;

using WatcherBot;

Console.OutputEncoding = Encoding.UTF8;

HostApplicationBuilder builder = new(args);

builder.Services
    .AddOptions<BotOptions>()
    .Bind(builder.Configuration.GetSection(BotOptions.ConfigurationSectionName));

builder.Services
    .AddSingleton<IValidateOptions<BotOptions>, ValidateBotOptions>();

builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();

return 0;

internal sealed class Worker(IOptionsMonitor<BotOptions> optionsDelegate) : BackgroundService, IDisposable
{
    private readonly AtProtoJetstream _jetStream = new(collections: ["app.bsky.feed.post"]);
    private volatile bool _disposed = false;

    private void OnRecordReceived(object? sender, RecordReceivedEventArgs e)
    {
        switch (e.ParsedEvent)
        {
            case AtJetstreamCommitEvent commitEvent:
                if (string.Equals(commitEvent.Commit.Operation, "create", StringComparison.OrdinalIgnoreCase) &&
                    commitEvent.Commit.Record is not null)
                {
                    // A new record has been created in the monitored collections.
                    // Let's try to convert it to a Bluesky post

                    try
                    {
                        Post? post = JsonSerializer.Deserialize<Post>(
                            commitEvent.Commit.Record,
                            BlueskyServer.BlueskyJsonSerializerOptions);

                        if (post != null && !string.IsNullOrEmpty(post.Text))
                        {
                            // We have a post, and the post has text, so look for our watched words.
                            if (optionsDelegate.CurrentValue.WatchWords.Any(
                                watchWord => post.Text.Contains(watchWord, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                string timeStamp = e.ParsedEvent.DateTimeOffset.ToLocalTime().ToString("G", CultureInfo.DefaultThreadCurrentUICulture);

                                // We have a post that contains what we're looking for.
                                Console.WriteLine($"{commitEvent.Did} executed a {commitEvent.Commit.Operation} in {commitEvent.Commit.Collection} at {timeStamp}");
                                Console.WriteLine($"{post.Text}");
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // If we encounter a record in the post collection which
                        // can't be deserialized as a post, just skip it.
                    }
                }

                break;

            default:
                break;
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _jetStream.RecordReceived += OnRecordReceived;

        await base.StartAsync(cancellationToken);
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _jetStream.ConnectAsync(cancellationToken: stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _jetStream.CloseAsync();

        await base.StopAsync(cancellationToken);
    }

    void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _jetStream?.Dispose();
        }

        _disposed = true;
    }

    public override void Dispose()
    {
        Dispose(true);
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}