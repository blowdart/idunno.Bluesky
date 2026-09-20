// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A user's labelers preferences
/// </summary>
public sealed record LabelersPreference : Preference
{
    /// <summary>
    /// Creates a new instance of <see cref="LabelersPreference"/> from the specified <paramref name="labelers"/>.
    /// </summary>
    /// <param name="labelers">A list of <see cref="LabelerPreference"/>s.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labelers"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public LabelersPreference(IReadOnlyList<LabelerPreference> labelers)
    {
        ArgumentNullException.ThrowIfNull(labelers);

        Labelers = labelers;
    }

    /// <summary>
    /// Gets the list of <see cref="LabelerPreference"/>s.
    /// </summary>
    /// <remarks>
    /// <para>The collection is copied when it is set, so later changes to the collection the caller supplied do
    /// not alter the preference, and the returned collection is read only.</para>
    /// </remarks>
    [JsonInclude]
    [JsonRequired]
    public IReadOnlyList<LabelerPreference> Labelers
    {
        get;
        init => field = new List<LabelerPreference>(value).AsReadOnly();
    }
}
