// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Actor;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class VerificationTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public void VerificationViewDeserializesCorrectlyWithSourceGeneratedJsonContext()
        {
            string json = """
                
                {
                    "issuer": "did:plc:z72i7hdynmk6r22z27h6tvur",
                    "uri": "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lndpysuow22f",
                    "isValid": true,
                    "createdAt": "2025-04-21T10:49:31.969Z"
                }               
                """;

            VerificationView? actual = JsonSerializer.Deserialize<VerificationView>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(actual);

            Assert.Equal("did:plc:z72i7hdynmk6r22z27h6tvur", actual.Issuer);
            Assert.Equal("at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lndpysuow22f", actual.Uri);
            Assert.True(actual.IsValid);
            Assert.Equal(DateTimeOffset.Parse("2025-04-21T10:49:31.969Z"), actual.CreatedAt);
        }

        [Fact]
        public void VerificationViewDeserializesCorrectlyWithoutSourceGeneratedJsonContext()
        {
            string json = """
                
                {
                    "issuer": "did:plc:z72i7hdynmk6r22z27h6tvur",
                    "uri": "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lndpysuow22f",
                    "isValid": true,
                    "createdAt": "2025-04-21T10:49:31.969Z"
                }               
                """;

            VerificationView? actual = JsonSerializer.Deserialize<VerificationView>(json, _jsonSerializerOptions);

            Assert.NotNull(actual);

            Assert.Equal("did:plc:z72i7hdynmk6r22z27h6tvur", actual.Issuer);
            Assert.Equal("at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lndpysuow22f", actual.Uri);
            Assert.True(actual.IsValid);
            Assert.Equal(DateTimeOffset.Parse("2025-04-21T10:49:31.969Z"), actual.CreatedAt);
        }

        [Fact]
        public void VerificationStateDeserializesCorrectlyWithSourceGeneratedJsonContext()
        {
            string json = """
                {
                    "verifications": [
                        {
                            "issuer": "did:plc:z72i7hdynmk6r22z27h6tvur",
                            "uri": "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lndpysuow22f",
                            "isValid": true,
                            "createdAt": "2025-04-21T10:49:31.969Z"
                        }
                    ],
                    "verifiedStatus": "valid",
                    "trustedVerifierStatus": "none"
                }
                """;

            VerificationState? actual = JsonSerializer.Deserialize<VerificationState>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(actual);
            Assert.Equal(VerificationStatus.Valid, actual.VerifiedStatus);
            Assert.Equal(VerificationStatus.None, actual.TrustedVerifierStatus);

            Assert.NotEmpty(actual.Verifications);
            Assert.Single(actual.Verifications);
        }

        [Fact]
        public void VerificationStateDeserializesCorrectlyWithoutSourceGeneratedJsonContext()
        {
            string json = """
                {
                    "verifications": [
                        {
                            "issuer": "did:plc:z72i7hdynmk6r22z27h6tvur",
                            "uri": "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lndpysuow22f",
                            "isValid": true,
                            "createdAt": "2025-04-21T10:49:31.969Z"
                        }
                    ],
                    "verifiedStatus": "valid",
                    "trustedVerifierStatus": "none"
                }
                """;

            VerificationState? actual = JsonSerializer.Deserialize<VerificationState>(json, _jsonSerializerOptions);

            Assert.NotNull(actual);
            Assert.Equal(VerificationStatus.Valid, actual.VerifiedStatus);
            Assert.Equal(VerificationStatus.None, actual.TrustedVerifierStatus);

            Assert.NotEmpty(actual.Verifications);
            Assert.Single(actual.Verifications);
        }

        [Fact]
        public void VerificationRecordValueDeserializesCorrectlyWithSourceGeneratedJsonContext()
        {
            string json = """
                {
                  "$type": "app.bsky.graph.verification",
                  "handle": "ashley.dev",
                  "subject": "did:plc:5tgxxpsiv36w3e37im6kd2se",
                  "createdAt": "2025-04-21T10:49:31.969Z",
                  "displayName": "Ashley Willis-McNamara"
                }
                """;

            BlueskyRecord? actual = JsonSerializer.Deserialize<BlueskyRecord>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.IsType<Verification>(actual);

            Verification? verificationRecordValue = actual as Verification;

            Assert.NotNull(verificationRecordValue);
            Assert.Equal("ashley.dev", verificationRecordValue.Handle);
            Assert.Equal("did:plc:5tgxxpsiv36w3e37im6kd2se", verificationRecordValue.Subject);
            Assert.Equal(DateTimeOffset.Parse("2025-04-21T10:49:31.969Z"), verificationRecordValue.CreatedAt);
            Assert.Equal("Ashley Willis-McNamara", verificationRecordValue.DisplayName);
        }
    }
}
