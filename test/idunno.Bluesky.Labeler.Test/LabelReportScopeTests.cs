// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Test;

public class LabelReportScopeTests
{
    private static readonly Did s_repository = new("did:plc:report-subject");
    private static readonly Cid s_cid = new("bafyreihdwdcefgh4dqkjv67uzcmw7ojee6xedzdetojuzjevtenxquvyku");

    [Theory]
    [InlineData("account", null, true)]
    [InlineData("record", null, true)]
    [InlineData("account", "empty", false)]
    [InlineData("record", "empty", false)]
    [InlineData("account", "matching", true)]
    [InlineData("record", "matching", true)]
    [InlineData("account", "different", false)]
    [InlineData("record", "different", false)]
    public void ReasonRestrictionsPreserveNullEmptyAndOpenIdentifierSemantics(
        string subjectType,
        string? restriction,
        bool accepted)
    {
        string reason = "future.example.report#newReason";
        string[]? reasons = restriction switch
        {
            null => null,
            "empty" => [],
            "matching" => [reason],
            _ => ["tools.ozone.report.defs#reasonOther"]
        };
        LabelReportScope scope = new(["account", "record"], reasons);
        Assert.Equal(accepted, scope.Accepts(Subject(subjectType), reason));
    }

    [Theory]
    [InlineData("account", null, true)]
    [InlineData("account", "empty", true)]
    [InlineData("account", "different", true)]
    [InlineData("record", null, true)]
    [InlineData("record", "empty", false)]
    [InlineData("record", "matching", true)]
    [InlineData("record", "different", false)]
    public void CollectionRestrictionsApplyOnlyToRecords(string subjectType, string? restriction, bool accepted)
    {
        Nsid[]? collections = restriction switch
        {
            null => null,
            "empty" => [],
            "matching" => [new("app.bsky.feed.post")],
            _ => [new("app.bsky.actor.profile")]
        };
        LabelReportScope scope = new(
            subjectTypes: ["account", "record"],
            reasonTypes: null,
            subjectCollections: collections);
        Assert.Equal(accepted, scope.Accepts(Subject(subjectType), "com.atproto.moderation.defs#reasonOther"));
    }

    [Theory]
    [InlineData("account", "record", false)]
    [InlineData("record", "account", false)]
    [InlineData("account", "account", true)]
    [InlineData("record", "record", true)]
    [InlineData("", "account", false)]
    public void SubjectScopeIsExplicit(string permittedType, string submittedType, bool accepted)
    {
        LabelReportScope scope = new(permittedType.Length == 0 ? [] : [permittedType]);
        Assert.Equal(accepted, scope.Accepts(Subject(submittedType), "tools.ozone.report.defs#reasonOther"));
    }

    [Fact]
    public void ScopeDefensivelyCopiesEveryCollection()
    {
        List<string> subjects = ["account", "record"];
        List<string> reasons = ["tools.ozone.report.defs#reasonOther"];
        List<Nsid> collections = [new("app.bsky.feed.post")];
        LabelReportScope scope = new(subjects, reasons, collections);
        subjects.Clear();
        reasons.Clear();
        collections.Clear();
        Assert.True(scope.Accepts(Subject("record"), "tools.ozone.report.defs#reasonOther"));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)scope.SubjectTypes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)scope.ReasonTypes!).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Nsid>)scope.SubjectCollections!).Clear());
    }

    [Theory]
    [InlineData("chat")]
    [InlineData("Account")]
    [InlineData("")]
    public void UnsupportedSubjectTypesAreRejected(string subjectType)
    {
        Assert.Throws<ArgumentException>(() => new LabelReportScope([subjectType]));
    }

    [Fact]
    public void DuplicateNullAndUnboundedScopesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new LabelReportScope(["record", "record"]));
        Assert.Throws<ArgumentException>(() => new LabelReportScope(["record"], ["reason", "reason"]));
        Assert.Throws<ArgumentException>(() => new LabelReportScope(
            subjectTypes: ["record"],
            reasonTypes: null,
            subjectCollections: [new("app.bsky.feed.post"), new("app.bsky.feed.post")]));
        Assert.Throws<ArgumentNullException>(() => new LabelReportScope([null!]));
        Assert.Throws<ArgumentNullException>(() => new LabelReportScope(["record"], [null!]));
        Assert.Throws<ArgumentNullException>(() => new LabelReportScope(
            subjectTypes: ["record"], reasonTypes: null, subjectCollections: [null!]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LabelReportScope(["record"], [new string('a', 1025)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LabelReportScope(new string[1001]));
    }

    [Theory]
    [InlineData("at://handle.example/app.bsky.feed.post/record")]
    [InlineData("at://did:plc:report-subject")]
    [InlineData("at://did:plc:report-subject/app.bsky.feed.post")]
    public void RecordReportsRequireCompleteDidBasedSubjects(string value)
    {
        Assert.Throws<ArgumentException>(() => LabelReportSubject.Record(new(value), s_cid));
    }

    [Fact]
    public void AccountAndProfileRecordRemainDistinct()
    {
        LabelReportSubject account = Subject("account");
        LabelReportSubject profile = LabelReportSubject.Record(new($"at://{s_repository}/app.bsky.actor.profile/self"), s_cid);
        Assert.Equal(s_repository, account.Repository);
        Assert.Equal(s_repository.Value, account.Subject);
        Assert.Null(account.Cid);
        Assert.Null(account.RecordUri);
        Assert.Equal("account", account.SubjectType);
        Assert.Equal(s_repository, profile.Repository);
        Assert.Equal(s_cid, profile.Cid);
        Assert.Equal("record", profile.SubjectType);
        Assert.NotEqual(account.Subject, profile.Subject);
    }

    /// <summary>Creates valid subjects without resolving them or substituting a current record version.</summary>
    private static LabelReportSubject Subject(string kind) => kind == "account"
        ? LabelReportSubject.Account(s_repository)
        : LabelReportSubject.Record(new($"at://{s_repository}/app.bsky.feed.post/record"), s_cid);
}
