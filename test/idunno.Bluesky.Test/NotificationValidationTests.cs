// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Notifications;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class NotificationValidationTests
{
    private static readonly Uri s_service = new("https://test.internal");

    private static AccessCredentials CreateCredentials()
    {
        return new AccessCredentials(
            service: s_service,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:test"), s_service.ToString()),
            refreshToken: "refreshToken");
    }

    [Fact]
    public async Task PutActivitySubscriptionThrowsWhenTheActivitySubscriptionIsNull()
    {
        using HttpClient httpClient = new();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => BlueskyServer.PutActivitySubscription(
            subscriptionSettings: new SubjectActivitySubscription(new Did("did:plc:test"), null),
            service: s_service,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("subscriptionSettings", exception.ParamName);
    }
}
