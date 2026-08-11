// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.AtProto.Server;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Links a server may provide as part its description.
/// </summary>
public sealed record Links
{
    /// <summary>
    /// Gets a URI to the server's privacy policy.
    /// </summary>
    [JsonInclude]
    public Uri? PrivacyPolicy { get; init; }

    /// <summary>
    /// Gets a URI to the server's terms of service.
    /// </summary>
    [JsonInclude]
    public Uri? TermsOfService { get; init; }
}
