// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Jetstream;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class JetstreamEventDerivationTests
{
    private const string TestDid = "did:plc:g6ylltenitt4tp27bpwalh7b";

    [Theory]
    [InlineData(JetStreamEventKind.Account)]
    [InlineData(JetStreamEventKind.Commit)]
    [InlineData(JetStreamEventKind.Identity)]
    public void AnEventWhoseKindNamesAPayloadItDoesNotCarryDoesNotDerive(JetStreamEventKind kind)
    {
        using var jetstream = new AtProtoJetstream();

        AtJetstreamEvent jetstreamEvent = new()
        {
            Did = new Did(TestDid),
            TimeStamp = 1746663645473657,
            Kind = kind
        };

        // The kind and the payload it names arrive as two independent pieces of remote input, so a kind without its
        // payload has to be handled rather than indexed into.
        Assert.Null(jetstream.DeriveEvent(jetstreamEvent));
    }

    [Theory]
    [InlineData(JetStreamEventKind.Account)]
    [InlineData(JetStreamEventKind.Commit)]
    [InlineData(JetStreamEventKind.Identity)]
    public void AnEventWithNoExtensionDataAtAllDoesNotDerive(JetStreamEventKind kind)
    {
        using var jetstream = new AtProtoJetstream();

        AtJetstreamEvent jetstreamEvent = new()
        {
            Did = new Did(TestDid),
            TimeStamp = 1746663645473657,
            Kind = kind,
            ExtensionData = null
        };

        Assert.Null(jetstream.DeriveEvent(jetstreamEvent));
    }

    [Fact]
    public void AnEventCarryingThePayloadItsKindNamesDerives()
    {
        using var jetstream = new AtProtoJetstream();

        AtJetstreamEvent jetstreamEvent = new()
        {
            Did = new Did(TestDid),
            TimeStamp = 1746663645473657,
            Kind = JetStreamEventKind.Identity,
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["identity"] = JsonDocument.Parse(
                    $$"""
                    {
                      "did":"{{TestDid}}",
                      "handle":"miyakotubaki.bsky.social",
                      "seq":1,
                      "time":"2025-05-08T00:20:44.859Z"
                    }
                    """).RootElement
            }
        };

        AtJetstreamIdentityEvent derived = Assert.IsType<AtJetstreamIdentityEvent>(jetstream.DeriveEvent(jetstreamEvent));

        Assert.Equal(1U, derived.Identity.Sequence);
    }
}
