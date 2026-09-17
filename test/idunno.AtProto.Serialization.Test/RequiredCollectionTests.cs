// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Repo.Models;

namespace idunno.AtProto.Serialization.Test;

/// <summary>
/// <see cref="ApplyWritesResponse.Results"/> is declared non-nullable and is enumerated without a null check, so a
/// server that omitted <c>results</c> or sent an explicit null used to throw a <see cref="NullReferenceException"/>
/// out of the client. <c>[JsonRequired]</c> rejects the omitted case and
/// <see cref="JsonSerializerOptions.RespectNullableAnnotations"/> rejects the explicit null case.
/// </summary>
public class RequiredCollectionTests
{
    private static JsonSerializerOptions Options => AtProtoServer.AtProtoJsonSerializerOptions;

    private const string Commit = """{"cid":"bafyreib2rxk3rh6kzwq6y7ug4eqhfhpqaqzqvuflstfpvgkzjt7b5yfkzy","rev":"3jx4mnqhtpm2b"}""";

    [Fact]
    public void RespectNullableAnnotationsIsEnabled()
    {
        Assert.True(Options.RespectNullableAnnotations);
    }

    [Theory]
    [InlineData($$"""{"commit":{{Commit}}}""")]
    [InlineData($$"""{"commit":{{Commit}},"results":null}""")]
    public void MissingOrNullResultsThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ApplyWritesResponse>(json, Options));
    }

    [Fact]
    public void PopulatedResultsDeserializes()
    {
        ApplyWritesResponse? response = JsonSerializer.Deserialize<ApplyWritesResponse>(
            $$"""{"commit":{{Commit}},"results":[]}""",
            Options);

        Assert.NotNull(response);
        Assert.Empty(response.Results);
    }
}
