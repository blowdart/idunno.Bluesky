// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace idunno.AtProto.Integration.Test
{
    internal class JwtBuilder
    {
        public static string CreateJwt(Did did, string? issuer = null, TimeSpan? expiresIn = null)
        {
            issuer ??= "https://test.internal";
            expiresIn ??= new TimeSpan(0, 15, 0);

            var claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = did.ToString()
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

        public static string CreateProofKey()
        {
            var rsaKey = new RsaSecurityKey(RSA.Create(2048));
            JsonWebKey jsonWebKey = JsonWebKeyConverter.ConvertFromSecurityKey(rsaKey);
            jsonWebKey.Alg = "PS256";
            return JsonSerializer.Serialize(jsonWebKey);
        }
    }
}
