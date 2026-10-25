// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Administration;

/// <summary>Describes a retained team member.</summary>
/// <param name="Did">The stable member DID.</param>
/// <param name="Role">The assigned role.</param>
/// <param name="Active">Whether the member can currently act.</param>
/// <param name="Removed">Whether this entry is a removal tombstone.</param>
public sealed record LabelTeamMember(Did Did, LabelTeamRole Role, bool Active, bool Removed);
