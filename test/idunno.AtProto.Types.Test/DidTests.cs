// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Types.Test;

public class DidTests
{
    // Test cases from https://github.com/bluesky-social/atproto/blob/main/interop-test-files/syntax/did_syntax_valid.txt
    [Theory]
    [InlineData("did:method:val")]
    [InlineData("did:method:VAL")]
    [InlineData("did:method:val123")]
    [InlineData("did:method:123")]
    [InlineData("did:method:val-two")]
    [InlineData("did:method:val_two")]
    [InlineData("did:method:val.two")]
    [InlineData("did:method:val:two")]
    [InlineData("did:method:val%BB")]
    [InlineData("did:method:%BB:%BB:%BB:%BB:%BB")]
    [InlineData("did:method:%BB:foo:%BB:foo:%BB:foo:%BB:foo:%BB")]
    [InlineData("did:method:val%BBval%BBval%BBval%BBval%BBval")]
    [InlineData("did:method:vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv")]
    [InlineData("did:m:v")]
    [InlineData("did:method::::val")]
    [InlineData("did:method:-")]
    [InlineData("did:method:-:_:.:%AB")]
    [InlineData("did:method:.")]
    [InlineData("did:method:_")]
    [InlineData("did:method::.")]
    [InlineData("did:m123:val")]

    // Real world DIDs
    [InlineData("did:onion:2gzyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid")]
    [InlineData("did:example:123456789abcdefghi")]
    [InlineData("did:plc:7iza6de2dwap2sbkpav7c6c6")]
    [InlineData("did:web:example.com")]
    [InlineData("did:web:localhost%3A1234")]
    [InlineData("did:key:zQ3shZc2QzApp2oymGvQbzP8eKheVshBHbU4ZYjeXqwSKEn6N")]
    [InlineData("did:ethr:0xb9c5714089478a327f09197987f16f9e5d936e8a")]

    // https://github.com/Web7Foundation/Specifications/blob/main/methods/did-ns-1-0-1.md
    [InlineData("did:ns:com:example:users:0e12c4ff-227b-4642-b37f-f1eff9d44914")]
    [InlineData("did:ns:com:example:users:0e12c4ff-227b-4642-b37f-f1eff9d44914:_diddoc")]
    [InlineData("did:ns:com:example:users:0e12c4ff-227b-4642-b37f-f1eff9d44914:_didid")]
    [InlineData("did:ns:com:example:users:0e12c4ff-227b-4642-b37f-f1eff9d44914:_didkey")]
    [InlineData("did:ns:fe042e80-e963-4a35-9734-7d1eaeb1b06e")]
    [InlineData("did:ns:fe042e80-e963-4a35-9734-7d1eaeb1b06e:_diddoc")]
    [InlineData("did:ns:fe042e80-e963-4a35-9734-7d1eaeb1b06e:_didid")]
    [InlineData("did:ns:fe042e80-e963-4a35-9734-7d1eaeb1b06e:_didkey")]

    // https://github.com/Web7Foundation/Specifications/blob/main/methods/did-web7-1-0-1.md
    [InlineData("did:web7:0")]

    // https://github.com/Web7Foundation/Specifications/blob/main/methods/did-tdw-1-0-1.md
    [InlineData("did:tdw:com:example:users:0e12c4ff-227b-4642-b37f-f1eff9d44914")]
    [InlineData("did:tdw:com:example:users:0e12c4ff-227b-4642-b37f-f1eff9d44914:_diddoc")]
    [InlineData("did:tdw:com:example:users:0e12c4ff-227b-4642-b37f-f1eff9d44914:_didid")]
    [InlineData("did:tdw:com:example:users:0e12c4ff-227b-4642-b37f-f1eff9d44914:_didkey")]

    // https://identity.foundation/keri/did_methods/#privacy-considerations
    [InlineData("did:keri:EXq5YqaL6L48pf0fu7IUhL0JRaU2_RxFP0AL43wYn148")]

    // https://identity.foundation/peer-did-method-spec/index.html#privacy-considerations
    [InlineData("did:peer:2")]
    [InlineData("did:peer:0z6MkpTHR8VNsBxYAAWHut2Geadd9jSwuBV8xRoAnwWsdvktH")]
    [InlineData("did:peer:4zQmd8CpeFPci817KDsbSAKWcXAE2mjvCQSasRewvbSF54Bd:z2M1k7h4psgp4CmJcnQn2Ljp7Pz7ktsd7oBhMU3dWY5s4fhFNj17qcRTQ427C7QHNT6cQ7T3XfRh35Q2GhaNFZmWHVFq4vL7F8nm36PA9Y96DvdrUiRUaiCuXnBFrn1o7mxFZAx14JL4t8vUWpuDPwQuddVo1T8myRiVH7wdxuoYbsva5x6idEpCQydJdFjiHGCpNc2UtjzPQ8awSXkctGCnBmgkhrj5gto3D4i3EREXYq4Z8r2cWGBr2UzbSmnxW2BuYddFo9Yfm6mKjtJyLpF74ytqrF5xtf84MnGFg1hMBmh1xVx1JwjZ2BeMJs7mNS8DTZhKC7KH38EgqDtUZzfjhpjmmUfkXg2KFEA3EGbbVm1DPqQXayPYKAsYPS9AyKkcQ3fzWafLPP93UfNhtUPL8JW5pMcSV3P8v6j3vPXqnnGknNyBprD6YGUVtgLiAqDBDUF3LSxFQJCVYYtghMTv8WuSw9h1a1SRFrDQLGHE4UrkgoRvwaGWr64aM87T1eVGkP5Dt4L1AbboeK2ceLArPScrdYGTpi3BpTkLwZCdjdiFSfTy9okL1YNRARqUf2wm8DvkVGUU7u5nQA3ZMaXWJAewk6k1YUxKd7LvofGUK4YEDtoxN5vb6r1Q2godrGqaPkjfL3RoYPpDYymf9XhcgG8Kx3DZaA6cyTs24t45KxYAfeCw4wqUpCH9HbpD78TbEUr9PPAsJgXBvBj2VVsxnr7FKbK4KykGcg1W8M1JPz21Z4Y72LWgGQCmixovrkHktcTX1uNHjAvKBqVD5C7XmVfHgXCHj7djCh3vzLNuVLtEED8J1hhqsB1oCBGiuh3xXr7fZ9wUjJCQ1HYHqxLJKdYKtoCiPmgKM7etVftXkmTFETZmpM19aRyih3bao76LdpQtbw636r7a3qt8v4WfxsXJetSL8c7t24SqQBcAY89FBsbEnFNrQCMK3JEseKHVaU388ctvRD45uQfe5GndFxthj4iSDomk4uRFd1uRbywoP1tRuabHTDX42UxPjz")]

   // https://github.com/trustbloc/trustbloc-did-method/blob/main/docs/spec/trustbloc-did-method.md
    [InlineData("did:trustbloc:consortium.net:s1did12345")]
    public void ValidDidsShouldConstruct(string input)
    {
        Did did = new(input);

        Assert.NotNull(did);
        Assert.Equal(input, did.Value);
        Assert.Equal(input, did.ToString());
    }

    // Test cases from https://github.com/bluesky-social/atproto/blob/main/interop-test-files/syntax/did_syntax_invalid.txt
    [Theory]
    [InlineData("did")]
    [InlineData("didmethodval")]
    [InlineData("method:did:val")]
    [InlineData("did:method:")]
    [InlineData("didmethod:val")]
    [InlineData("did:methodval")]
    [InlineData(":did:method:val")]
    [InlineData("did.method.val")]
    [InlineData("did:method:val:")]
    [InlineData("did:method:val%")]
    [InlineData("DID:method:val")]
    [InlineData("did:METHOD:val")]
    [InlineData("did:method:val/two")]
    [InlineData("did:method:val?two")]
    [InlineData("did:method:val#two")]
    [InlineData("did:method:vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv")]
    public void InvalidDidsShouldNotConstruct(string input)
    {
        Exception? exception = Record.Exception(() =>
        {
            Did did = new(input);
        });

        Assert.NotNull(exception);
        Assert.IsType<ArgumentException>(exception);
    }

    [Theory]
    [InlineData("did:method:val")]
    [InlineData("did:method:VAL")]
    [InlineData("did:method:val123")]
    [InlineData("did:method:123")]
    [InlineData("did:method:val-two")]
    [InlineData("did:method:val_two")]
    [InlineData("did:method:val.two")]
    [InlineData("did:method:val:two")]
    [InlineData("did:method:val%BB")]
    [InlineData("did:method:vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv")]
    [InlineData("did:m:v")]
    [InlineData("did:method::::val")]
    [InlineData("did:method:-")]
    [InlineData("did:method:-:_:.:%AB")]
    [InlineData("did:method:.")]
    [InlineData("did:method:_")]
    [InlineData("did:method::.")]

    // Real world DIDs
    [InlineData("did:onion:2gzyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid")]
    [InlineData("did:example:123456789abcdefghi")]
    [InlineData("did:plc:7iza6de2dwap2sbkpav7c6c6")]
    [InlineData("did:web:example.com")]
    [InlineData("did:web:localhost%3A1234")]
    [InlineData("did:key:zQ3shZc2QzApp2oymGvQbzP8eKheVshBHbU4ZYjeXqwSKEn6N")]
    [InlineData("did:ethr:0xb9c5714089478a327f09197987f16f9e5d936e8aN")]
    public void TryParseOnValidDidsShouldBeSuccessful(string input)
    {
        bool parseResult = Did.TryParse(input, out Did? result);

        Assert.True(parseResult);
        Assert.NotNull(result);
    }

    // Test cases from https://github.com/bluesky-social/atproto/blob/main/interop-test-files/syntax/did_syntax_invalid.txt
    [Theory]
    [InlineData("did")]
    [InlineData("didmethodval")]
    [InlineData("method:did:val")]
    [InlineData("did:method:")]
    [InlineData("didmethod:val")]
    [InlineData("did:methodval")]
    [InlineData(":did:method:val")]
    [InlineData("did.method.val")]
    [InlineData("did:method:val:")]
    [InlineData("did:method:val::::")]
    [InlineData("did:method::")]
    [InlineData("did:method:::::")]
    [InlineData("did:method:val%")]
    [InlineData("did:method:val%bb")]
    [InlineData("did:method:-:_:.:%ab")]
    [InlineData("did:method:val%ZZ")]
    [InlineData("did:method:val%FG")]
    [InlineData("did:method:val%F")]
    [InlineData("did:method:val%FG:tt")]
    [InlineData("did:method:val%B:foo")]
    [InlineData("did:method:val%zz")]
    [InlineData("DID:method:val")]
    [InlineData("did:METHOD:val")]
    [InlineData("did:Method:val")]
    [InlineData("did:method:val/two")]
    [InlineData("did:method:val?two")]
    [InlineData("did:method:val#two")]
    [InlineData("did:method:vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv")]
    public void TryParseOnInvalidDidsShouldReturnFalse(string input)
    {
        bool parseResult = Did.TryParse(input, out Did? result);

        Assert.False(parseResult);
        Assert.Null(result);
    }

    [Fact]
    public void ImplicitConversionFromValidStringWorks()
    {
        const string value = "did:method:val";

        Did did = value;

        Assert.NotNull(did);
        Assert.Equal(value, did.Value);
    }

    [Fact]
    public void ExplicitConversionFromValidStringWorks()
    {
        const string value = "did:method:val";

        Did did = (Did)value;

        Assert.NotNull(did);
        Assert.Equal(value, did.Value);

        AtIdentifier did2 = Did.Create(value);
        Assert.NotNull(did2);
        Assert.Equal(value, did2.Value);
        Assert.Equal(did, did2);
    }

    [Fact]
    public void EqualityWorks()
    {
        Did lhs = new("did:method:val");
        Did rhs = new("did:method:val");

        Assert.NotNull(lhs);
        Assert.NotNull(rhs);
        Assert.Equal(lhs, rhs);
        Assert.True(lhs.Equals(rhs));
    }

    [InlineData("did:example:123456789abcdefghi", "example")]
    [InlineData("did:plc:7iza6de2dwap2sbkpav7c6c6", "plc")]
    [InlineData("did:web:example.com", "web")]
    [InlineData("did:web:localhost%3A1234", "web")]
    [InlineData("did:key:zQ3shZc2QzApp2oymGvQbzP8eKheVshBHbU4ZYjeXqwSKEn6N", "key")]
    [InlineData("did:ethr:0xb9c5714089478a327f09197987f16f9e5d936e8aN", "ethr")]
    [Theory]
    public void MethodShouldBeExtractedWithValidDids(string input, string expectedMethod)
    {
        Did did = new(input);

        Assert.NotNull(did);
        Assert.Equal(input, did.Value);
        Assert.Equal(input, did.ToString());
        Assert.Equal(expectedMethod, did.Method);
    }
}
