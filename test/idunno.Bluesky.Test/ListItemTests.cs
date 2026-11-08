// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class ListItemTests
{
    private static readonly AtUri s_list = new("at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.graph.list/test");
    private static readonly Did s_subject = new("did:plc:ewvi7nxzyoun6zhxrhs64oiz");
    private static readonly DateTimeOffset s_createdAt = new(2025, 4, 21, 10, 49, 31, TimeSpan.FromHours(2));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstructorsRejectNullInputs(bool explicitTimestamp)
    {
        ArgumentNullException listException = Assert.Throws<ArgumentNullException>(() =>
            explicitTimestamp ? new ListItem(null!, s_subject, s_createdAt) : new ListItem(null!, s_subject));
        ArgumentNullException subjectException = Assert.Throws<ArgumentNullException>(() =>
            explicitTimestamp ? new ListItem(s_list, null!, s_createdAt) : new ListItem(s_list, null!));

        Assert.Equal("list", listException.ParamName);
        Assert.Equal("subject", subjectException.ParamName);
    }

    [Theory]
    [InlineData("initializer")]
    [InlineData("constructor-initializer")]
    [InlineData("with")]
    public void InitializationRejectsNullList(string initialization)
    {
        ListItem original = new(s_list, s_subject, s_createdAt);

        Assert.Throws<ArgumentNullException>(() => initialization switch
        {
            "initializer" => new ListItem { List = null!, Subject = s_subject },
            "constructor-initializer" => new ListItem(s_list, s_subject) { List = null! },
            "with" => original with { List = null! },
            _ => throw new ArgumentOutOfRangeException(nameof(initialization))
        });

        Assert.Equal(s_list, original.List);
        Assert.Equal(s_subject, original.Subject);
    }

    [Theory]
    [InlineData("initializer")]
    [InlineData("constructor-initializer")]
    [InlineData("with")]
    public void InitializationRejectsNullSubject(string initialization)
    {
        ListItem original = new(s_list, s_subject, s_createdAt);

        Assert.Throws<ArgumentNullException>(() => initialization switch
        {
            "initializer" => new ListItem { List = s_list, Subject = null! },
            "constructor-initializer" => new ListItem(s_list, s_subject) { Subject = null! },
            "with" => original with { Subject = null! },
            _ => throw new ArgumentOutOfRangeException(nameof(initialization))
        });

        Assert.Equal(s_list, original.List);
        Assert.Equal(s_subject, original.Subject);
    }

    [Theory]
    [InlineData("at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.graph.list/test")]
    [InlineData("at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/com.example.lists/test")]
    [InlineData("at://did:plc:ewvi7nxzyoun6zhxrhs64oiz")]
    public void InitializationAcceptsAnyValidAtUri(string uri)
    {
        AtUri list = new(uri);
        Did subject = new("did:plc:3guzzweuqraryl3rdkimjamk");
        ListItem original = new(s_list, s_subject, s_createdAt);
        ListItem constructed = new(list, subject, s_createdAt);
        ListItem initialized = new() { List = list, Subject = subject, CreatedAt = constructed.CreatedAt };
        ListItem copied = original with { List = list, Subject = subject };
        ListItem overridden = new(s_list, s_subject, s_createdAt) { List = list, Subject = subject };

        Assert.Equal(list, constructed.List);
        Assert.Equal(subject, constructed.Subject);
        Assert.Equal(constructed, initialized);
        Assert.Equal(constructed, copied);
        Assert.Equal(constructed, overridden);
        Assert.Equal(s_createdAt.ToUniversalTime(), constructed.CreatedAt);
        Assert.Equal(TimeSpan.Zero, constructed.CreatedAt.Offset);
        Assert.Equal(s_list, original.List);
        Assert.Equal(s_subject, original.Subject);
        Assert.Equal(original, original with { });
    }
}
