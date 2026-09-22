// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Maps between the string values the service uses for the open unions in the actor lexicons and the enumerations
/// this library projects them as.
/// </summary>
/// <remarks>
/// <para>Each of these lexicon definitions is an open union, so the set of values is decided by the service, not by
/// this library. The preference which carries the value keeps the string the service sent and exposes the
/// enumeration as a projection of it, so a value added upstream survives the read, modify and write cycle
/// <see cref="BlueskyAgent.PutPreferences(Preferences)"/> performs rather than being discarded or throwing.</para>
/// <para>Converting in the other direction rejects the <c>Unknown</c> member of each enumeration. A preference built
/// from an unrecognized value could not be written, so it is refused when it is constructed rather than when it is
/// serialized.</para>
/// </remarks>
internal static class ActorWireValues
{
    internal static ThreadSortingMode ToThreadSortingMode(string value) => value switch
    {
        "oldest" => ThreadSortingMode.Oldest,
        "newest" => ThreadSortingMode.Newest,
        "most-likes" => ThreadSortingMode.MostLikes,
        "random" => ThreadSortingMode.Random,
        "hotness" => ThreadSortingMode.Hotness,
        _ => ThreadSortingMode.Unknown
    };

    internal static string FromThreadSortingMode(ThreadSortingMode value, string paramName) => value switch
    {
        ThreadSortingMode.Oldest => "oldest",
        ThreadSortingMode.Newest => "newest",
        ThreadSortingMode.MostLikes => "most-likes",
        ThreadSortingMode.Random => "random",
        ThreadSortingMode.Hotness => "hotness",
        _ => throw UnknownValue(nameof(ThreadSortingMode), value, paramName)
    };

    internal static LabelVisibility ToLabelVisibility(string value) => value switch
    {
        "ignore" => LabelVisibility.Ignore,
        "show" => LabelVisibility.Show,
        "warn" => LabelVisibility.Warn,
        "hide" => LabelVisibility.Hide,
        _ => LabelVisibility.Unknown
    };

    internal static string FromLabelVisibility(LabelVisibility value, string paramName) => value switch
    {
        LabelVisibility.Ignore => "ignore",
        LabelVisibility.Show => "show",
        LabelVisibility.Warn => "warn",
        LabelVisibility.Hide => "hide",
        _ => throw UnknownValue(nameof(LabelVisibility), value, paramName)
    };

    internal static SavedFeedPreferenceType ToSavedFeedPreferenceType(string value) => value switch
    {
        "feed" => SavedFeedPreferenceType.Feed,
        "list" => SavedFeedPreferenceType.List,
        "timeline" => SavedFeedPreferenceType.Timeline,
        _ => SavedFeedPreferenceType.Unknown
    };

    internal static string FromSavedFeedPreferenceType(SavedFeedPreferenceType value, string paramName) => value switch
    {
        SavedFeedPreferenceType.Feed => "feed",
        SavedFeedPreferenceType.List => "list",
        SavedFeedPreferenceType.Timeline => "timeline",
        _ => throw UnknownValue(nameof(SavedFeedPreferenceType), value, paramName)
    };

    internal static MutedWordTarget ToMutedWordTarget(string value) => value switch
    {
        "content" => MutedWordTarget.Content,
        "tag" => MutedWordTarget.Tag,
        _ => MutedWordTarget.Unknown
    };

    internal static string FromMutedWordTarget(MutedWordTarget value, string paramName) => value switch
    {
        MutedWordTarget.Content => "content",
        MutedWordTarget.Tag => "tag",
        _ => throw UnknownValue(nameof(MutedWordTarget), value, paramName)
    };

    internal static MutedWordActorTarget ToMutedWordActorTarget(string value) => value switch
    {
        "all" => MutedWordActorTarget.All,
        "exclude-following" => MutedWordActorTarget.ExcludeFollowing,
        _ => MutedWordActorTarget.Unknown
    };

    internal static string FromMutedWordActorTarget(MutedWordActorTarget value, string paramName) => value switch
    {
        MutedWordActorTarget.All => "all",
        MutedWordActorTarget.ExcludeFollowing => "exclude-following",
        _ => throw UnknownValue(nameof(MutedWordActorTarget), value, paramName)
    };

    private static ArgumentOutOfRangeException UnknownValue<TEnum>(string enumName, TEnum value, string paramName) where TEnum : struct, Enum =>
        new(
            paramName,
            value,
            $"{enumName}.{value} cannot be sent to the service. It only exists to carry a value this library does not recognize.");
}
