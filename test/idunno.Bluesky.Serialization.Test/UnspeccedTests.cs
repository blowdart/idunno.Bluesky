// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Unspecced.Model;

namespace idunno.Bluesky.Serialization.Test;

public class UnspeccedTests
{
    [Fact]
    public void TrendingTopicsWithoutDescriptionDeserializeCorrectly()
    {
        string json = """
            
            
            {
                "topics": [
                {
                    "topic": "Manchester Elections",
                    "link": "/profile/trending.bsky.app/feed/815157883"
                },
                {
                    "topic": "Horror Films",
                    "link": "/profile/trending.bsky.app/feed/815029726"
                },
                {
                    "topic": "B Movie Maniacs",
                    "link": "/profile/trending.bsky.app/feed/815012533"
                },
                {
                    "topic": "Kremer Trade",
                    "link": "/profile/trending.bsky.app/feed/814994662"
                },
                {
                    "topic": "FIFA",
                    "link": "/profile/trending.bsky.app/feed/814821759"
                },
                {
                    "topic": "Gianni Infantino",
                    "link": "/profile/trending.bsky.app/feed/814861202"
                },
                {
                    "topic": "Todd Blanche",
                    "link": "/profile/trending.bsky.app/feed/814798353"
                },
                {
                    "topic": "WWE",
                    "link": "/profile/trending.bsky.app/feed/814885958"
                },
                {
                    "topic": "Cinematography",
                    "link": "/profile/trending.bsky.app/feed/814866039"
                },
                {
                    "topic": "Art Fight",
                    "link": "/profile/trending.bsky.app/feed/814810276"
                }
            ],
            "suggested": [
                {
                    "topic": "Popular with Friends",
                    "link": "/profile/bsky.app/feed/with-friends"
                },
                {
                    "topic": "Quiet Posters",
                    "link": "/profile/why.bsky.team/feed/infreq"
                },
                {
                    "topic": "Sports",
                    "link": "/profile/crevier.bsky.social/feed/aaanstr6k5dvo"
                },
                {
                    "topic": "NFL",
                    "link": "/profile/parkermolloy.com/feed/aaai44jkavvrs"
                },
                {
                    "topic": "NBA",
                    "link": "/profile/davelevitan.bsky.social/feed/aaadvxju4txkk"
                },
                {
                    "topic": "WNBA",
                    "link": "/profile/trollhamels.bsky.social/feed/aaac3xufjdvjg"
                },
                {
                    "topic": "MLB",
                    "link": "/profile/parkermolloy.com/feed/aaap7dpu57ve6"
                },
                {
                    "topic": "NHL",
                    "link": "/profile/hockeyhotline.bsky.social/feed/aaacm5rbitxqa"
                },
                {
                    "topic": "Cats",
                    "link": "/profile/jaz.bsky.social/feed/cv:cat"
                },
                {
                    "topic": "Gardening",
                    "link": "/profile/eepy.bsky.social/feed/aaao6g552b33o"
                },
                {
                    "topic": "Dogs",
                    "link": "/profile/jaz.bsky.social/feed/cv:dog"
                },
                {
                    "topic": "Game Dev",
                    "link": "/profile/trezy.codes/feed/game-dev"
                },
                {
                    "topic": "Web Dev",
                    "link": "/profile/did:plc:m2sjv3wncvsasdapla35hzwj/feed/web-development"
                },
                {
                    "topic": "Video Games",
                    "link": "/profile/wyattswickedgoods.com/feed/aaaaieaxm5v3y"
                },
                {
                    "topic": "Anime",
                    "link": "/profile/anianimals.moe/feed/anime-en-new"
                },
                {
                    "topic": "Music",
                    "link": "/profile/cookieduh.xyz/feed/aaagw7oidihfs"
                },
                {
                    "topic": "Film & TV",
                    "link": "/profile/francesmeh.reviews/feed/aaaotdzmoni2q"
                },
                {
                    "topic": "Taylor Swift",
                    "link": "/profile/heheviolet.bsky.social/feed/aaakqsvp6kke4"
                },
                {
                    "topic": "Fashion",
                    "link": "/profile/sammyouatts.bsky.social/feed/aaacqhe34hlv6"
                },
                {
                    "topic": "Pop Culture",
                    "link": "/profile/nahuel.bsky.social/feed/aaae2qpt4236c"
                },
                {
                    "topic": "Fitness/Health",
                    "link": "/profile/sammyouatts.bsky.social/feed/aaadcogx3hvwc"
                },
                {
                    "topic": "Beauty",
                    "link": "/profile/abmuse.net/feed/aaac256qq7vh4"
                },
                {
                    "topic": "Science",
                    "link": "/profile/bossett.social/feed/for-science"
                },
                {
                    "topic": "Blacksky Trending",
                    "link": "/profile/rudyfraser.com/feed/blacksky-trend"
                }
            ]
        }
        """;

        GetTrendingTopicsResponse? trendingTopics = JsonSerializer.Deserialize<GetTrendingTopicsResponse>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(trendingTopics);
        Assert.NotNull(trendingTopics.Topics);
        Assert.NotNull(trendingTopics.Suggested);

        Assert.Equal(10, trendingTopics.Topics.Count);

        Assert.Equal("Manchester Elections", trendingTopics.Topics.ElementAt(0).Topic);
        Assert.Equal("/profile/trending.bsky.app/feed/815157883", trendingTopics.Topics.ElementAt(0).Link) ;
        Assert.Null(trendingTopics.Topics.ElementAt(0).Description);
        Assert.Null(trendingTopics.Topics.ElementAt(0).DisplayName);

        Assert.Equal(24, trendingTopics.Suggested.Count);
        Assert.Equal("Popular with Friends", trendingTopics.Suggested.ElementAt(0).Topic);
        Assert.Equal("/profile/bsky.app/feed/with-friends", trendingTopics.Suggested.ElementAt(0).Link);
        Assert.Null(trendingTopics.Suggested.ElementAt(0).Description);
        Assert.Null(trendingTopics.Suggested.ElementAt(0).DisplayName);
    }

    [Fact]
    public void TrendingTopicsWithDescriptionsAndDisplayNamesDeserializeCorrectly()
    {
        string json = """
            {
                "topics": [
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/8ed06fe41219",
                        "description": "Trump's DOJ admitted damage was caused by a contractor's flawed installation, not vandalism by ex-Olympian David Hearn.",
                        "topic": "DOJ drops Reflecting Pool charges",
                        "displayName": "DOJ drops Reflecting Pool charges"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/5412a1f74176",
                        "description": "Spain says ~48,000 of 60,000 who crossed have gone back; at least 57 deaths reported.",
                        "topic": "Most Ceuta migrants return to Morocco",
                        "displayName": "Most Ceuta migrants return to Morocco"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/30f1646be875",
                        "description": "Infantino reversed course after backlash from UEFA and others within FIFA itself.",
                        "topic": "FIFA scraps World Cup sell-off",
                        "displayName": "FIFA scraps World Cup sell-off"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/97d665442b87",
                        "description": "Audience reactions are mixed; Matt Damon's casting divides viewers.",
                        "topic": "Nolan's The Odyssey released",
                        "displayName": "Nolan's The Odyssey released"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/f8d735a7d51e",
                        "description": "Over 30 water systems were hit; no contamination reported, but some plants safely shut down.",
                        "topic": "Iran linked to Minnesota water cyberattacks",
                        "displayName": "Iran linked to Minnesota water cyberattacks"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/ef70ff1ef2ab",
                        "description": "Over 70 missiles and 280 drones hit multiple cities; at least 8 killed, one missile crossed into Poland.",
                        "topic": "Russia strikes Ukraine with missiles",
                        "displayName": "Russia strikes Ukraine with missiles"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/ae5c0c224448",
                        "description": "Work requirements and drug subsidy cuts are pushing millions off coverage, with costs rising in 2027.",
                        "topic": "Trump cuts Medicaid and Medicare",
                        "displayName": "Trump cuts Medicaid and Medicare"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/13c71b95ff31",
                        "description": "Labour's Craig nearly doubled Reform UK's vote share, seen as a blow to Farage's party.",
                        "topic": "Bev Craig wins Greater Manchester mayor",
                        "displayName": "Bev Craig wins Greater Manchester mayor"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/e024e6e6f115",
                        "description": "Critics dispute Hassett's jobs claims, citing water use, energy costs, and environmental harm.",
                        "topic": "Backlash against AI data centers",
                        "displayName": "Backlash against AI data centers"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/21243a347717",
                        "description": "Artists are trading attacks and sharing creations in the annual summer art battle event.",
                        "topic": "ArtFight 2026",
                        "displayName": "ArtFight 2026"
                    }
                ],
                "suggested": [],
                "recIdStr": "761433928885927517"
            }
            """;

        GetTrendingTopicsResponse? trendingTopics = JsonSerializer.Deserialize<GetTrendingTopicsResponse>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(trendingTopics);
        Assert.NotNull(trendingTopics.Topics);
        Assert.NotNull(trendingTopics.Suggested);

        Assert.Equal(10, trendingTopics.Topics.Count);

        Assert.Equal("DOJ drops Reflecting Pool charges", trendingTopics.Topics.ElementAt(0).Topic);
        Assert.Equal("/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/8ed06fe41219", trendingTopics.Topics.ElementAt(0).Link);
        Assert.Equal("Trump's DOJ admitted damage was caused by a contractor's flawed installation, not vandalism by ex-Olympian David Hearn.", trendingTopics.Topics.ElementAt(0).Description);
        Assert.Equal("DOJ drops Reflecting Pool charges", trendingTopics.Topics.ElementAt(0).DisplayName);

        Assert.Empty(trendingTopics.Suggested);
    }
}
