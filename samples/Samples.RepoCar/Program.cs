// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

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

        using JsonDocument commitDocument = DagCbor.ToJsonDocument(commitBlock.Data);
        if (!TryGetLink(commitDocument.RootElement, "data", out Cid? dataRoot))
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

        using JsonDocument treeDocument = DagCbor.ToJsonDocument(treeBlock.Data);
        JsonElement treeNode = treeDocument.RootElement;

        if (TryGetLink(treeNode, "l", out Cid? leftTree))
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

        if (!treeNode.TryGetProperty("e", out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"MST node '{treeCid}' does not contain entries.");
        }

        byte[] previousKey = [];
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("p", out JsonElement prefixValue) ||
                !prefixValue.TryGetUInt64(out ulong prefixLength) ||
                !TryGetBytes(entry, "k", out byte[]? keySuffix) ||
                !TryGetLink(entry, "v", out Cid? recordCid))
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

            using JsonDocument recordDocument = DagCbor.ToJsonDocument(recordBlock.Data);
            await PrintRecordAsync(
                repoDid,
                key,
                recordCid,
                recordDocument.RootElement,
                resolvedHandles,
                cancellationToken).ConfigureAwait(false);

            if (TryGetLink(entry, "t", out Cid? rightTree))
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
        JsonElement record,
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
        string type = TryGetString(record, "$type", out string? recordType) ? recordType : collection;

        Console.WriteLine($"Type     : {type}");
        Console.WriteLine($"AtUri    : {atUri}");
        Console.WriteLine($"CID      : {cid}");

        if (type == "app.bsky.feed.post")
        {
            if (TryGetString(record, "text", out string? text))
            {
                Console.WriteLine($"Text     : {text}");
            }
            else
            {
                Console.WriteLine("Text     : (none)");
            }

            if (TryGetString(record, "createdAt", out string? createdAt))
            {
                Console.WriteLine($"Date     : {createdAt}");
            }

            if (record.TryGetProperty("reply", out JsonElement reply) &&
                reply.ValueKind == JsonValueKind.Object &&
                reply.TryGetProperty("parent", out JsonElement parent) &&
                parent.ValueKind == JsonValueKind.Object &&
                TryGetString(parent, "uri", out string? parentUri) &&
                TryGetLink(parent, "cid", out Cid? parentCid))
            {
                Console.WriteLine($"Reply To : {parentUri} / {parentCid}");
            }
        }
        else if (type is "app.bsky.graph.follow" or "app.bsky.graph.block")
        {
            if (TryGetString(record, "subject", out string? subjectText) &&
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

    private static void VerifyBlockCid(CarBlock block)
    {
        if (block.Cid != Cid.FromDagCbor(block.Data.Span))
        {
            throw new InvalidDataException($"CAR block '{block.Cid}' does not match its DAG-CBOR SHA-256 CID.");
        }
    }

    private static bool TryGetString(JsonElement element, string propertyName, [NotNullWhen(true)] out string? value)
    {
        if (element.TryGetProperty(propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString()!;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetLink(JsonElement element, string propertyName, [NotNullWhen(true)] out Cid? cid)
    {
        if (element.TryGetProperty(propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.Object &&
            TryGetString(property, "$link", out string? link))
        {
            cid = new Cid(link);
            return true;
        }

        cid = null;
        return false;
    }

    private static bool TryGetBytes(JsonElement element, string propertyName, [NotNullWhen(true)] out byte[]? bytes)
    {
        if (element.TryGetProperty(propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.Object &&
            TryGetString(property, "$bytes", out string? encoded))
        {
            bytes = new Bytes(encoded).ToBytes();
            return true;
        }

        bytes = null;
        return false;
    }
}