// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky.Serialization.Test
{
    public class GetSuggestedActorsTests
    {
        [Fact]
        public void GetSuggestedActorsDeserializesCorrectly()
        {
            string json = """
                {
                "isFallback": false,
                "suggestions": [
                    {
                        "did": "did:plc:x4qyokjtdzgl7gmqhsw4ajqj",
                        "handle": "bencollins.bsky.social",
                        "displayName": "Tim Onion",
                        "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:x4qyokjtdzgl7gmqhsw4ajqj/bafkreihfnahgxywmhcl4fffsa65tk4edrio5bjyy3s3yabr6caymiyl73a@jpeg",
                        "associated": {
                            "chat": {
                                "allowIncoming": "all"
                            },
                            "activitySubscription": {
                                "allowSubscriptions": "followers"
                            }
                        },
                        "viewer": {
                            "muted": false,
                            "blockedBy": false
                        },
                        "labels": [],
                        "createdAt": "2023-04-27T14:23:34.544Z",
                        "verification": {
                            "verifications": [
                                {
                                    "issuer": "did:plc:z72i7hdynmk6r22z27h6tvur",
                                    "uri": "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lndpvfaugw2l",
                                    "isValid": true,
                                    "createdAt": "2025-04-21T10:47:37.028Z"
                                }
                            ],
                            "verifiedStatus": "valid",
                            "trustedVerifierStatus": "none"
                        },
                        "description": "pamphleteer",
                        "indexedAt": "2025-08-24T02:46:52.998Z"
                    },
                    {
                        "did": "did:plc:qvzn322kmcvd7xtnips5xaun",
                        "handle": "scalzi.com",
                        "displayName": "John Scalzi",
                        "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:qvzn322kmcvd7xtnips5xaun/bafkreih4dn5gllculyzb6wlqcqparkax35zloe3bzn2nufeqeilz4sutsu@jpeg",
                        "associated": {
                            "chat": {
                                "allowIncoming": "following"
                            },
                            "activitySubscription": {
                                "allowSubscriptions": "followers"
                            }
                        },
                        "viewer": {
                            "muted": false,
                            "blockedBy": false
                        },
                        "labels": [],
                        "createdAt": "2023-04-27T16:05:17.859Z",
                        "verification": {
                            "verifications": [
                                {
                                    "issuer": "did:plc:z72i7hdynmk6r22z27h6tvur",
                                    "uri": "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lrg4ekamcf2e",
                                    "isValid": true,
                                    "createdAt": "2025-06-12T14:16:56.128Z"
                                }
                            ],
                            "verifiedStatus": "valid",
                            "trustedVerifierStatus": "none"
                        },
                        "description": "I enjoy pie.\n\nSocial Media FAQ: https://whatever.scalzi.com/2025/04/16/the-official-john-scalzi-social-media-faq/",
                        "indexedAt": "2025-04-17T03:05:39.172Z"
                    }
                ],
                "recId": 151653027839741950
                }
                """;

            SuggestedActors? actualSuggestedActors = JsonSerializer.Deserialize<SuggestedActors>(json, options: BlueskyJsonSerializerOptions.Options);

            Assert.NotNull(actualSuggestedActors);
            Assert.False(actualSuggestedActors.IsFallback);
#pragma warning disable 612, 618
            Assert.Equal(151653027839741950, actualSuggestedActors.RecId);
#pragma warning restore 612, 618
            Assert.Equal(2, actualSuggestedActors.Suggestions.Count);
        }
    }
}
