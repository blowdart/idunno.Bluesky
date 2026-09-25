// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;

using idunno.AtProto;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace idunno.Bluesky.Test;

internal static class JwtBuilder
{
    public static string CreateJwt(Did did, string issuer, TimeSpan? expiresIn = null)
    {
        expiresIn ??= new TimeSpan(0, 15, 0);

        Dictionary<string, object> claims = new()
        {
            { JwtRegisteredClaimNames.Sub, did.ToString() }
        };

        SecurityKey key = new RsaSecurityKey(RSA.Create(2048));

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = issuer,
            Audience = issuer,
            Claims = claims,
            IssuedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow + expiresIn,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256Signature)
        };

        JsonWebTokenHandler handler = new()
        {
            SetDefaultTimesOnTokenCreation = false
        };

        return handler.CreateToken(descriptor);
    }
}
