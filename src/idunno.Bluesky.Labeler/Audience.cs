// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler;

/// <summary>Defines the service-token audience fragments used by labeler hosts.</summary>
public static class Audience
{
    /// <summary>The service-token audience fragment for labeler administration APIs.</summary>
    public const string Administration = "labeler_admin";

    /// <summary>The service-token audience fragment for remote label issuance APIs.</summary>
    public const string Issuance = "labeler_issuance";
}
