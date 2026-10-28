// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.AspNet.Labeler;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public class PolicyAlertEmailOptionsTests
{
    [Fact]
    public void DisabledEmailAllowsDefaultConfiguration()
    {
        new LabelerModerationOptions().Validate();
    }

    [Fact]
    public void EnabledEmailAcceptsAuthenticatedStartTlsConfiguration()
    {
        new LabelerModerationOptions
        {
            PolicyEmail = new()
            {
                Enabled = true,
                Server = "smtp.example.invalid",
                Port = 587,
                FromAddress = "labeler@example.invalid",
                UserName = "labeler",
                Password = "secret"
            }
        }.Validate();
    }

    [Theory]
    [InlineData(null, "labeler@example.invalid", null, null)]
    [InlineData("smtp.example.invalid", null, null, null)]
    [InlineData("smtp.example.invalid", "Not an email", null, null)]
    [InlineData("smtp.example.invalid", "labeler@example.invalid", "labeler", null)]
    public void EnabledEmailRejectsIncompleteConfiguration(
        string? server,
        string? fromAddress,
        string? userName,
        string? password)
    {
        LabelerModerationOptions options = new()
        {
            PolicyEmail = new()
            {
                Enabled = true,
                Server = server,
                FromAddress = fromAddress,
                UserName = userName,
                Password = password
            }
        };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void RejectsPortsOutsideTcpRange(int port)
    {
        LabelerModerationOptions options = new()
        {
            PolicyEmail = new() { Port = port }
        };

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }
}
