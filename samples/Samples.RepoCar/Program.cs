// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace Samples.RepoCar;

public sealed class Program
{
    private static readonly UTF8Encoding s_strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static async Task<int> Main(string[] args)
    {
        Argument<string> identifierArgument = new("identifier")
        {
            Description = "The repository DID or handle to inspect."
        };

        Option<string?> proxyOption = new("--proxy")
        {
            Description = "The URL of a web proxy to use.",
            HelpName = "Uri"
        };
        proxyOption.Validators.Add(result =>
        {
            string? proxyValue = result.GetValue(proxyOption);
            if (proxyValue is not null &&
                (!Uri.TryCreate(proxyValue, UriKind.Absolute, out Uri? proxyUri) ||
                 proxyUri.Scheme is not "http" and not "https"))
            {
                result.AddError("The proxy URL must be an absolute HTTP or HTTPS URI.");
            }
        });

        RootCommand rootCommand = new("Download and inspect an AT Protocol repository CAR.")
        {
            identifierArgument,
            proxyOption
        };
        rootCommand.SetAction((parseResult, cancellationToken) =>
        {
            string? identifierText = parseResult.GetValue(identifierArgument);

            if (string.IsNullOrWhiteSpace(identifierText) ||
                !AtIdentifier.TryParse(identifierText, out AtIdentifier? identifier) ||
                identifier is null)
            {
                throw new InvalidOperationException($"'{identifierText}' is not a valid AT identifier.");
            }

            string? proxyValue = parseResult.GetValue(proxyOption);
            Uri? proxyUri = proxyValue is null
                ? null
                : Uri.TryCreate(proxyValue, UriKind.Absolute, out Uri? parsedProxyUri)
                    ? parsedProxyUri
                    : throw new InvalidOperationException("The proxy URL is invalid.");
            return InspectRepositoryAsync(identifier, proxyUri, cancellationToken);
        });

        return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
    }

    private static async Task InspectRepositoryAsync(AtIdentifier identifier, Uri? proxyUri, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Inspecting repository for '{identifier}'...");

        using AtProtoAgent agent = new(
            new Uri("https://bsky.social"),
            new AtProtoAgentOptions
            {
                HttpClientOptions = new HttpClientOptions(proxyUri: proxyUri)
            });

        Did did = identifier switch
        {
            Did repositoryDid => repositoryDid,
            Handle handle => await agent.ResolveHandle(handle, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Could not resolve handle '{handle}' to a DID."),
            _ => throw new ArgumentException("The identifier must be a DID or handle.", nameof(identifier))
        };

        Console.WriteLine($"Getting repository for {did}...");
        AtProtoHttpResult<Stream> repoResult = await agent.GetRepo(did, cancellationToken).ConfigureAwait(false);
        repoResult.EnsureSucceeded();

        Console.WriteLine($"Reading the repository...");
        await using Stream carStream = repoResult.Result;
        using CarReader reader = await CarReader.CreateAsync(
            carStream,
            (repositoryDid, token) => agent.ResolveDidDocument(repositoryDid, token),
            leaveOpen: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        CarHeader header = reader.ReadHeader();
        Cid root = header.Roots.Single();
        Dictionary<Cid, CarBlock> blocks = [];
        CarBlock? block;
        while ((block = reader.ReadBlock()) is not null)
        {
            VerifyBlockCid(block);
            blocks.TryAdd(block.Cid, block);
        }

        if (!blocks.TryGetValue(root, out CarBlock? commitBlock))
        {
            throw new InvalidDataException("The CAR root commit block is missing.");
        }

        IReadOnlyDictionary<string, object?> commit = ReadMap(commitBlock.Data);
        if (!commit.TryGetValue("data", out object? dataValue) || dataValue is not Cid dataRoot)
        {
            throw new InvalidDataException("The CAR root commit does not contain a valid MST data link.");
        }

        Dictionary<Did, Task<Handle>> resolvedHandles = [];
        SortedDictionary<string, int> collectionCounts = new(StringComparer.Ordinal);
        await WalkTreeAsync(dataRoot, blocks, did, agent, resolvedHandles, collectionCounts, cancellationToken).ConfigureAwait(false);

        Console.WriteLine("Summary");
        Console.WriteLine("-------");
        if (collectionCounts.Count == 0)
        {
            Console.WriteLine("The repository contains no records.");
            return;
        }

        int collectionWidth = collectionCounts.Keys.Max(collection => collection.Length);
        foreach ((string collection, int count) in collectionCounts)
        {
            Console.WriteLine($"{collection.PadRight(collectionWidth)} : {count}");
        }

        Console.WriteLine($"{collectionCounts.Count} collection(s), {collectionCounts.Values.Sum()} record(s).");
    }

    private static async Task WalkTreeAsync(
        Cid treeCid,
        IReadOnlyDictionary<Cid, CarBlock> blocks,
        Did repoDid,
        AtProtoAgent agent,
        Dictionary<Did, Task<Handle>> resolvedHandles,
        SortedDictionary<string, int> collectionCounts,
        CancellationToken cancellationToken)
    {
        if (!blocks.TryGetValue(treeCid, out CarBlock? treeBlock))
        {
            throw new InvalidDataException($"MST node '{treeCid}' is missing from the CAR.");
        }

        IReadOnlyDictionary<string, object?> treeNode = ReadMap(treeBlock.Data);
        if (treeNode.TryGetValue("l", out object? leftValue) && leftValue is Cid leftTree)
        {
            await WalkTreeAsync(
                leftTree,
                blocks,
                repoDid,
                agent,
                resolvedHandles,
                collectionCounts,
                cancellationToken).ConfigureAwait(false);
        }

        if (!treeNode.TryGetValue("e", out object? entriesValue) || entriesValue is not List<object?> entries)
        {
            throw new InvalidDataException($"MST node '{treeCid}' does not contain entries.");
        }

        byte[] previousKey = [];
        foreach (object? entryValue in entries)
        {
            if (entryValue is not IReadOnlyDictionary<string, object?> entry ||
                !entry.TryGetValue("p", out object? prefixValue) ||
                prefixValue is not ulong prefixLength ||
                !entry.TryGetValue("k", out object? keySuffixValue) ||
                keySuffixValue is not byte[] keySuffix ||
                !entry.TryGetValue("v", out object? recordCidValue) ||
                recordCidValue is not Cid recordCid)
            {
                throw new InvalidDataException($"MST node '{treeCid}' contains an invalid entry.");
            }

            if (prefixLength > (ulong)previousKey.Length)
            {
                throw new InvalidDataException($"MST node '{treeCid}' contains an invalid key prefix length.");
            }

            byte[] keyBytes = [.. previousKey.AsSpan(0, checked((int)prefixLength)), .. keySuffix];
            string key;
            try
            {
                key = s_strictUtf8.GetString(keyBytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException($"MST node '{treeCid}' contains a key that is not valid UTF-8.", exception);
            }

            previousKey = keyBytes;

            if (!blocks.TryGetValue(recordCid, out CarBlock? recordBlock))
            {
                throw new InvalidDataException($"MST record '{recordCid}' is missing from the CAR.");
            }

            int collectionSeparator = key.IndexOf('/');
            if (collectionSeparator > 0)
            {
                string collection = key[..collectionSeparator];
                collectionCounts[collection] = collectionCounts.GetValueOrDefault(collection) + 1;
            }

            IReadOnlyDictionary<string, object?> record = ReadMap(recordBlock.Data);
            await PrintRecordAsync(
                repoDid,
                key,
                recordCid,
                record,
                resolvedHandles,
                cancellationToken).ConfigureAwait(false);

            if (entry.TryGetValue("t", out object? rightTreeValue) && rightTreeValue is Cid rightTree)
            {
                await WalkTreeAsync(
                    rightTree,
                    blocks,
                    repoDid,
                    agent,
                    resolvedHandles,
                        collectionCounts,
                        cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task PrintRecordAsync(
        Did repoDid,
        string key,
        Cid cid,
        IReadOnlyDictionary<string, object?> record,
        Dictionary<Did, Task<Handle>> resolvedHandles,
        CancellationToken cancellationToken)
    {
        int separator = key.IndexOf('/');
        if (separator <= 0 || separator == key.Length - 1)
        {
            throw new InvalidDataException($"MST record key '{key}' is not a collection/rkey path.");
        }

        string collection = key[..separator];
        string atUri = $"at://{repoDid}/{key}";
        string type = record.TryGetValue("$type", out object? typeValue) && typeValue is string recordType
            ? recordType
            : collection;

        Console.WriteLine($"Type     : {type}");
        Console.WriteLine($"AtUri    : {atUri}");
        Console.WriteLine($"CID      : {cid}");

        if (type == "app.bsky.feed.post")
        {
            if (record.TryGetValue("text", out object? textValue) && textValue is string text)
            {
                Console.WriteLine($"Text     : {text}");
            }
            else
            {
                Console.WriteLine("Text     : (none)");
            }

            if (record.TryGetValue("createdAt", out object? createdAtValue) && createdAtValue is string createdAt)
            {
                Console.WriteLine($"Date     : {createdAt}");
            }

            if (record.TryGetValue("reply", out object? replyValue) && replyValue is IReadOnlyDictionary<string, object?> reply &&
                reply.TryGetValue("parent", out object? parentValue) && parentValue is IReadOnlyDictionary<string, object?> parent &&
                parent.TryGetValue("uri", out object? parentUri) && parentUri is not null &&
                parent.TryGetValue("cid", out object? parentCid) && parentCid is not null)
            {
                Console.WriteLine($"Reply To : {parentUri} / {parentCid}");
            }
        }
        else if (type is "app.bsky.graph.follow" or "app.bsky.graph.block")
        {
            if (record.TryGetValue("subject", out object? subjectValue) &&
                subjectValue is string subjectText &&
                Did.TryParse(subjectText, out Did? subjectDid) &&
                subjectDid is not null)
            {
                if (!resolvedHandles.TryGetValue(subjectDid, out Task<Handle>? handleTask))
                {
                    handleTask = Resolution.ResolveVerifiedHandle(subjectDid, cancellationToken: cancellationToken);
                    resolvedHandles.Add(subjectDid, handleTask);
                }

                Handle subjectHandle = await handleTask.ConfigureAwait(false);
                string relation = type == "app.bsky.graph.follow" ? "Follows" : "Blocks";
                Console.WriteLine($"{relation}: {(subjectHandle.IsValid ? subjectHandle.Value : subjectDid.Value)}");
            }
            else
            {
                Console.WriteLine("Subject: (missing or invalid DID)");
            }
        }

        Console.WriteLine();
    }

    private static IReadOnlyDictionary<string, object?> ReadMap(ReadOnlyMemory<byte> data)
    {
        CborReader reader = new(data, CborConformanceMode.Canonical);
        object? value = ReadValue(reader);
        if (reader.BytesRemaining != 0 || value is not IReadOnlyDictionary<string, object?> map)
        {
            throw new InvalidDataException("A CAR block did not contain a CBOR map.");
        }

        return map;
    }

    private static void VerifyBlockCid(CarBlock block)
    {
        IReadOnlyList<byte> hash = block.Cid.Hash;
        if (block.Cid.Version != 1 ||
            block.Cid.Codec != 0x71 ||
            hash.Count != 34 ||
            hash[0] != 0x12 ||
            hash[1] != 0x20 ||
            !CryptographicOperations.FixedTimeEquals(
                hash.Skip(2).ToArray(),
                SHA256.HashData(block.Data.Span)))
        {
            throw new InvalidDataException($"CAR block '{block.Cid}' does not match its DAG-CBOR SHA-256 CID.");
        }
    }

    private static object? ReadValue(CborReader reader)
    {
        switch (reader.PeekState())
        {
            case CborReaderState.Tag:
                ulong tag = (ulong)reader.ReadTag();
                if (tag != 42)
                {
                    throw new InvalidDataException($"Unsupported DAG-CBOR tag '{tag}'.");
                }

                byte[] cidBytes = reader.ReadByteString();
                if (cidBytes.Length < 2 || cidBytes[0] != 0)
                {
                    throw new InvalidDataException("A DAG-CBOR CID link had an invalid prefix.");
                }

                return new Cid(cidBytes.AsSpan(1).ToArray());
            case CborReaderState.StartMap:
                int? mapLength = reader.ReadStartMap();
                Dictionary<string, object?> map = new(StringComparer.Ordinal);
                int mapItemsRead = 0;
                while (mapLength is null
                    ? reader.PeekState() != CborReaderState.EndMap
                    : mapItemsRead < mapLength.Value)
                {
                    string key = reader.ReadTextString();
                    if (!map.TryAdd(key, ReadValue(reader)))
                    {
                        throw new InvalidDataException($"A DAG-CBOR map contains duplicate key '{key}'.");
                    }

                    mapItemsRead++;
                }

                reader.ReadEndMap();
                return map;
            case CborReaderState.StartArray:
                int? arrayLength = reader.ReadStartArray();
                List<object?> array = [];
                while (arrayLength is null
                    ? reader.PeekState() != CborReaderState.EndArray
                    : array.Count < arrayLength.Value)
                {
                    array.Add(ReadValue(reader));
                }

                reader.ReadEndArray();
                return array;
            case CborReaderState.TextString:
                return reader.ReadTextString();
            case CborReaderState.ByteString:
                return reader.ReadByteString();
            case CborReaderState.UnsignedInteger:
                return reader.ReadUInt64();
            case CborReaderState.NegativeInteger:
                return reader.ReadInt64();
            case CborReaderState.Boolean:
                return reader.ReadBoolean();
            case CborReaderState.Null:
                reader.ReadNull();
                return null;
            case CborReaderState.HalfPrecisionFloat:
                return (double)reader.ReadHalf();
            case CborReaderState.SinglePrecisionFloat:
                return (double)reader.ReadSingle();
            case CborReaderState.DoublePrecisionFloat:
                return reader.ReadDouble();
            default:
                throw new InvalidDataException($"Unsupported DAG-CBOR value '{reader.PeekState()}'.");
        }
    }
}
