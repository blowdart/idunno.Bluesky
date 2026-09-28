using System.Globalization;
using System.Text;

using idunno.AtProto.Jetstream;

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

Console.OutputEncoding = Encoding.UTF8;

await using var jetStream = new AtProtoJetstream();

try
{
    await foreach (JetstreamEvent evt in jetStream.StreamAsync(cancellationToken: cancellation.Token))
    {
        if (evt is JetstreamCommitEvent commitEvent)
        {
            string timeStamp = evt.DateTimeOffset.ToLocalTime().ToString("G", CultureInfo.CurrentCulture);
            Console.WriteLine($"{commitEvent.Did} executed a {commitEvent.Commit.Operation} in {commitEvent.Commit.Collection} at {timeStamp}");
        }
    }
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    // Ctrl+C stops the live stream.
}
