# Benchmarks

[BenchmarkDotNet](https://benchmarkdotnet.org/) benchmarks for the hot paths in `idunno.AtProto`: web socket message
reassembly, Jetstream (V1, V1 with zstd, and V2) parsing, the Firehose decode pipeline, CIDs, and the XRPC request
and response path.

The benchmarks replay fixed corpora of messages captured from the live, public Firehose, Jetstream and AppView, checked in under
`idunno.AtProto.Benchmarks/Corpus`, so results are repeatable and no network access is needed.

## Running

```shell
dotnet run -c Release --project benchmarks/idunno.AtProto.Benchmarks -- --filter *
```

Pass `--filter *Firehose*` (or any other BenchmarkDotNet filter) to run a subset. Results are written to
`BenchmarkDotNet.Artifacts`.

## What to trust

* **Allocated bytes per operation** are deterministic for a given runtime, code and corpus. The allocation budget tests gate
  pull requests on them. `test/idunno.AtProto.Test/AllocationBudgetTests.cs` covers web socket receive, Jetstream,
  the Firehose decode pipeline and CIDs. `test/idunno.Bluesky.Test/AllocationBudgetTests.cs` covers `getTimeline` and
  `getAuthorFeed` through the XRPC client, timeline deserialization, and post and author self labels.
* **Time per operation** depends on hardware and load. Compare it only between runs on the same machine, and never gate
  on it.

## Refreshing the corpus

```shell
./benchmarks/capture.ps1 [-Handle <handle>] [-Password <app password>]
```

The `getTimeline` corpus is captured from a real account's home timeline, so capturing needs a Bluesky account. Pass
`-Handle` and `-Password` (`-AppPassword` and `-ApiKey` are aliases), or set the `_BlueskyHandle` and `_BlueskyPassword`
environment variables, which are used when the parameters are omitted. If neither is supplied the script exits with an
error. Use an app password, never your main password, and preferably a throwaway account.

The script captures new corpora, verifies them, and then prints what each allocation-budget path now allocates per
message, call or item on every target framework, with a suggested budget. Pass `-XrpcOnly` to recapture only the AppView
responses, or `-SkipCapture` to measure the existing corpora, which needs no account. Commit the new corpora together with
any budget changes in the `AllocationBudgetTests.cs` files in `test/idunno.AtProto.Test` and `test/idunno.Bluesky.Test`.

To capture without verifying or measuring, set the two environment variables and run
`dotnet run -c Release --project benchmarks/idunno.AtProto.Benchmarks -- capture [xrpc]`.

The firehose, jetstream, `getAuthorFeed` and `getProfiles` captures connect anonymously. Only the timeline request is
authenticated. Every capture records only the payload of each web socket message and the body of each AppView response,
never headers, so neither the password nor the access and refresh tokens can be recorded. It discards any web socket message,
and rejects any response, that looks like it contains a credential (see `Shared/SensitiveContent.cs`).

The timeline is additionally scrubbed (see `Shared/CaptureScrubber.cs`). Any entry that mentions the account's handle or
password is removed, and the account's DID is replaced with `did:plc:aaaaaaaaaaaaaaaaaaaaaaaa`. If the handle, password,
DID or either token remains after scrubbing, the timeline is not written. The capture logs out afterwards, which revokes the
session.

Compressed Jetstream captures enforce the configured maximum decompressed message size before inspecting their content.

The corpus tests rescan every checked-in message on every build. When `_BlueskyHandle` or `_BlueskyPassword` is set, which
`capture.ps1` does for its verification step, they also check that no corpus contains either value.

The corpus contains public posts, profiles and DIDs as they appeared on the network at capture time. The timeline also
reflects whom the capturing account follows.