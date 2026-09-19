// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Drafts;

/// <summary>
/// Specifies how the local file paths in a draft's media embeds are validated before the files they point to are read and uploaded.
/// </summary>
/// <remarks>
///   <para>
///     The local paths in a draft's media embeds are not necessarily created by the current device. A draft is retrieved from the
///     service the agent is connected to, so the paths it contains should be treated as untrusted input. A draft containing a path
///     which points outside the directories a user expects to publish from would cause the contents of that file to be uploaded as
///     a blob when the draft is posted.
///   </para>
/// </remarks>
public enum DraftMediaPathValidation
{
    /// <summary>
    /// Each local media path must be fully qualified, must not be a UNC or device path, and must resolve to a location inside one of
    /// the directories configured in <see cref="BlueskyAgentOptions.DraftMediaRoots"/>. Posting the draft fails if no roots are configured.
    /// </summary>
    Enforce = 0,

    /// <summary>
    /// The local media paths in the draft are known to be correct and are read without any containment checks.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Only use this when the paths in the draft are known to have come from the current device, for example when posting a draft
    ///     the calling application has just constructed itself from paths the user chose.
    ///   </para>
    /// </remarks>
    Trust = 1
}
