using System.Globalization;
using System.Text;

using idunno.AtProto.Jetstream;

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

internal sealed class Worker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var jetStream = new AtProtoJetstream();

        try
        {
            await foreach (JetstreamEvent evt in jetStream.StreamAsync(cancellationToken: stoppingToken))
            {
                if (evt is JetstreamCommitEvent commitEvent)
                {
                    string timeStamp = evt.DateTimeOffset.ToLocalTime().ToString("G", CultureInfo.CurrentCulture);
                    Console.WriteLine($"{commitEvent.Did} executed a {commitEvent.Commit.Operation} in {commitEvent.Commit.Collection} at {timeStamp}");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown stops the live stream.
        }
    }
}
