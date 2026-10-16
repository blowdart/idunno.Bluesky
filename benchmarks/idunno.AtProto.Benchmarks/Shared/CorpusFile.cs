// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.IO.Compression;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Reads and writes the captured message corpora used by the benchmarks and the allocation budget tests.
/// </summary>
/// <remarks>
/// <para>A corpus is a gzip compressed sequence of messages, each written as a little endian 32 bit length followed by
/// the message exactly as it arrived from the web socket or, for the xrpc corpora, the HTTP response body. Only payloads
/// are captured, never connection, request or response headers.</para>
/// </remarks>
internal static class CorpusFile
{
    public const string Firehose = "firehose";
    public const string JetstreamV1 = "jetstream-v1";
    public const string JetstreamV1Zstd = "jetstream-v1-zstd";
    public const string JetstreamV2 = "jetstream-v2";
    public const string XrpcGetAuthorFeed = "xrpc-getAuthorFeed";
    public const string XrpcGetProfiles = "xrpc-getProfiles";
    public const string XrpcGetTimeline = "xrpc-getTimeline";

    public const int FirehoseMessageCount = 200;
    public const int JetstreamMessageCount = 500;

    public static IReadOnlyList<string> Names { get; } = [Firehose, JetstreamV1, JetstreamV1Zstd, JetstreamV2, XrpcGetAuthorFeed, XrpcGetProfiles, XrpcGetTimeline];

    public static int MessageCount(string name) => name switch
    {
        Firehose => FirehoseMessageCount,
        XrpcGetAuthorFeed or XrpcGetProfiles or XrpcGetTimeline => 1,
        _ => JetstreamMessageCount
    };

    public static string FileName(string name) => name + ".bin.gz";

    public static string Locate(string name)
    {
        string fileName = FileName(name);

        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string local = Path.Combine(directory.FullName, "Corpus", fileName);
            if (File.Exists(local))
            {
                return local;
            }

            string source = Path.Combine(directory.FullName, "benchmarks", "idunno.AtProto.Benchmarks", "Corpus", fileName);
            if (File.Exists(source))
            {
                return source;
            }
        }

        throw new FileNotFoundException($"Could not find the {name} corpus. Run the benchmarks with the capture command to create it.", fileName);
    }

    public static byte[][] Read(string name)
    {
        using FileStream file = File.OpenRead(Locate(name));
        using GZipStream gzip = new(file, CompressionMode.Decompress);
        using MemoryStream uncompressed = new();
        gzip.CopyTo(uncompressed);

        ReadOnlySpan<byte> data = uncompressed.GetBuffer().AsSpan(0, (int)uncompressed.Length);
        List<byte[]> messages = [];
        while (!data.IsEmpty)
        {
            int length = BinaryPrimitives.ReadInt32LittleEndian(data);
            messages.Add(data.Slice(sizeof(int), length).ToArray());
            data = data[(sizeof(int) + length)..];
        }

        return [.. messages];
    }

    public static void Write(string path, IEnumerable<byte[]> messages)
    {
        using FileStream file = File.Create(path);
        using GZipStream gzip = new(file, CompressionLevel.SmallestSize);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (byte[] message in messages)
        {
            BinaryPrimitives.WriteInt32LittleEndian(length, message.Length);
            gzip.Write(length);
            gzip.Write(message);
        }
    }
}