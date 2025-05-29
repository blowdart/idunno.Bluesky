using System.Globalization;
using System.Text;

using idunno.AtProto.Jetstream;

CancellationTokenSource cancellationTokenSource = new();
CancellationToken cancellationToken = cancellationTokenSource.Token;
Console.CancelKeyPress += (sender, e) =>
{
    cancellationTokenSource?.Cancel();
    e.Cancel = true;
};

Console.OutputEncoding = Encoding.UTF8;

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

await jetStream.ConnectAsync(cancellationToken: cancellationToken);

while (!cancellationToken.IsCancellationRequested)
{
}

await jetStream.CloseAsync();

return 0;
