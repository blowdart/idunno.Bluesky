# <a name="profileViewing">Viewing a user's profile</a>

[!include[Untrusted data warning](includes/untrusted-data-warning.md)]

## <a name="gettingAProfile">Getting a profile</a>

To retrieve a user's profile use `agent.GetProfile()`, with either a `Handle` or `Did`.

```c#
var profileViewResult = await agent.GetProfile(new Handle("blowdart.me"), cancellationToken: cancellationToken);
```

A user's `ProfileView` includes their `Did`, `Handle`, display name, description, avatar etc. It also includes their follower, following and posts count, information
on the number of lists, starter packs and feed generators they have created, as well as who they will accept direct messages from.

The profile view also includes a `Viewer` property which describes the relationship between the user that requested the profile and the user the profile refers to,
including properties such as `Muted`, when the user requesting the profile has muted the user the profile refers to, as well as `BlockedBy`, `Following`, `FollowedBy`
and other properties that will affect how you should render the profile, or the item in a view that also contains the author profile, to the requesting user.

Feeds, timelines etc. will also contain a `ProfileViewBasic`, `ProfileView` or `ProfileViewDetailed`, depending on the view or API definition.

## Parsing and validating profile records

`Profile` represents the repository record, rather than the `ProfileView` returned by the app view.
Use `Profile.Parse()` or `Profile.TryParse()` with a `JsonDocument` or `JsonElement` containing the record value.
These methods validate by default. To inspect a record that violates profile constraints, pass `validate: false`
and then call `Validate()` to enumerate its failures:

```c#
using System.ComponentModel.DataAnnotations;
using idunno.Bluesky.Actor;

if (Profile.TryParse(document, validate: false, out Profile? profile))
{
    foreach (ValidationResult error in profile.Validate(new ValidationContext(profile)))
    {
        Console.WriteLine($"{string.Join(", ", error.MemberNames)}: {error.ErrorMessage}");
    }
}
```

Validation checks display-name, description and pronoun limits, link collections, the website URI, and avatar and banner
blob MIME types and declared sizes. Blob metadata is untrusted; these checks do not inspect or verify the actual file.
Tolerant parsing does not change public constructor, setter or ordinary JSON deserialization behavior, and malformed
JSON values or invalid protocol identifiers still cause parsing to fail.

### Profile link and status records

`Actor.Link` and `Actor.Status` also implement `IValidatableObject` and expose the same `JsonDocument`/`JsonElement`
`Parse` and `TryParse` overloads. Validation is enabled by default; `validate: false` preserves constraint-invalid values
for later inspection without weakening existing public constructors, setters or ordinary JSON deserialization.

Link validation checks an absolute HTTPS destination, a title of at most 40 graphemes / 320 UTF-8 bytes, and PNG, JPEG or
WebP icon metadata of at most 100,000 bytes. Status validation checks the required status value, a duration of at least
one minute when supplied, and nested external cards. Unknown status strings remain valid; validation does not check expiry.
Nested failures identify member paths such as `Embed.External.Uri`.

A profile stores strong references to link records, not their contents. Validating a profile does not fetch or validate
the referenced link records; retrieve and validate each `Actor.Link` separately. Profile view types are unchanged.

## <a name="profileLabels">Profile labels</a>

Profile views can include labels from labelers. Please see [Labels](labels.md) for more information.

## <a name="profileVerification">Profile verification</a>

Bluesky supports a composable verification system where various organizations will verify accounts, for example the New York Times may verify their reporters. The
`Verification` property holds the verification status for a user, and a list of the verifiers who have verified it. The `VerifiedStatus` can be valid, invalid, none or unknown,
you can use this to decide if you want to display an indicator of the status to your users.
