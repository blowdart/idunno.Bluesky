// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;

using idunno.AtProto.Benchmarks;
using idunno.AtProto.Firehose;
using idunno.AtProto.Jetstream;
using ZstdSharp;

namespace idunno.AtProto.Test;

#pragma warning disable CS0618 // AtJetstreamEvent is obsolete, but is still what the V1 receive path produces.

/// <summary>
/// Replays the benchmark corpora through the streaming hot paths, and runs the <see cref="Cid"/> operations the firehose
/// relies on, and fails if the bytes allocated per message or per CID grow past a budget.
/// </summary>
/// <remarks>
/// <para>Unlike time, allocations are deterministic for a given runtime, code and corpus, so they can gate a pull request.
/// Each path completes synchronously against the corpus, so the allocations of the current thread are exactly those of the
/// path being measured, whatever other tests are running.</para>
/// <para>Budgets are roughly ten percent above the measured baseline. When a change reduces allocations, lower the budget
/// to lock in the gain. When the corpus is refreshed with benchmarks\capture.ps1, use the budgets it suggests.</para>
/// </remarks>
public class AllocationBudgetTests
{
    private const int MaxMessageSize = 5 * 1024 * 1024;

    // Bytes allocated per message. Baselines measured on .NET 8, 9 and 10 are in the comments; .NET 8 is slightly higher.
    public static TheoryData<string, long> Budgets => new()
    {
        { "WebSocketReceive.Firehose", 7_300 },   // 6,620
        { "WebSocketReceive.Jetstream", 1_050 },  // 944
        { "Jetstream.V1", 5_600 },                 // 5,009
        { "Jetstream.V1Compressed", 5_800 },       // 4,986 / 5,244
        { "Jetstream.V2", 5_500 },                 // 4,793 / 4,921
        { "Firehose.FrameParse", 3_100 },          // 2,762
        { "Firehose.ReadFields", 5_000 },          // 4,514
        { "Firehose.FullDecode", 35_000 },         // 31,786 / 31,746
    };

    [Theory]
    [MemberData(nameof(Budgets))]
    public async Task AllocationsPerMessageAreWithinBudget(string path, long budget)
    {
        (byte[][] messages, Func<byte[], Task> action, IDisposable? state) = Create(path);

        using (state)
        {
            (long perMessage, bool completedSynchronously) =
                await AllocationMeasurement.PerUnitAsync(messages.Length, i => action(messages[i]));

            AllocationMeasurement.Report(path, perMessage);

            Assert.True(completedSynchronously, $"{path} did not complete synchronously, so its allocations cannot be attributed to it.");
            Assert.True(perMessage <= budget, $"{path} allocated {perMessage:N0} bytes per message, over its budget of {budget:N0}.");
        }
    }

    // Bytes allocated per CID. The CIDs are those of random 256 byte blocks, the size of a typical record.
    public static TheoryData<string, long> CidBudgets => new()
    {
        { "Cid.CarBlockDictionary", 40 },   // 33
        { "Cid.ToString", 8 },              // 0, as the string is cached after the first call
        { "Cid.CreateAndFormat", 680 },     // 616
    };

    [Theory]
    [MemberData(nameof(CidBudgets))]
    public void AllocationsPerCidAreWithinBudget(string path, long budget)
    {
        const int count = 64;

        Random random = new(42);
        Cid[] cids = new Cid[count];
        byte[][] contents = new byte[count][];
        for (int i = 0; i < count; i++)
        {
            contents[i] = new byte[256];
            random.NextBytes(contents[i]);
            cids[i] = Cid.FromDagCbor(contents[i]);
        }

        Action action = path switch
        {
            // RepoEventDecoder.ReadCar fills a dictionary keyed by CID with every block in a commit, then looks each operation up.
            "Cid.CarBlockDictionary" => () =>
            {
                Dictionary<Cid, int> blocks = new(count);
                for (int i = 0; i < count; i++)
                {
                    blocks.TryAdd(cids[i], i);
                }

                foreach (Cid cid in cids)
                {
                    blocks.TryGetValue(cid, out _);
                }
            },
            "Cid.ToString" => () =>
            {
                foreach (Cid cid in cids)
                {
                    _ = cid.ToString();
                }
            },
            // The firehose creates a new CID for every block, so the first call to ToString is the one that matters there.
            "Cid.CreateAndFormat" => () =>
            {
                foreach (byte[] content in contents)
                {
                    _ = Cid.FromDagCbor(content).ToString();
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown path.")
        };

        long perCid = AllocationMeasurement.PerUnit(count, action);

        AllocationMeasurement.Report(path, perCid);

        Assert.True(perCid <= budget, $"{path} allocated {perCid:N0} bytes per CID, over its budget of {budget:N0}.");
    }

    private static (byte[][] Messages, Func<byte[], Task> Action, IDisposable? State) Create(string path)
    {
        switch (path)
        {
            case "WebSocketReceive.Firehose":
            case "WebSocketReceive.Jetstream":
                ReplayWebSocket socket = new();
                return (
                    CorpusFile.Read(path.EndsWith("Firehose", StringComparison.Ordinal) ? CorpusFile.Firehose : CorpusFile.JetstreamV1),
                    async message =>
                    {
                        socket.Load(message);
                        await socket.ReceiveNextMessageAsync(ReplayWebSocket.FragmentSize, MaxMessageSize);
                    },
                    socket);

            case "Jetstream.V1":
                AtProtoJetstream v1 = CreateJetstream(JetstreamProtocolVersion.V1);
                return (CorpusFile.Read(CorpusFile.JetstreamV1), message => ParseV1(v1, message), v1);

            case "Jetstream.V1Compressed":
                AtProtoJetstream v1Compressed = CreateJetstream(JetstreamProtocolVersion.V1);
                Decompressor decompressor = new();
                decompressor.LoadDictionary(new JetstreamOptions().Dictionary);
                return (
                    CorpusFile.Read(CorpusFile.JetstreamV1Zstd),
                    message => ParseV1(v1Compressed, decompressor.Unwrap(message, MaxMessageSize)),
                    new Disposables(v1Compressed, decompressor));

            case "Jetstream.V2":
                AtProtoJetstream v2 = CreateJetstream(JetstreamProtocolVersion.V2);
                return (
                    CorpusFile.Read(CorpusFile.JetstreamV2),
                    message =>
                    {
                        v2.TryDeriveV2Event(Encoding.UTF8.GetString(message), out _);
                        return Task.CompletedTask;
                    },
                    v2);

            default:
                RepoEventDecoder decoder = new(new FirehoseOptions(), verifier: null);
                CborFieldNames fields = new CborFieldNames("error", "message", "name", "seq").Union(decoder.PayloadFields);
                return (CorpusFile.Read(CorpusFile.Firehose), path switch
                {
                    "Firehose.FrameParse" => message =>
                    {
                        FirehoseFrame.Parse(message);
                        return Task.CompletedTask;
                    },
                    "Firehose.ReadFields" => message =>
                    {
                        FirehoseCbor.ReadFields(FirehoseFrame.Parse(message).Payload, fields);
                        return Task.CompletedTask;
                    },
                    "Firehose.FullDecode" => async message =>
                    {
                        FirehoseFrame frame = FirehoseFrame.Parse(message);
                        CborFields payload = FirehoseCbor.ReadFields(frame.Payload, fields);
                        if (frame.Type is not null && decoder.IsSequenced(frame.Type))
                        {
                            await decoder.DecodeAsync(frame.Type, payload.GetInteger("seq"), payload, CancellationToken.None);
                        }
                    },
                    _ => throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown path.")
                }, null);
        }
    }

    private static AtProtoJetstream CreateJetstream(JetstreamProtocolVersion version) =>
        new(options: new JetstreamOptions { ProtocolVersion = version, UseCompression = false });

    private static Task ParseV1(AtProtoJetstream jetstream, ReadOnlySpan<byte> message)
    {
        AtJetstreamEvent? atJetstreamEvent = JsonSerializer.Deserialize(Encoding.UTF8.GetString(message), SourceGenerationContext.Default.AtJetstreamEvent);
        if (atJetstreamEvent is not null)
        {
            jetstream.DeriveEvent(atJetstreamEvent);
        }

        return Task.CompletedTask;
    }

    private sealed class Disposables(params IDisposable[] items) : IDisposable
    {
        public void Dispose()
        {
            foreach (IDisposable item in items)
            {
                item.Dispose();
            }
        }
    }
}