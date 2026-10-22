# <a name="labels">Labels</a>
For operating a standalone service, start with [What is a labeller?](labeller/overview.md),
then see [provider-backed persistence](labeller/persistence.md) and
[shared issuance and withdrawal](labeller/issuance.md).

Labelers and [labels](https://docs.bsky.app/docs/advanced-guides/moderation) are how Bluesky allows for composable moderation.
A label is published by a moderation service, which a user or an application can choose to subscribe to.

[!include[Untrusted data warning](includes/untrusted-data-warning.md)]

Once subscribed to a labeler requests to a Bluesky API can request the labeler's labels to be applied to posts or actors,
and the labels will be applied to records underneath the labels key. When an application encounters a label it should act on it based on
the user's preferences for that labeler.

## Verifying issued labels

Labels returned by `QueryLabels`, or received through the Firehose label subscription, can have individual signatures.
This includes negation labels. Label policy definitions in a labeler declaration do not have individual label signatures.

```c#
using idunno.AtProto.Labels;

// Resolve the issuer's DID document over the network and verify one label.
await label.VerifyAsync(cancellationToken: cancellationToken);

// Alternatively, use a DID document obtained from a trusted resolver.
label.Verify(didDocument);

// Verify a collection from the same issuer, decoding its public key once.
LabelSignatureVerifier.Verify(didDocument, issuedLabels);
```

Verification returns normally on success and throws `InvalidDataException` for unsigned labels, unsupported label versions,
missing or mismatched issuer keys, or invalid signatures. Collection verification stops at the first failure; it does not
filter or silently discard invalid labels. Group labels by `Source` before verifying labels from multiple issuers.
The offline methods do not resolve identities or refresh keys. `VerifyAsync` accepts an optional `HttpClient`, resolves the issuer
once per invocation, and does not cache or retry; for a batch, resolve and reuse the DID document instead.
Cryptographic platform failures can throw `CryptographicException`, and cancellation propagates from `VerifyAsync`.

The SDK verifies version-one canonical DAG-CBOR label data against the issuer's `#atproto_label` key, with compact,
low-S ECDSA signatures for P-256 and secp256k1. It preserves exact timestamp and CID strings and whether `neg` was omitted.
JSON round trips and unchanged `with` copies retain this information. Setting a timestamp property replaces its original spelling
with a round-trip representation, and explicitly setting `IsNegationLabel` to `false` introduces a signed `neg: false` field.
Such changes can invalidate an existing signature even when the displayed timestamp or Boolean value is unchanged.
Setting `Cid` similarly replaces its original string with the CID's standard representation.
Other changes to signed properties are also included in subsequent verification, rather than checking stale retained bytes.
Unknown fields and `$type` are not signed.
Optional `cid`, `exp`, and `neg` fields may be omitted, but explicit null values are rejected rather than treated as omissions.
Protocol timestamps may contain more fractional digits than `DateTimeOffset` can represent: public values truncate to seven digits
and retain their offsets, while verification and JSON round trips retain the full original strings.

A valid signature authenticates the issued label's contents against the supplied document; it does not establish trust in the
issuer, evaluate expiry, reconcile negations or supersession, or prove which key was published when the label was created.
The current DID document might no longer contain the key used before a rotation. Handle these policy and key-history decisions
separately. API queries still deserialize without automatically verifying labels; Firehose retains its explicit verification option.

## Signing issued labels

`LabelSigningKey` generates or imports local P-256 and secp256k1 private keys using Microsoft's
`System.Security.Cryptography` APIs and the operating system's cryptographic provider. Curve support depends on that
provider; an unsupported curve fails explicitly, without substituting another curve or using a third-party fallback.
The application chooses when to generate a key, how to protect and reload it, and when to dispose it.
Never generate a replacement automatically when loading an established key fails.

```csharp
using idunno.AtProto;
using idunno.AtProto.Labels;

// Initial setup only. Normally import the established key instead.
using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.Secp256k1);
Did issuer = new("did:plc:your-labeler");
string subject = "at://did:plc:subject/app.bsky.feed.post/3lngovum7vm2k";
DateTimeOffset createdAt = DateTimeOffset.UtcNow;

Label issued = key.CreateLabel(issuer, subject, "spam", createdAt);
Label retraction = key.CreateNegation(issuer, subject, "spam", createdAt.AddSeconds(1));

// Public data only. Publishing this under #atproto_label is a separate operation.
string publicMultikey = key.ExportPublicKeyMultibase();
byte[] canonicalPayload = issued.GetCanonicalUnsignedBytes();
```

Both factories require an explicit creation timestamp and set version 1. `CreateLabel` omits `neg`;
`CreateNegation` writes `neg: true`. Both accept optional `cid` and `expiresAt` arguments.
The caller must choose a negation timestamp that supersedes the earlier label. These methods do not look up,
authorize, persist, or publish labels, and expiry/negation reconciliation belongs to the application.

`key.Sign(existingLabel)` returns a signed copy, preserving all signed fields, including the exact retained timestamp/CID
strings and omitted versus explicit `neg: false`. It accepts unsigned version-one labels and can re-sign existing labels
after a key change without changing their creation time. It rejects missing/unsupported versions and malformed signed fields,
including values over 128 UTF-8 bytes even when introduced by an `init` mutation. The original label is unchanged.
`GetCanonicalUnsignedBytes()` also works on unsigned version-one labels and excludes `sig`, `$type`, and unknown fields.
Payload encoding is deterministic; ECDSA signature bytes need not be. Signatures use SHA-256 and 64-byte compact
P1363 encoding with low-S normalization for both curves.

Signing does not resolve the issuer or prove that its DID document publishes the selected key. Keep label private keys
separate from PDS-controlled repository signing keys, OAuth keys, and other-purpose keys. Key generation/export does not
create an identity, publish a DID document, or configure a labeler service.

### Importing and protecting private keys

External keys do not need to have been generated by the SDK. Supported import formats are PKCS#8 binary or PEM,
unencrypted or password-encrypted, for either supported named curve. Import determines the curve from the key.
Raw scalar/hex, JWK, SEC 1 (`EC PRIVATE KEY`), public-only keys, remote signers, and external non-exportable key
handles are not accepted by this API. Convert other private-key formats to PKCS#8 first.

```csharp
using System.Security.Cryptography;
using idunno.AtProto.Labels;

// passwordChars is a caller-owned char[] supplied by your host's protected secret input.
// Do not embed a password in code, configuration, logs, or command-line arguments.
try
{
    using LabelSigningKey key = LabelSigningKey.ImportFromEncryptedPem(
        File.ReadAllText(privateKeyPath), passwordChars);
    string publicMultikey = key.ExportPublicKeyMultibase();
    Label signed = key.Sign(unsignedLabel);
}
finally
{
    Array.Clear(passwordChars);
}
```

For binary input use `ImportPkcs8PrivateKey(bytes)` or `ImportEncryptedPkcs8PrivateKey(bytes, passwordChars)`.
For unencrypted PEM use `ImportFromPem(pem)`. PEM imports require exactly one block with the matching PKCS#8 label,
surrounded only by whitespace; binary imports reject trailing bytes. Invalid keys/curves and incorrect passwords
throw `CryptographicException`; invalid PEM shapes and empty encryption passwords throw `ArgumentException`.
Provider failures may also throw `PlatformNotSupportedException`. A failed import never generates a replacement.

Private export is explicit: `ExportPkcs8PrivateKey()` / `ExportPkcs8PrivateKeyPem()`, or
`ExportEncryptedPkcs8PrivateKey(passwordChars, pbeParameters)` / `ExportEncryptedPkcs8PrivateKeyPem(...)`.
Prefer encrypted exports with a strong password and explicit password-derivation parameters, for example
`new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 600_000)`.
Choose an iteration count appropriate to your environment; do not mistake encryption for a complete storage policy.
Protect backups and password custody separately.

The key has no public private-material property and no default secret-bearing string representation. Do not put key
objects or exported material into logging, response, or configuration models. The SDK clears its transient decoded
private buffers; callers must clear their own byte arrays with `CryptographicOperations.ZeroMemory` and password arrays
after use. Immutable unencrypted PEM strings cannot be erased, so prefer binary input when memory erasure is important.
Disposal releases the owned provider key; it cannot promise erasure of every provider/OS copy.
Key operations serialize with disposal, and subsequent operations throw `ObjectDisposedException`.

### Generating external keypairs with PowerShell

This example uses **PowerShell 7.6+ on Windows** and .NET cryptography, not the SDK or Windows OpenSSL.
Windows PowerShell 5.1 does not provide these modern export APIs. Both curves must be supported by the native provider.
It generates separate keys for both curves in a new directory, restricts its ACL **before** writing private material,
prompts without echoing a password, and writes encrypted PKCS#8 PEM plus public SPKI PEM.
Use a dedicated label key, not an existing repository key.

```powershell
$ErrorActionPreference = 'Stop'
$keyDirectory = Join-Path $HOME ("label-keys-" + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $keyDirectory
$sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
$acl = [System.Security.AccessControl.DirectorySecurity]::new()
$acl.SetAccessRuleProtection($true, $false)
$rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
    $sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
$acl.AddAccessRule($rule)
Set-Acl -LiteralPath $keyDirectory -AclObject $acl

$password = Read-Host 'New private-key password' -AsSecureString
$passwordPointer = [IntPtr]::Zero
$passwordChars = [char[]]::new($password.Length)
try {
    if ($password.Length -eq 0) { throw 'A nonempty password is required.' }
    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
    for ($i = 0; $i -lt $password.Length; $i++) {
        $passwordChars[$i] = [char]([Runtime.InteropServices.Marshal]::ReadInt16($passwordPointer, 2 * $i) -band 0xFFFF)
    }
    $encryption = [System.Security.Cryptography.PbeParameters]::new(
        [System.Security.Cryptography.PbeEncryptionAlgorithm]::Aes256Cbc,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256, 600000)
    $curves = @{
        p256 = '1.2.840.10045.3.1.7'
        secp256k1 = '1.3.132.0.10'
    }
    foreach ($name in $curves.Keys) {
        $key = [System.Security.Cryptography.ECDsa]::Create(
            [System.Security.Cryptography.ECCurve]::CreateFromValue($curves[$name]))
        try {
            $encrypted = $key.ExportEncryptedPkcs8PrivateKeyPem($passwordChars, $encryption)
            [IO.File]::WriteAllText((Join-Path $keyDirectory "$name.private.pem"), $encrypted)
            [IO.File]::WriteAllText((Join-Path $keyDirectory "$name.public.pem"),
                $key.ExportSubjectPublicKeyInfoPem())
        } finally {
            $key.Dispose()
        }
    }
} finally {
    [Array]::Clear($passwordChars, 0, $passwordChars.Length)
    if ($passwordPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    }
    $password.Dispose()
}
Write-Host "Key files created in $keyDirectory"
```

Import either `.private.pem` file using `ImportFromEncryptedPem` and the password through protected input.
For an unencrypted binary export instead, use `ExportPkcs8PrivateKey()`, write the returned bytes only inside the
protected directory, and clear them in `finally`. The corresponding SDK import is `ImportPkcs8PrivateKey`.
Never overwrite the established production key during setup, and never commit generated keys or backups.

### Generating external keypairs with Linux Bash and OpenSSL

Use **native Linux Bash and OpenSSL 3.x**, directly on Linux, in WSL, or in a Linux Docker container.
Do not use Git Bash or Windows OpenSSL. In WSL, keep private files on the Linux filesystem rather than a Windows mount;
in Docker, use private container storage or a protected Linux volume. Do not mount production keys or the Docker socket
into a validation container. These commands use restrictive permissions and a fresh directory to avoid overwriting a key.

```bash
set -euo pipefail
umask 077
key_directory="$(mktemp -d "$HOME/label-keys.XXXXXXXX")"

# Each command prompts for an encryption password without putting it in shell history.
# genpkey writes PKCS#8, not the SEC 1 format produced by some EC commands.
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:prime256v1 \
  -aes-256-cbc -out "$key_directory/p256.private.pem"
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:secp256k1 \
  -aes-256-cbc -out "$key_directory/secp256k1.private.pem"

# Prompts for the matching password; these outputs contain public material only.
openssl pkey -in "$key_directory/p256.private.pem" \
  -pubout -out "$key_directory/p256.public.pem"
openssl pkey -in "$key_directory/secp256k1.private.pem" \
  -pubout -out "$key_directory/secp256k1.public.pem"

printf 'Key files created in %s\n' "$key_directory"
```

OpenSSL encrypted exports must use a PKCS#8 encryption scheme supported by .NET; the OpenSSL 3.x AES/PBKDF2
exports above interoperate with `ImportFromEncryptedPem`. To generate unencrypted PEM instead, omit
`-aes-256-cbc` and import with `ImportFromPem`; this leaves an unencrypted secret on disk and is not the preferred backup.
For an unencrypted binary PKCS#8 conversion of an existing key, use
`openssl pkcs8 -topk8 -nocrypt -in input.private.pem -outform DER -out output.private.pk8`
inside the protected directory, then import with `ImportPkcs8PrivateKey`. The input password is prompted for.
Keep passwords out of `-pass pass:...`, shell history, scripts, and logs.

Public PEM/SPKI files are **not** AT Protocol compressed multikey strings. Import the private key into the SDK and call
`ExportPublicKeyMultibase()` to obtain the matching public multikey. Only that public material belongs in the
issuer's `#atproto_label` verification method; publishing it remains a separate identity-management operation.

## <a name="labelSubscriptions">Getting a user's labeler subscriptions</a>
A user's labeler subscriptions are part of a user's preferences, and can be loaded through the Bluesky agent's GetPreferences method once
the agent has authenticated.

```c#
using idunno.Bluesky.Actor;

Preferences userPreferences;
var userPreferencesResult = await agent.GetPreferences();
if (userPreferencesResult.Succeeded)
{
    userPreferences = userPreferencesResult.Result;
}
else
{
    userPreferences = new Preferences();
}
```

From there, you use the `SubscribedLabelers` property and pass that into any API that takes a `subscribedLabelers` parameter.
For example, to get a user's notifications with labels applied:

```c#
var notificationsList =
    await agent.ListNotifications(
      limit: pageSize,
      cursor: cursor,
      subscribedLabelers: userPreferences.SubscribedLabelers);
```

For notifications labels are applied to the `Author` property and the `PostView` property. You can iterate through the collection, and act on it.
For example, to display labels applied to the author of a notification:

```c#
foreach (Notification notification in notificationsList.Result)
{
    StringBuilder labelDisplayBuilder = new ();
    foreach (var label in notification.Author.Labels)
    {
        labelDisplayBuilder.Append(CultureInfo.InvariantCulture, $"[{label.Value}] ");
    }
    labelDisplayBuilder.Length--;

    Console.WriteLine($"Author: {notification.Author} {labelDisplayBuilder}");
}
```

Many APIs will take a `subscribedLabelers` parameter, including `GetProfile`, `GetSuggestions`, `SearchActors`, `GetFeed`, `GetTimeline` and so on.
It is recommended you cache the user's subscribed labelers and provide them to any API that accepts them.

By default idunno.Bluesky will return the Bluesky moderation labeler as part of a user's labeler subscriptions. This can be controlled
with the `includeBlueskyModerationLabeler` parameter on the `GetPreferences` method.

## <a name="labelReacting">Reacting to labels</a>
The value of a label will determine your application's behavior. Some example label values are porn, gore, and spam.
Label values are interpreted by their definitions. Those definitions include these attributes:

* blurs which may be content or media or none
* severity which may be alert or inform or none
* defaultSetting which may be hide or warn or ignore
* adultOnly which is a Boolean

A user chooses to hide, warn, or ignore each label from a labeler.

For more information on labels, their values and how your application should react to them, see the
[Bluesky moderation guide](https://docs.bsky.app/docs/advanced-guides/moderation).

## Publishing a labeler declaration

A labeler's policies and report scope live in its `app.bsky.labeler.service/self` repository record.
These helpers manage that declaration; they do not host a labeler, sign labels, or register the labeler
endpoint and public signing key in the account's DID document.

Use an authenticated `BlueskyAgent` to create a declaration:

```csharp
using idunno.Bluesky.Labeler;

Service declaration = new()
{
    CreatedAt = DateTimeOffset.UtcNow,
    Policies = new LabelerPolicies { LabelValues = ["spam"] },
    ReasonTypes = []
};

var createResult = await agent.CreateLabelerDeclaration(declaration);
if (!createResult.Succeeded)
{
    Console.Error.WriteLine($"Declaration creation failed: {createResult.StatusCode}, {createResult.AtErrorDetail}");
}
```

Creation uses the fixed `self` key and does not overwrite an existing declaration. The write helpers
check label-value lengths, custom identifiers, reserved custom-label names, duplicate definitions,
supported presentation settings, and localized text limits before sending a request. Custom definitions
must appear in `LabelValues` and include at least one locale. Reading existing declarations remains
permissive so future presentation values can still be inspected. The PDS also performs lexicon validation.

`ReasonTypes` has three distinct meanings:

| Value | Meaning |
| --- | --- |
| `null` (omitted from JSON) | Accept every report reason, including future additions. |
| `[]` | Accept no reports; appropriate for a publisher without report intake. |
| An explicit collection | Accept the selected reasons. |

Reasons are open strings: legacy `com.atproto.moderation.defs` reasons, granular
`tools.ozone.report.defs` reasons, and future identifiers are preserved without requiring Ozone.
`SubjectTypes` and `SubjectCollections` also preserve the distinction between omission and an empty array.
A `null` `SubjectCollections` accepts any record collection.

For updates, retrieve the record envelope to obtain its CID, then edit its value with a `with` expression:

```csharp
var readResult = await agent.GetLabelerDeclarationRecord();
if (readResult.Succeeded)
{
    Service edited = readResult.Result.Value with
    {
        ReasonTypes = ["tools.ozone.report.defs#reasonOther"]
    };

    var updateResult = await agent.UpdateLabelerDeclaration(edited, readResult.Result.Cid);
    if (!updateResult.Succeeded)
    {
        Console.Error.WriteLine($"Declaration update failed: {updateResult.StatusCode}, {updateResult.AtErrorDetail}");
    }
}
else
{
    Console.Error.WriteLine($"Declaration read failed: {readResult.StatusCode}, {readResult.AtErrorDetail}");
}
```

The update requires the expected CID. An `InvalidSwap` error means the declaration changed since it was read;
reload and reconcile the changes rather than blindly retrying. A `with` expression retains the creation time,
unchanged report scopes, policies, self-labels, and record extension data. Collection properties are shared
by a shallow record copy, so replace collections rather than mutating them in place.

**Breaking correction:** `Service.Labels` is now an optional `SelfLabels` object, matching the lexicon union,
instead of a collection of `SelfLabel`. For example, use
`Labels = new SelfLabels(new SelfLabel("!no-unauthenticated"))`.
Omitted `SubjectTypes` now remains omitted instead of becoming an empty array.
