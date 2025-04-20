// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using idunno.AtProto.Labels;

namespace idunno.AtProto.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class LabelValueDefinitionTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public void LabelValueDefinitionDeserializesPropertyWithSourceGeneratedJsonContext()
        {
            string json = """
             {
                 "adultOnly": false,
                 "blurs": "content",
                 "defaultSetting": "hide",
                 "identifier": "spam",
                 "locales": [
                     {
                         "description": "Unwanted, repeated, or unrelated actions that bother users.",
                         "lang": "en",
                         "name": "Spam"
                     }
                 ],
                 "severity": "inform"
             }
             """;

            LabelValueDefinition? labelValueDefinition = JsonSerializer.Deserialize<LabelValueDefinition>(json, AtProtoServer.AtProtoJsonSerializerOptions);

            Assert.NotNull(labelValueDefinition);
            Assert.False(labelValueDefinition.AdultOnly);
            Assert.Equal("content", labelValueDefinition.Blurs);
            Assert.Equal("inform", labelValueDefinition.Severity);
            Assert.Equal("hide", labelValueDefinition.DefaultSetting);
            Assert.Equal("spam", labelValueDefinition.Identifier);
            Assert.NotEmpty(labelValueDefinition.Locales);
            Assert.Equal("en", labelValueDefinition.Locales.ElementAt(0).Lang);
            Assert.Equal("Spam", labelValueDefinition.Locales.ElementAt(0).Name);
            Assert.Equal("Unwanted, repeated, or unrelated actions that bother users.", labelValueDefinition.Locales.ElementAt(0).Description);

            json = """
            {
                "adultOnly":false,
                "blurs":"content",
                "defaultSetting":"ignore",
                "identifier":"amplifier",
                "locales":[
                    {
                        "description":"User frequently posts links to sources which are used to launder fringe or extremist ideas into mainstream discourse. For more information on why this is harmful, regardless of intent, please see: Phillips, Whitney. 'The Oxygen of Amplification.' Data and Society, May 22, 2018. https://datasociety.net/library/oxygen-of-amplification/.",
                        "lang":"en",
                        "name":"Amplifier"
                    }
                ],
                "severity":"alert"
            }
            """;

            labelValueDefinition = JsonSerializer.Deserialize<LabelValueDefinition>(json, AtProtoServer.AtProtoJsonSerializerOptions);

            Assert.NotNull(labelValueDefinition);
            Assert.False(labelValueDefinition.AdultOnly);
            Assert.Equal("content", labelValueDefinition.Blurs);
            Assert.Equal("alert", labelValueDefinition.Severity);
            Assert.Equal("ignore", labelValueDefinition.DefaultSetting);
            Assert.Equal("amplifier", labelValueDefinition.Identifier);
            Assert.NotEmpty(labelValueDefinition.Locales);
            Assert.Equal("en", labelValueDefinition.Locales.ElementAt(0).Lang);
            Assert.Equal("Amplifier", labelValueDefinition.Locales.ElementAt(0).Name);
            Assert.Equal("User frequently posts links to sources which are used to launder fringe or extremist ideas into mainstream discourse. For more information on why this is harmful, regardless of intent, please see: Phillips, Whitney. 'The Oxygen of Amplification.' Data and Society, May 22, 2018. https://datasociety.net/library/oxygen-of-amplification/.", labelValueDefinition.Locales.ElementAt(0).Description);
        }

        [Fact]
        public void LabelValueDefinitionDeserializesPropertyWithNoSourceGeneration()
        {
            string json = """
             {
                 "adultOnly": false,
                 "blurs": "content",
                 "defaultSetting": "hide",
                 "identifier": "spam",
                 "locales": [
                     {
                         "description": "Unwanted, repeated, or unrelated actions that bother users.",
                         "lang": "en",
                         "name": "Spam"
                     }
                 ],
                 "severity": "inform"
             }
             """;

            LabelValueDefinition? labelValueDefinition = JsonSerializer.Deserialize<LabelValueDefinition>(json, _jsonSerializerOptions);

            Assert.NotNull(labelValueDefinition);
            Assert.False(labelValueDefinition.AdultOnly);
            Assert.Equal("content", labelValueDefinition.Blurs);
            Assert.Equal("inform", labelValueDefinition.Severity);
            Assert.Equal("hide", labelValueDefinition.DefaultSetting);
            Assert.Equal("spam", labelValueDefinition.Identifier);
            Assert.NotEmpty(labelValueDefinition.Locales);
            Assert.Equal("en", labelValueDefinition.Locales.ElementAt(0).Lang);
            Assert.Equal("Spam", labelValueDefinition.Locales.ElementAt(0).Name);
            Assert.Equal("Unwanted, repeated, or unrelated actions that bother users.", labelValueDefinition.Locales.ElementAt(0).Description);

            json = """
            {
                "adultOnly":false,
                "blurs":"content",
                "defaultSetting":"ignore",
                "identifier":"amplifier",
                "locales":[
                    {
                        "description":"User frequently posts links to sources which are used to launder fringe or extremist ideas into mainstream discourse. For more information on why this is harmful, regardless of intent, please see: Phillips, Whitney. 'The Oxygen of Amplification.' Data and Society, May 22, 2018. https://datasociety.net/library/oxygen-of-amplification/.",
                        "lang":"en",
                        "name":"Amplifier"
                    }
                ],
                "severity":"alert"
            }
            """;

            labelValueDefinition = JsonSerializer.Deserialize<LabelValueDefinition>(json, _jsonSerializerOptions);

            Assert.NotNull(labelValueDefinition);
            Assert.False(labelValueDefinition.AdultOnly);
            Assert.Equal("content", labelValueDefinition.Blurs);
            Assert.Equal("alert", labelValueDefinition.Severity);
            Assert.Equal("ignore", labelValueDefinition.DefaultSetting);
            Assert.Equal("amplifier", labelValueDefinition.Identifier);
            Assert.NotEmpty(labelValueDefinition.Locales);
            Assert.Equal("en", labelValueDefinition.Locales.ElementAt(0).Lang);
            Assert.Equal("Amplifier", labelValueDefinition.Locales.ElementAt(0).Name);
            Assert.Equal("User frequently posts links to sources which are used to launder fringe or extremist ideas into mainstream discourse. For more information on why this is harmful, regardless of intent, please see: Phillips, Whitney. 'The Oxygen of Amplification.' Data and Society, May 22, 2018. https://datasociety.net/library/oxygen-of-amplification/.", labelValueDefinition.Locales.ElementAt(0).Description);
        }
    }
}
