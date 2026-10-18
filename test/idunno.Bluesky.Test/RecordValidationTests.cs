// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Drafts;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.Graph;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class RecordValidationTests
{
    private static readonly AtUri s_postUri = new("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ldegderd6s2e");
    private static readonly AtUri s_listUri = new("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.list/3ldegderd6s2e");
    private static readonly DateTimeOffset s_createdAt = new(2026, 10, 8, 15, 43, 6, TimeSpan.Zero);

    public static IEnumerable<TheoryDataRow<IValidatableObject>> ValidRecords =>
    [
        new(new Graph.List("name", ListPurpose.CurateList, null, createdAt: s_createdAt)),
        new(new StarterPack("name", null, s_listUri, [], s_createdAt, null)),
        new(new ThreadGate(s_postUri, s_createdAt, [])),
        new(new PostGate(s_postUri, s_createdAt, [], [])),
        new(new MessageInput("")),
        new(new Actor.Link(new Uri("https://example.com"), new string('x', 40), CreateBlob("image/webp", 100_000), null, s_createdAt)),
        new(new Actor.Status("example.test#future", new EmbeddedExternal("https://example.com", "", ""), 1, s_createdAt)),
        new(new Generator { Did = new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), DisplayName = new string('x', 24), CreatedAt = s_createdAt, Description = new string('x', 300), Avatar = CreateBlob("image/jpeg", 1_000_000) }),
        new(new Draft([new DraftPost("")], null, langs: [], postGateEmbeddingRules: [], threadGateAllowRules: [])),
        new(new DraftPost("")),
        new(new DraftEmbedImage(new DraftEmbedLocalRef("image.jpg"), "")),
        new(new DraftEmbedVideo(new DraftEmbedLocalRef("video.mp4"), "", [new DraftEmbedCaption("", "en")])),
        new(new DraftEmbedCaption("", "en")),
        new(new DraftEmbedLocalRef("image.jpg")),
        new(new DraftEmbedGallery([])),
        new(new DraftEmbedExternal(new Uri("https://example.com"))),
        new(new DraftEmbedRecord(new StrongReference(s_postUri, new Cid("bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une"))))
    ];

    [Theory]
    [MemberData(nameof(ValidRecords))]
    public void ValidRecordsSupportDataAnnotationsAndRejectNullContexts(IValidatableObject value)
    {
        Assert.Empty(value.Validate(new ValidationContext(value)));
        Assert.Throws<ArgumentNullException>(() => value.Validate(null!));
        List<ValidationResult> results = [];
        Assert.True(Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true));
        Assert.Empty(results);
    }

    public static IEnumerable<TheoryDataRow<IValidatableObject, string>> InvalidRecords =>
    [
        new(new ThreadGate(s_postUri, s_createdAt, [null!]), "Rules[0]"),
        new(new ThreadGate(s_postUri, s_createdAt, [], [null!]), "HiddenReplies[0]"),
        new(new PostGate(s_postUri, s_createdAt, [null!], []), "Rules[0]"),
        new(new PostGate(s_postUri, s_createdAt, [], [null!]), "DetachedEmbeddingUris[0]"),
        new(new ThreadGate(s_postUri, s_createdAt) { Post = null! }, "Post"),
        new(new PostGate(s_postUri, s_createdAt, [], []) { Post = s_listUri }, "Post"),
        new(new StarterPack("name", null, s_listUri, [null!], s_createdAt, null), "Feeds[0]"),
        new(new Draft([null!], null), "Posts[0]"),
        new(new Draft([new DraftPost("")], null, langs: [null!]), "Langs[0]"),
        new(new DraftPost("", new DraftEmbedImage[] { null! }), "EmbedImages[0]"),
        new(new DraftEmbedGallery([null!]), "Items[0]"),
        new(new DraftEmbedVideo(new DraftEmbedLocalRef("video.mp4"), captions: [null!]), "Captions[0]"),
        new(new DraftEmbedExternal(new Uri("relative", UriKind.Relative)), "Uri"),
        new(new Actor.Link(new Uri("http://example.com"), null, null, null, s_createdAt), "Url"),
        new(new Actor.Link(new Uri("relative", UriKind.Relative), null, null, null, s_createdAt), "Url"),
        new(new Actor.Link(new Uri("https://example.com"), null, null, null, s_createdAt) { Url = null! }, "Url"),
        new(new Actor.Status("example.test#future", new EmbeddedImages(), null, s_createdAt), "Embed"),
        new(new Generator { Did = null!, DisplayName = "", CreatedAt = s_createdAt }, "Did"),
        new(new Generator { Did = new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), DisplayName = null!, CreatedAt = s_createdAt }, "DisplayName"),
        new(new Generator { Did = new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), DisplayName = new string('x', 25), CreatedAt = s_createdAt }, "DisplayName")
    ];

    [Theory]
    [MemberData(nameof(InvalidRecords))]
    public void ValidationReportsInvalidRequiredValuesAndIndexedCollectionMembers(IValidatableObject value, string member)
    {
        Assert.Contains(value.Validate(new ValidationContext(value)), result => result.MemberNames.Contains(member));
        List<ValidationResult> results = [];
        Assert.False(Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true));
        Assert.Contains(results, result => result.MemberNames.Contains(member));
    }

    [Fact]
    public void DraftConstructionAndMutationRemainStrict()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Draft([], null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DraftPost(new string('x', Maximum.DraftTextLengthInGraphemes + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DraftPost("") { Text = new string('x', Maximum.DraftTextLengthInGraphemes + 1) });
        Assert.Throws<ArgumentException>(() => new DraftEmbedLocalRef(" "));
        Assert.Throws<ArgumentException>(() => new DraftEmbedLocalRef("image.jpg") { Path = "" });
        Assert.Throws<ArgumentOutOfRangeException>(() => new DraftEmbedImage(new DraftEmbedLocalRef("image.jpg")) { AltText = new string('x', Maximum.DraftEmbedAltTextLengthInGraphemes + 1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new DraftEmbedVideo(new DraftEmbedLocalRef("video.mp4")) { AltText = new string('x', Maximum.DraftEmbedAltTextLengthInGraphemes + 1) });
        Assert.Throws<ArgumentException>(() => new DraftEmbedCaption("", "en") { Lang = " " });
        Assert.Throws<ArgumentOutOfRangeException>(() => new DraftEmbedCaption("", "en") { Content = new string('x', Maximum.DraftEmbedCaptionContentLengthInBytes + 1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MessageInput("") { Text = new string('x', Maximum.MessageLengthInGraphemes + 1) });
    }

    [Fact]
    public void ActorConstructionAndMutationRemainStrict()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Actor.Link(new Uri("https://example.com"), new string('x', 41), null, null, s_createdAt));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Actor.Link(new Uri("https://example.com")) { Title = new string('x', 41) });
        Assert.Throws<ArgumentException>(() => new Actor.Link(new Uri("https://example.com")) { Icon = CreateBlob("text/plain", 1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new Actor.Link(new Uri("https://example.com")) { Icon = CreateBlob("image/png", 100_001) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new Actor.Status("status", null, 0, s_createdAt));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Actor.Status("status", s_createdAt) { DurationMinutes = -1 });
    }

    private static Blob CreateBlob(string mimeType, int size) =>
        new(new CidLink("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), mimeType, size);

    [Fact]
    public void NullFacetIndexIsReportedThroughEveryContainingRecord()
    {
        Facet facet = new(new ByteSlice(0, 1), [new TagFacetFeature("tag")]) { Index = null! };
        IValidatableObject[] containers =
        [
            facet,
            new Post("post", s_createdAt, facets: [facet]),
            new Graph.List("list", ListPurpose.CurateList, "description", [facet], createdAt: s_createdAt),
            new Generator { Did = new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), DisplayName = "feed", CreatedAt = s_createdAt, DescriptionFacets = [facet] },
            new MessageInput("message", [facet])
        ];
        string[] paths = ["Index", "Facets[0].Index", "DescriptionFacets[0].Index", "DescriptionFacets[0].Index", "Facets[0].Index"];
        for (int index = 0; index < containers.Length; index++)
        {
            IValidatableObject value = containers[index];
            Assert.Contains(value.Validate(new ValidationContext(value)), result => result.MemberNames.Contains(paths[index]));
        }
    }

    [Fact]
    public void FacetAndFeatureFailuresRetainBothCollectionIndices()
    {
        TagFacetFeature invalid = TagFacetFeature.CreateUnvalidated(new string('x', 65));
        Facet first = new(new ByteSlice(0, 1), [invalid, new TagFacetFeature("valid")]);
        Facet second = new(new ByteSlice(0, 1), [new TagFacetFeature("valid"), invalid]);
        Post post = new("text", s_createdAt, facets: [first, second]);
        ValidationResult[] results = [.. post.Validate(new ValidationContext(post))];
        Assert.Contains(results, result => result.MemberNames.Contains("Facets[0].Features[0].Tag"));
        Assert.Contains(results, result => result.MemberNames.Contains("Facets[1].Features[1].Tag"));
        Assert.Equal(2, results.Length);
    }

    [Fact]
    public void AssociatedReferenceErrorsIdentifyEveryInvalidEntry()
    {
        StrongReference reference = new(s_postUri, new Cid("bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une"));
        Embed.External.Properties properties = new("https://example.com", "", "", associatedRefs: [null!, reference, null!]);
        Post post = new("post", s_createdAt, embeddedRecord: new EmbeddedExternal(properties));
        string[] members = [.. properties.Validate(new ValidationContext(properties)).SelectMany(result => result.MemberNames)];
        Assert.Equal(["AssociatedRefs[0]", "AssociatedRefs[2]"], members);
        string[] nestedMembers = [.. post.Validate(new ValidationContext(post)).SelectMany(result => result.MemberNames)];
        Assert.Equal(["EmbeddedRecord.External.AssociatedRefs[0]", "EmbeddedRecord.External.AssociatedRefs[2]"], nestedMembers);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void StrongReferenceMembersAreValidatedThroughEveryContainingPath(bool missingUri, bool missingCid)
    {
        StrongReference valid = new(s_postUri, new Cid("bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une"));
        StrongReference reference = valid with
        {
            Uri = missingUri ? null! : valid.Uri,
            Cid = missingCid ? null! : valid.Cid
        };
        EmbeddedRecord embed = new(reference);
        DraftEmbedRecord draftEmbed = new(reference);
        DraftPost draftPost = new("", (PostSelfLabels?)null, null, null, null, null, [draftEmbed]);
        Embed.External.Properties card = new("https://example.com", "", "", associatedRefs: [valid, reference]);
        IValidatableObject[] containers =
        [
            embed,
            new Post("post", s_createdAt, embeddedRecord: embed),
            new MessageInput("message", embed: embed),
            draftEmbed,
            draftPost,
            new Draft([draftPost], null),
            card,
            new Post("post", s_createdAt, embeddedRecord: new EmbeddedExternal(card))
        ];
        string[] prefixes =
        [
            "Record", "EmbeddedRecord.Record", "Embed.Record", "Record",
            "EmbedRecords[0].Record", "Posts[0].EmbedRecords[0].Record",
            "AssociatedRefs[1]", "EmbeddedRecord.External.AssociatedRefs[1]"
        ];

        for (int index = 0; index < containers.Length; index++)
        {
            List<string> expected = [];
            if (missingUri)
            {
                expected.Add($"{prefixes[index]}.Uri");
            }

            if (missingCid)
            {
                expected.Add($"{prefixes[index]}.Cid");
            }

            IValidatableObject value = containers[index];
            Assert.Equal(expected, value.Validate(new ValidationContext(value)).SelectMany(result => result.MemberNames));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void KnownFacetFeaturesReportMissingMembersThroughContainingRecords(string? uri)
    {
        Facet facet = new(new ByteSlice(0, 1),
        [
            new MentionFacetFeature(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk")) { Did = null! },
            new LinkFacetFeature("https://example.com") { Uri = uri! },
            new FacetFeature()
        ]);
        IValidatableObject[] containers =
        [
            facet,
            new Post("post", s_createdAt, facets: [facet]),
            new Graph.List("list", ListPurpose.CurateList, "description", [facet], createdAt: s_createdAt),
            new Generator { Did = new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), DisplayName = "feed", CreatedAt = s_createdAt, DescriptionFacets = [facet] },
            new MessageInput("message", [facet])
        ];
        string[] prefixes = ["", "Facets[0].", "DescriptionFacets[0].", "DescriptionFacets[0].", "Facets[0]."];
        for (int index = 0; index < containers.Length; index++)
        {
            IValidatableObject value = containers[index];
            string[] expected = [$"{prefixes[index]}Features[0].Did", $"{prefixes[index]}Features[1].Uri"];
            Assert.Equal(expected, value.Validate(new ValidationContext(value)).SelectMany(result => result.MemberNames));
        }
    }

    [Fact]
    public void FacetValidationPreservesNonemptyLinkAndUnknownFeatureHandling()
    {
        Facet facet = new(new ByteSlice(0, 1),
        [
            new MentionFacetFeature(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk")),
            new LinkFacetFeature("not an absolute URI"),
            new FacetFeature()
        ]);
        Post post = new("post", s_createdAt, facets: [facet]);
        Assert.Empty(post.Validate(new ValidationContext(post)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ProfileReferencesValidateRequiredMembers(bool missingUri, bool missingCid)
    {
        StrongReference valid = new(s_postUri, new Cid("bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une"));
        StrongReference reference = valid with
        {
            Uri = missingUri ? null! : valid.Uri,
            Cid = missingCid ? null! : valid.Cid
        };
        Actor.Profile profile = new()
        {
            Links = [valid, reference],
            PinnedPost = reference,
            JoinedViaStarterPack = reference
        };
        List<string> expected = [];
        foreach (string prefix in new[] { "Links[1]", "PinnedPost", "JoinedViaStarterPack" })
        {
            if (missingUri)
            {
                expected.Add($"{prefix}.Uri");
            }

            if (missingCid)
            {
                expected.Add($"{prefix}.Cid");
            }
        }

        Assert.Equal(expected, profile.Validate(new ValidationContext(profile)).SelectMany(result => result.MemberNames));
        Actor.Profile empty = new();
        Assert.Empty(empty.Validate(new ValidationContext(empty)));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", true)]
    [InlineData("code", true)]
    public void MessageJoinLinksValidateRequiredCodeAfterMutation(string? code, bool valid)
    {
        Chat.Embed.JoinLink link = new("code");
        MessageInput message = new("join") { Embed = link with { Code = code! } };
        ValidationResult[] results = [.. message.Validate(new ValidationContext(message))];
        if (valid)
        {
            Assert.Empty(results);
        }
        else
        {
            ValidationResult result = Assert.Single(results);
            Assert.Equal(["Embed.Code"], result.MemberNames);
        }
    }
}
