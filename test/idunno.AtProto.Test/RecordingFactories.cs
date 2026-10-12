// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.AtProto.Test;

/// <summary>
/// An <see cref="IHttpClientFactory"/> which records the names it was asked for.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class RecordingHttpClientFactory : IHttpClientFactory
{
    internal List<string> RequestedNames { get; } = [];

    public HttpClient CreateClient(string name)
    {
        RequestedNames.Add(name);

        return new HttpClient();
    }
}
