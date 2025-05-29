using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

using System.Globalization;
using System.Text;

using idunno.AtProto.Jetstream;

Console.OutputEncoding = Encoding.UTF8;

HostApplicationBuilder builder = new(args);
builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();

return 0;

internal sealed class Worker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var jetStream = new AtProtoJetstream();

        jetStream.RecordReceived += (sender, e) =>
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
        };

        await jetStream.ConnectAsync(cancellationToken: stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
        }

        await jetStream.CloseAsync();
    }
}
