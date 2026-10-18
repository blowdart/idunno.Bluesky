// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Types.Test;

public class ParsableTypesTests
{
    [Fact]
    public void StandaloneStringTypesImplementIParsable()
    {
        AssertParsable<AtUri>("at://did:plc:identifier/app.bsky.feed.post/record", "not-an-at-uri");
        AssertParsable<Cid>("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4", "not-a-cid");
        AssertParsable<Did>("did:plc:identifier", "not-a-did");
        AssertParsable<Handle>("alice.example", "not-a-handle");
        AssertParsable<Nsid>("app.bsky.feed.post", "not-an-nsid");
        AssertParsable<RecordKey>("record", ".");
    }

    private static void AssertParsable<T>(string validValue, string invalidValue)
        where T : IParsable<T>
    {
        T parsed = T.Parse(validValue, provider: null);
        Assert.True(T.TryParse(validValue, provider: null, out T? tryParseResult));
        Assert.Equal(parsed, tryParseResult);

        Assert.False(T.TryParse(invalidValue, provider: null, out T? invalidResult));
        Assert.Null(invalidResult);
        Assert.False(T.TryParse(null, provider: null, out T? nullResult));
        Assert.Null(nullResult);
        Assert.Throws<FormatException>(() => T.Parse(invalidValue, provider: null));
        Assert.Throws<ArgumentNullException>(() => T.Parse(null!, provider: null));
    }
}
