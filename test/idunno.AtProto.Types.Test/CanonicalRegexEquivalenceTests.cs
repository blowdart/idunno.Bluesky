// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;

namespace idunno.AtProto.Types.Test;

// Did, Handle, Nsid, RecordKey, TimestampIdentifier and AtUri validate with hand written parsers rather than the regular expressions
// from the atproto specifications, because the parsers are much faster. These tests check that each parser accepts exactly
// the strings that the canonical regular expression accepts.
//
// The canonical expressions end with $, which in .NET also matches before a trailing new line, so the reference expressions
// replace the final $ with \z. Accepting a trailing new line was a bug in the old regex based validation.
public class CanonicalRegexEquivalenceTests
{
    private const int FuzzIterations = 20_000;

    private static readonly Regex s_did = Strict(Did.ValidationRegex);
    private static readonly Regex s_handle = Strict(Handle.ValidationRegex);
    private static readonly Regex s_nsid = Strict(Nsid.ValidationRegex);
    private static readonly Regex s_recordKey = Strict(RecordKey.ValidationRegex);
    private static readonly Regex s_tid = Strict(TimestampIdentifier.ValidationRegex);

    // The regular expressions AtUri used before it was replaced with a hand written parser.
    private static readonly Regex s_atUriCharacters = Strict(@"^[a-zA-Z0-9._~:@!$&')(*+,;=%/-]*$");
    private static readonly Regex s_atUri = Strict(@"^at:\/\/(?<authority>[a-zA-Z0-9._:%-]+)(\/(?<collection>[a-zA-Z0-9-.]+)(\/(?<rkey>[a-zA-Z0-9._~:@!$&%')(*+,;=-]+))?)?(#(?<fragment>\/[a-zA-Z0-9._~:@!$&%')(*+,;=\-[\]/\\]*))?$");

    private static Regex Strict(string pattern)
    {
        Assert.EndsWith("$", pattern, StringComparison.Ordinal);
        return new Regex(pattern[..^1] + @"\z", RegexOptions.CultureInvariant);
    }

    private static bool DidReference(string s) => s.Length <= 2048 && s_did.IsMatch(s);

    private static bool HandleReference(string s) => s.Length <= 253 && s_handle.IsMatch(s);

    private static bool NsidReference(string s) => s.Length <= 317 && s_nsid.IsMatch(s);

    private static bool RecordKeyReference(string s) => s is not ("." or "..") && s_recordKey.IsMatch(s);

    private static bool TimestampIdentifierReference(string s) => s_tid.IsMatch(s);

    private static bool AtUriReference(string s)
    {
        if (s.Length > 8192 || s.Contains('#', StringComparison.Ordinal) || s.Contains('?', StringComparison.Ordinal) || !s_atUriCharacters.IsMatch(s))
        {
            return false;
        }

        Match match = s_atUri.Match(s);
        if (!match.Success)
        {
            return false;
        }

        string authority = match.Groups["authority"].Value;
        bool validAuthority = authority.StartsWith("did:", StringComparison.OrdinalIgnoreCase) ?
            DidReference(authority) :
            HandleReference(authority);

        return validAuthority &&
            (!match.Groups["collection"].Success || NsidReference(match.Groups["collection"].Value)) &&
            (!match.Groups["rkey"].Success || RecordKeyReference(match.Groups["rkey"].Value));
    }

    public static TheoryData<string> DidInputs =>
    [
        "did:plc:z72i7hdynmk6r22z27h6tvur",
        "did:web:example.com",
        "did:web:localhost%3A8080",
        "did:method:val:two",
        "did:m:v",
        "did:m::v",
        "did:m:v:",
        "did:m:",
        "did::v",
        "did:M:v",
        "did:m:%",
        "did:m:%4",
        "did:m:%41",
        "did:m:%4a",
        "did:m:%ZZ",
        "did:m:a%41b",
        "did:m:a/b",
        "did:m:a b",
        "did:m:v\n",
        "did:m:v\r",
        "did:m",
        "did:",
        "DID:m:v",
        "did:m:" + new string('a', 2042),
        "did:m:" + new string('a', 2043),
        "",
    ];

    public static TheoryData<string> HandleInputs =>
    [
        "alice.bsky.social",
        "jay.bsky.team",
        "a.co",
        "a.b",
        "a",
        "a.",
        ".a",
        "a..b",
        "-a.b",
        "a-.b",
        "a.-b",
        "a.b-",
        "a--b.c",
        "0.a",
        "a.0",
        "a.b0",
        "a.0b",
        "xn--ls8h.test",
        "Alice.Example.COM",
        "a_b.test",
        "a b.test",
        "a@b.test",
        "a.test\n",
        "a.test\r",
        "a\n.test",
        "\u212A.com",
        "\u0130.com",
        string.Join('.', new string('a', 63), "test"),
        string.Join('.', new string('a', 64), "test"),
        string.Join('.', "a", new string('t', 63)),
        string.Join('.', "a", new string('t', 64)),
        string.Join('.', new string('a', 63), new string('b', 63), new string('c', 63), new string('d', 61)),
        string.Join('.', new string('a', 63), new string('b', 63), new string('c', 63), new string('d', 62)),
        "",
        " ",
    ];

    public static TheoryData<string> NsidInputs =>
    [
        "app.bsky.feed.post",
        "com.example.fooBar",
        "net.users.bob.ping",
        "a.b.c",
        "a-0.b-1.c",
        "a.b",
        "a",
        "a.b.c.",
        ".a.b.c",
        "a..b.c",
        "1a.b.c",
        "a.1b.c",
        "a.b.1c",
        "a.b-.c",
        "a.-b.c",
        "a-.b.c",
        "a.b.c-d",
        "a.b.c_d",
        "a.b.c\n",
        "a.b\n.c",
        string.Join('.', new string('a', 63), "b", "c"),
        string.Join('.', new string('a', 64), "b", "c"),
        string.Join('.', "a", "b", new string('c', 63)),
        string.Join('.', "a", "b", new string('c', 64)),
        string.Join('.', new string('a', 63), new string('b', 63), new string('c', 63), new string('d', 63), new string('e', 63)),
        string.Join('.', new string('a', 63), new string('b', 63), new string('c', 63), new string('d', 63), new string('e', 61)),
    ];

    public static TheoryData<string> RecordKeyInputs =>
    [
        "self",
        "3jzfcijpj2z2a",
        "~1.2-3_",
        "literal:self",
        ".",
        "..",
        "...",
        "a/b",
        "a b",
        "a@b",
        "a#b",
        "self\n",
        "self\r",
        "\n",
        new string('o', 512),
        new string('o', 513),
    ];

    public static TheoryData<string> TimestampIdentifierInputs =>
    [
        "3jzfcijpj2z2a",
        "7777777777777",
        "2222222222222",
        "jzzzzzzzzzzzz",
        "kzzzzzzzzzzzz",
        "3jzfcijpj2z2",
        "3jzfcijpj2z2aa",
        "3jzfcijpj2z21",
        "3jzfcijpj2z28",
        "3JZFCIJPJ2Z2A",
        "3jzfcijpj2z2\n",
        "3jzfcijpj2z2a\n",
    ];

    public static TheoryData<string> AtUriInputs
    {
        get
        {
            TheoryData<string> data = [];

            foreach (string prefix in new[] { "at://", "AT://", "at:/", "https://" })
            {
                foreach (string authority in new[] { "did:plc:z72i7hdynmk6r22z27h6tvur", "did:web:localhost%3A8080", "DID:plc:abc", "did:plc:", "alice.test", "alice", "", "a_b.test" })
                {
                    data.Add(prefix + authority);

                    foreach (string collection in new[] { "app.bsky.feed.post", "a.b", "1a.b.c", "app.bsky.feed.post-", "" })
                    {
                        data.Add($"{prefix}{authority}/{collection}");

                        foreach (string recordKey in new[] { "3jzfcijpj2z2a", "self", ".", "..", "a@b!c", "a/b", "", "a b" })
                        {
                            foreach (string suffix in new[] { "", "/", "\n", "#/frag", "?q=1" })
                            {
                                data.Add($"{prefix}{authority}/{collection}/{recordKey}{suffix}");
                            }
                        }
                    }
                }
            }

            data.Add("at://did:plc:abc/app.bsky.feed.post/" + new string('a', 8192));

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(DidInputs))]
    public void DidTryParseMatchesCanonicalRegex(string s) => Assert.Equal(DidReference(s), Did.TryParse(s, out _));

    [Theory]
    [MemberData(nameof(HandleInputs))]
    public void HandleTryParseMatchesCanonicalRegex(string s) => Assert.Equal(HandleReference(s), Handle.TryParse(s, out _));

    [Theory]
    [MemberData(nameof(NsidInputs))]
    public void NsidTryParseMatchesCanonicalRegex(string s) => Assert.Equal(NsidReference(s), Nsid.TryParse(s, out _));

    [Theory]
    [MemberData(nameof(RecordKeyInputs))]
    public void RecordKeyTryParseMatchesCanonicalRegex(string s) => Assert.Equal(RecordKeyReference(s), RecordKey.TryParse(s, out _));

    [Theory]
    [MemberData(nameof(TimestampIdentifierInputs))]
    public void TimestampIdentifierTryParseMatchesCanonicalRegex(string s) =>
        Assert.Equal(TimestampIdentifierReference(s), TimestampIdentifier.TryParse(s, out _));

    [Theory]
    [MemberData(nameof(AtUriInputs))]
    public void AtUriTryParseMatchesCanonicalRegex(string s) => Assert.Equal(AtUriReference(s), AtUri.TryParse(s, out _));

    [Theory]
    [InlineData("did:plc:z72i7hdynmk6r22z27h6tvur\n")]
    [InlineData("did:web:example.com\n")]
    public void DidRejectsTrailingNewLine(string s) => Assert.False(Did.TryParse(s, out _));

    [Theory]
    [InlineData("alice.bsky.social\n")]
    [InlineData("example.com\n")]
    public void HandleRejectsTrailingNewLine(string s)
    {
        Assert.False(Handle.TryParse(s, out _));
        Assert.Throws<ArgumentException>(() => new Handle(s));
    }

    [Theory]
    [InlineData("app.bsky.feed.post\n")]
    public void NsidRejectsTrailingNewLine(string s) => Assert.False(Nsid.TryParse(s, out _));

    [Theory]
    [InlineData("self\n")]
    [InlineData("3jzfcijpj2z2a\n")]
    public void RecordKeyRejectsTrailingNewLine(string s) => Assert.False(RecordKey.TryParse(s, out _));

    [Theory]
    [InlineData("3jzfcijpj2z2\n")]
    [InlineData("3jzfcijpj2z2a\n")]
    public void TimestampIdentifierRejectsTrailingNewLine(string s) => Assert.False(TimestampIdentifier.TryParse(s, out _));

    [Theory]
    [InlineData("at://did:plc:z72i7hdynmk6r22z27h6tvur\n")]
    [InlineData("at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.post\n")]
    [InlineData("at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.post/3jzfcijpj2z2a\n")]
    public void AtUriRejectsTrailingNewLine(string s) => Assert.False(AtUri.TryParse(s, out _));

    [Fact]
    public void DidTryParseMatchesCanonicalRegexForRandomInput()
    {
        string[] prefixes = ["did:", "did:plc:", "did:web:", "did:m:", "DID:", "di:", ""];

        AssertEquivalent(
            random => prefixes[random.Next(prefixes.Length)] + RandomString(random, "abzAZ09.:_-%F\n/ ", random.Next(0, 12)),
            DidReference,
            s => Did.TryParse(s, out _));
    }

    [Fact]
    public void HandleTryParseMatchesCanonicalRegexForRandomInput()
    {
        AssertEquivalent(
            random =>
            {
                string[] labels = new string[random.Next(1, 5)];
                for (int i = 0; i < labels.Length; i++)
                {
                    int length = random.Next(10) == 0 ? random.Next(61, 66) : random.Next(0, 5);
                    labels[i] = RandomString(random, "abAZ09-_\n ", length);
                }

                return string.Join('.', labels);
            },
            HandleReference,
            s => Handle.TryParse(s, out _));
    }

    [Fact]
    public void NsidTryParseMatchesCanonicalRegexForRandomInput()
    {
        AssertEquivalent(
            random =>
            {
                string[] segments = new string[random.Next(1, 6)];
                for (int i = 0; i < segments.Length; i++)
                {
                    int length = random.Next(10) == 0 ? random.Next(61, 66) : random.Next(0, 5);
                    segments[i] = RandomString(random, "abAZ09-_\n", length);
                }

                return string.Join('.', segments);
            },
            NsidReference,
            s => Nsid.TryParse(s, out _));
    }

    [Fact]
    public void RecordKeyTryParseMatchesCanonicalRegexForRandomInput()
    {
        AssertEquivalent(
            random =>
            {
                int length = random.Next(20) == 0 ? random.Next(510, 515) : random.Next(0, 6);
                return RandomString(random, "aZ09_~.:-\n/@ ", length);
            },
            RecordKeyReference,
            s => RecordKey.TryParse(s, out _));
    }

    [Fact]
    public void TimestampIdentifierTryParseMatchesCanonicalRegexForRandomInput()
    {
        AssertEquivalent(
            random => RandomString(random, "234567abcdefghijklmnopqrstuvwxyz18A\n", random.Next(12, 15)),
            TimestampIdentifierReference,
            s => TimestampIdentifier.TryParse(s, out _));
    }

    private static string RandomString(Random random, string alphabet, int length)
    {
        StringBuilder builder = new(length);
        for (int i = 0; i < length; i++)
        {
            builder.Append(alphabet[random.Next(alphabet.Length)]);
        }

        return builder.ToString();
    }

    private static void AssertEquivalent(Func<Random, string> generate, Func<string, bool> reference, Func<string, bool> tryParse)
    {
        // A fixed seed keeps the inputs, and so any failure, reproducible.
        Random random = new(20261001);
        List<string> mismatches = [];

        for (int i = 0; i < FuzzIterations; i++)
        {
            string s = generate(random);
            if (reference(s) != tryParse(s))
            {
                mismatches.Add(s);
            }
        }

        Assert.Empty(mismatches);
    }
}
