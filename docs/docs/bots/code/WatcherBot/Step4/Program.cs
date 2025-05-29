using System.Globalization;
using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Events;

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

internal sealed class Worker : BackgroundService, IDisposable
{
    internal readonly AtProtoJetstream _jetStream = new ();

    private volatile bool _disposed = false;

    private void OnRecordReceived(object? sender, RecordReceivedEventArgs e)
    {
        string timeStamp = e.ParsedEvent.DateTimeOffset.ToLocalTime().ToString("G", CultureInfo.DefaultThreadCurrentUICulture);

        switch (e.ParsedEvent)
        {
            case AtJetstreamCommitEvent commitEvent:
                Console.WriteLine($"{commitEvent.Did} executed a {commitEvent.Commit.Operation} in {commitEvent.Commit.Collection} at {timeStamp}");
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
