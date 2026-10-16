# Prototyping and measuring performance improvements

This note describes how to use the benchmarks project, `benchmarks/idunno.AtProto.Benchmarks`, to try out a possible
performance improvement, prove it is faster or allocates less, and then land it without leaving throwaway code behind.
It is aimed at anyone changing a hot path in `idunno.AtProto.Types`, `idunno.AtProto` or `idunno.Bluesky`.

[`benchmarks/readme.md`](../benchmarks/readme.md) covers running the benchmarks and refreshing the corpus. This note covers
the workflow around them.

## The short version

1. Find the hot path, and check a benchmark already measures it.
2. Write the proposed implementation inside the benchmarks project, next to the current code, not in the library.
3. Prove the prototype gives the same answers as the current code before measuring it.
4. Benchmark current against proposed, as a `Baseline` pair, in the same category.
5. If it wins, move the change into the library, then delete the prototype so the benchmark measures the real code again.
6. Lock the gain in with an allocation budget.

## 1. Start from a measurement

Do not prototype on a hunch. Run the existing benchmarks for the area first, so you know what the current code costs and
have numbers to compare with:

```shell
dotnet run -c Release --project benchmarks/idunno.AtProto.Benchmarks -- --filter *Jetstream* --artifacts %TEMP%\bench
```

The benchmarks replay corpora captured from the live network (`Corpus/*.bin.gz`, read through `Shared/CorpusFile.cs`), so
the inputs look like real traffic: real DIDs, real record shapes, real message sizes. Prefer them over hand-written
inputs. A prototype that is faster on `did:plc:abc` but slower on the distribution of identifiers the firehose actually
sends is not an improvement.

If nothing measures the path you want to change, add a benchmark for the current code first, and commit that on its own.
It is useful whether or not the improvement works out.

## 2. Write the prototype in the benchmarks project

Put the proposed implementation in the benchmark file, as a private nested or file-scoped type, rather than editing the
library. That way:

* the current and proposed code can run side by side in the same process, against the same inputs,
* the library and its public API are untouched until the change has proved itself, and
* abandoning an idea costs nothing more than deleting a file.

Keep the prototype honest. It must do everything the current code does, including validation, error cases and the shape
of the object it produces. If the current code builds a `Did` and the prototype only returns a `bool`, the comparison
measures the missing work, not the improvement. When the real type cannot be constructed without its own validation
running, write a minimal stand-in type with the same fields (for example a `ProposedDid` holding `Value` and `Method`) so
the allocations are comparable.

### What the benchmarks project can see

The benchmarks project references `idunno.AtProto.Types`, `idunno.AtProto` and `idunno.Bluesky`, and all three grant it
`InternalsVisibleTo`, so a benchmark can call their internal members directly. If the code you want to measure is
private, making it `internal` is an acceptable, minimal change (that is how `AtProtoJetstream.ExtensionDataExcept` and
`AtProtoHttpClient<T>.MergeRequestHeaders` are benchmarked).

Because the benchmarks can see internals in more than one assembly, an internal type that exists in both, such as
`idunno.AtProto.SourceGenerationContext`, is ambiguous (CS0433). The `idunno.AtProto` reference has the extern alias
`AtProto` for this case: add `extern alias AtProto;` and qualify the type, for example
`AtProto::idunno.AtProto.SourceGenerationContext.Default`.

## 3. Prove equivalence before measuring

A faster answer is only an improvement if it is the same answer. Check this in `[GlobalSetup]`, before BenchmarkDotNet
times anything, and throw if the two implementations disagree:

```csharp
[GlobalSetup]
public void Setup()
{
    _inputs = BuildInputsFromCorpus();

    foreach (string input in _inputs)
    {
        bool currentAccepted = Did.TryParse(input, out Did? current);
        bool proposedAccepted = ProposedDid.TryParse(input, out ProposedDid? proposed);

        if (currentAccepted != proposedAccepted ||
            (currentAccepted && (current!.Value != proposed!.Value || current.Method != proposed.Method)))
        {
            throw new InvalidOperationException($"Current and proposed disagree on \"{input}\".");
        }
    }
}
```

Corpus inputs only exercise the success path. Add inputs that should be rejected as well: empty and whitespace strings,
values at and just over each length limit, invalid characters, and anything an existing unit test calls out. The test
projects (for example `test/idunno.AtProto.Types.Test`) are a good source of edge cases.

When the comparison finds a difference, decide which side is right before going further. Sometimes the current code is
the one that is wrong, for example a regular expression ending in `$` rather than `\z` accepts a trailing `\n`. That is a
bug fix to make deliberately, with its own test and changelog entry, not a side effect to discover later. Record any such
known differences explicitly in the setup check rather than loosening it.

## 4. Benchmark current against proposed

Measure the two implementations as a pair, with the current code as the baseline, in a shared category, so
BenchmarkDotNet reports the ratio between them:

```csharp
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TypesBenchmarks
{
    [Benchmark(Baseline = true, OperationsPerInvoke = InputCount)]
    [BenchmarkCategory("DidParse")]
    public int Current()
    {
        int accepted = 0;
        foreach (string input in _inputs)
        {
            if (Did.TryParse(input, out _))
            {
                accepted++;
            }
        }

        return accepted;
    }

    [Benchmark(OperationsPerInvoke = InputCount)]
    [BenchmarkCategory("DidParse")]
    public int Proposed()
    {
        // The same loop, calling ProposedDid.TryParse.
    }
}
```

Guidelines:

* Always use `[MemoryDiagnoser]`. Allocations are usually the bigger win in this library, and unlike time they are
  deterministic, so they can be gated on later.
* Loop over the whole input set and set `OperationsPerInvoke` to its size, so results are per item and reflect a realistic
  mix of inputs rather than one value the JIT can specialise for.
* Return or consume the result (a count, a hash, a length) so the JIT cannot eliminate the work.
* Keep setup out of the measured method. Decompressing the corpus, building strings and creating clients belong in
  `[GlobalSetup]`.
* Measure each idea separately. If a change touches parsing and equality, give each its own category, so you can tell
  which part earned the gain and drop the part that did not.

Run a dry job first to check everything compiles and the equivalence check passes, then a real run:

```shell
dotnet run -c Release --project benchmarks/idunno.AtProto.Benchmarks -- --filter *TypesBenchmarks* --job dry --artifacts %TEMP%\bench
dotnet run -c Release --project benchmarks/idunno.AtProto.Benchmarks -- --filter *TypesBenchmarks* --artifacts %TEMP%\bench
```

`--job short` is good enough to see a large difference quickly. Use the default job before drawing conclusions about a
small one. Write artifacts outside the repository and delete them afterwards.

### Reading the results

* **Allocated** is the number to trust. It is exact for a given runtime, code and corpus.
* **Mean** and **Ratio** are only meaningful between runs on the same machine, close together in time. Treat differences
  of a few percent, or anything within the error column, as noise.
* The benchmarks project targets .NET 10 only, but the libraries also ship for .NET 8 and 9, where an API that is fast
  on .NET 10 may fall back to a slower path or not exist. Check the change on the older frameworks too. The allocation
  budget tests run on every target framework, and `benchmarks/capture.ps1 -SkipCapture` reports allocations for each.
* A change that saves time but adds allocations, or the reverse, needs a judgement call. On the firehose and Jetstream
  paths, which run continuously, allocations usually matter more because they drive garbage collections.

## 5. Move the winner into the library

When a prototype wins:

1. Make the change in the library, with unit tests covering the cases the equivalence check found, including any bug it
   uncovered.
2. Delete the prototype and the `Proposed` benchmark. Change the remaining benchmark so it calls the real library code,
   and drop `Baseline = true`. Keep the category, so the benchmark carries on tracking the path.
3. Re-run the benchmark to confirm the library change performs like the prototype did. Prototypes in the benchmarks
   project do not always carry over exactly, for example when the real type has extra fields or the call goes through
   an interface.
4. Update `PublicAPI.Unshipped.txt` if the public surface changed, and add an entry to `CHANGELOG.md` under the
   unreleased section for the package.

Do not leave `Current` and `Proposed` pairs in the benchmarks project once the decision is made. After the change lands
the "current" code no longer exists, so the pair compares the library with a copy of itself and stops telling anyone
anything.

When a prototype loses, delete it. If what you learned is worth keeping, write it down in the pull request or an issue.

## 6. Lock in the gain

Benchmarks show a regression only when someone runs them. The allocation budget tests are what stop one being merged:

* `test/idunno.AtProto.Test/AllocationBudgetTests.cs` covers web socket receive, Jetstream, the Firehose pipeline and
  CIDs.
* `test/idunno.Bluesky.Test/AllocationBudgetTests.cs` covers the XRPC client, timeline deserialization and self labels.

Each budget sits roughly ten percent above the measured allocation per item. When your change reduces allocations on a
budgeted path, lower its budget to match, so a later change cannot quietly give the gain back. When it speeds up a path
that has no budget, consider adding one. `benchmarks/capture.ps1 -SkipCapture` prints the current allocation for every
budgeted path on every target framework, with a suggested budget, without needing a Bluesky account.

## Before committing

The benchmarks project is part of the solution, so the usual pre-commit gate applies: a clean
`dotnet build idunno.Bluesky.slnx -c Debug --no-incremental` and a passing `dotnet test`. A benchmark that only builds in
`Release` still fails CI.
