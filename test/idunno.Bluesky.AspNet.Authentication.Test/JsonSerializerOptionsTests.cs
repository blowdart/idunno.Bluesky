// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.Bluesky.AspNet.Authentication.Test;

[ExcludeFromCodeCoverage]
public class JsonSerializerOptionsTests
{
    [Fact]
    public void AuthenticationJsonSerializerOptionsAllowOutOfOrderMetadataProperties()
    {
        System.Text.Json.JsonSerializerOptions options = Authentication.JsonSerializerOptions.Options;

        Assert.True(options.AllowOutOfOrderMetadataProperties);
    }
}
