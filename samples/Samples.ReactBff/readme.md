# React BFF sample

See [React BFF setup and security notes](../../docs/docs/reactBff.md) for prerequisites, localhost OAuth configuration,
development proxy setup, API behavior, tests and deployment limitations.

Run `dotnet run --project samples\Samples.ReactBff` from the repository root and browse to `http://127.0.0.1:5254/`.
The HTTP launch profile enables browser launch. No certificate or tunnel is required.
HTTP and cookies without Secure enforcement are for local development only; a production app must use HTTPS and Secure cookies.

Builds install frontend dependencies when needed and compile the frontend, but neither `dotnet build` nor `dotnet test`
runs the frontend tests. After building, run `npm test --prefix samples\Samples.ReactBff\ClientApp` from the repository
root to run them separately.

This sample creates a **real public Bluesky post** and provides a button to delete that same post.
Use a test account. Logout, expiry and application restarts do not delete posts.
