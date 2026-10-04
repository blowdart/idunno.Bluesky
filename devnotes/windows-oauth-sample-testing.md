# Windows OAuth sample validation

This is the maintainer testing checklist for `Samples.WinUIOAuth`.
The developer-facing guide is [Adding Bluesky authentication to a Windows desktop app](../docs/docs/windowsOAuth.md).

## Build and automated checks

Open `idunno.Bluesky.slnx`, set the app under `samples/Windows` as startup, and
select Debug / Any CPU (mapped to x64) and the packaged launch profile.
For ARM64, change the app's mapping in Configuration Manager or build directly.
From Visual Studio Developer PowerShell, build unsigned packages without installing:

```powershell
msbuild samples\Samples.WinUIOAuth\Samples.WinUIOAuth.csproj /restore /t:Build /p:Configuration=Debug /p:Platform=x64 /p:GenerateAppxPackageOnBuild=true /p:AppxPackageSigningEnabled=false
msbuild samples\Samples.WinUIOAuth\Samples.WinUIOAuth.csproj /restore /t:Build /p:Configuration=Debug /p:Platform=ARM64 /p:GenerateAppxPackageOnBuild=true /p:AppxPackageSigningEnabled=false
```

Inspect the generated MSIX manifest for `dev.idunno.bluesky` protocol registration,
the correct architecture, executable and full-trust entry point. Confirm package
assets are present and no private signing certificate is included.
Unsigned artifacts are for development, not distributable installers.

Run callback routing and SDK login tests:

```powershell
dotnet test --project test\Samples.WinUIOAuth.Test\Samples.WinUIOAuth.Test.csproj
dotnet test --project test\idunno.AtProto.Integration.Test\idunno.AtProto.Integration.Test.csproj --filter-class idunno.AtProto.Integration.Test.OAuthLoginResponseTests
```

Before committing, run the repository's full gate:

```powershell
dotnet build idunno.Bluesky.slnx -c Debug --no-incremental
dotnet test
```

For cross-platform changes, verify solution restore/build/test on Linux with the
pinned SDK. Non-Windows project evaluation should contain no app sources, Windows
packages or project references. A Windows run with `-p:OS=Unix` checks that branch
but is not equivalent to a native Linux run.

## Manual deployment and checks

Deploy only on your own development machine. Enable Windows Developer Mode.
Visual Studio Deploy / F5 registers the package and scheme for the current user;
do not deploy to a shared build host. Never put real callback URIs, codes, tokens,
PKCE verifiers or keys in terminals, screenshots, issues or logs.

1. **Login/profile:** Enter a valid handle, complete consent in the system browser
   and allow it to open the app. Confirm the original window displays avatar,
   display name, handle, bio, follower and following counts. Refresh the profile.
2. **Single instance:** Launch the Start menu entry again while signed in. Confirm
   the existing window activates and keeps its session. Also try another launch
   while login is waiting.
3. **Cancellation/retry:** Log out, start login, cancel in the app, then finish
   the old browser flow. Its callback must be rejected. Start a fresh login and
   confirm success. Test invalid handles and denied consent too.
4. **Unsolicited/mismatched callback:** Run the synthetic command below when no
   login is pending, both with the app open and after closing it. Expect a
   rejection, not a restored session. Repeat during a real pending login and
   confirm the synthetic callback does not consume it.
5. **Close during login:** Close the app while awaiting consent, then finish the
   browser flow. Cold start must reject the callback because its state is gone.
   Close after successful login and relaunch; login must be required again.
6. **Timeout:** Leave consent unfinished for five minutes. Confirm timeout and
   successful fresh login.
7. **Network failures:** Disconnect during profile refresh and logout. Confirm
   readable errors and a responsive UI. Failed or canceled logout must clear
   the profile and local session; it must not claim successful remote revocation.
8. **Normal logout:** Restore connectivity, log in and log out. Confirm local
   profile removal and successful revocation. There must be no timeline or write
   operations.

Synthetic activation with no real secrets:

```powershell
Start-Process 'dev.idunno.bluesky:/callback?state=not-a-real-login&iss=https%3A%2F%2Fexample.org&code=not-a-real-code'
```

The automated router tests cover malformed, duplicate, expired and replayed
callbacks; do not copy a real successful callback into a shell to test replay.
Pay particular attention to consent return: deferred access to forwarded WinRT
activation data previously caused a disconnected COM proxy exception.
The event handler must capture managed data before returning.

## Cleanup

Use Log out before closing if remote revocation is desired. Closing alone only
discards local credentials. If offline logout fails, remove the authorization
through the account's app settings as needed.

Close the app and uninstall **idunno.Bluesky Windows OAuth sample** through Windows
Installed apps for the current user. Confirm its package/protocol handler is
removed. Do not edit the registry manually. Uninstall does not revoke account
authorization or clear browser cookies.
