// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Authentication.Models
{
    internal sealed record OAuthResponseError(string? Error, string? ErrorDescription)
    {
    }
}
