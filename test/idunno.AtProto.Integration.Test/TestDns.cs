// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.AtProto.Integration.Test;

/// <summary>
/// Replaces the DNS lookup handle resolution performs with a canned answer, so a test never depends on a working
/// DNS server, on what a real name server happens to return, or on how long it takes to say so.
/// </summary>
/// <remarks>
/// <para>
///   The resolver is held in an <see cref="AsyncLocal{T}"/>, so the answer installed here reaches only the work
///   started inside the <c>using</c> block and cannot disturb a test running beside it.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
internal sealed class TestDns : IDisposable
{
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<string>>>? _previous;
    private bool _disposed;

    private TestDns(Func<string, CancellationToken, Task<IReadOnlyList<string>>> resolver)
    {
        _previous = AtProtoServer.DnsTextRecordResolver;
        AtProtoServer.DnsTextRecordResolver = resolver;
    }

    /// <summary>
    /// Answers every lookup through <paramref name="resolver"/>.
    /// </summary>
    /// <param name="resolver">The function to answer lookups with.</param>
    public static TestDns WithResolver(Func<string, CancellationToken, Task<IReadOnlyList<string>>> resolver)
    {
        return new TestDns(resolver);
    }

    /// <summary>
    /// Answers every lookup with no text records, which is what a handle with no <c>_atproto</c> record looks like,
    /// and sends resolution on to its <c>/.well-known/atproto-did</c> fallback.
    /// </summary>
    public static TestDns WithNoTextRecords()
    {
        return new TestDns((_, _) => Task.FromResult<IReadOnlyList<string>>([]));
    }

    /// <summary>
    /// Answers a lookup for the <c>_atproto</c> record of <paramref name="handle"/> with <paramref name="textRecords"/>,
    /// and every other lookup with no text records.
    /// </summary>
    /// <param name="handle">The handle whose <c>_atproto</c> record is being answered.</param>
    /// <param name="textRecords">The text records to answer with.</param>
    public static TestDns WithTextRecordsFor(string handle, params string[] textRecords)
    {
        string host = $"_atproto.{handle}";

        return new TestDns((queriedHost, _) => Task.FromResult<IReadOnlyList<string>>(
            string.Equals(queriedHost, host, StringComparison.OrdinalIgnoreCase) ? textRecords : []));
    }

    /// <summary>
    /// Answers every lookup by throwing, which is how a resolver which cannot be reached behaves.
    /// </summary>
    public static TestDns WhichFails()
    {
        return new TestDns((_, _) => throw new InvalidOperationException("The DNS resolver could not be reached."));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            AtProtoServer.DnsTextRecordResolver = _previous;
            _disposed = true;
        }
    }
}
