# Contributing

🎉 Firstly a huge thank you for taking the time to contribute.🎉

The following is a set of guidelines for contributing to these libraries.
These are just guidelines, not rules, so feel free to use your own judgement, and to propose changes to these guidelines in a pull request.

## Building locally

You will need, at a minimum, a [.NET SDK 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) installed.

If you want to use Visual Studio you will need [Visual Studio 2026](https://visualstudio.microsoft.com/downloads/) with the
ASP.NET and web development workload installed. Visual Studio 2026 community edition will work just fine.

You will also need NPM installed to build the ASP.NET Core Authentication sample/workspace. You can get it from [here](https://nodejs.org/en/download/).

## Issues

Issues will be marked stale after 14 days of inactivity, and closed 14 days after they have been marked stale.

_If an issue has been closed and you still feel it's relevant, feel free to ping a maintainer or add a comment to the closed issue._

## Pull requests

Pull Requests are the way concrete changes are made to the code, documentation, dependencies, and tools.

When creating a pull request please create an issue first, unless it's a simple change like a spelling correction.

* Setup your build environment
  * [Fork](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/working-with-forks/about-forks) the repo.
  * Build your fork.
  * [Branch](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/proposing-changes-to-your-work-with-pull-requests/about-branches) your fork.
* Making your code changes
  * Create an issue for the changes you want to make.
  * Add or change the code you want to add or change. Please add tests for the code you are adding or changing.
  * Build your code at the command line with `dotnet build`, this ensures all the code and documentation analyzers run.
  * [Test](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test) with `dotnet test`
  * [Commit](https://docs.github.com/en/pull-requests/committing-changes-to-your-project/creating-and-editing-commits/about-commits)
    your changes to your branch, with a meaningful commit message.
  * [Rebase](https://docs.github.com/en/get-started/using-git/about-git-rebase)
  * [Test](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test) with `dotnet test`
  * [Push](https://docs.github.com/en/get-started/using-git/pushing-commits-to-a-remote-repository) your commits to your fork.
  * Open a [pull request](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/proposing-changes-to-your-work-with-pull-requests/about-pull-requests).
  * Act on any comments in the pull request or associated issue.
  * Finally, hopefully, your pull request is merged into the main branch.

Tests use the new MTP runner. If you attempt to `dotnet test --nologo` it will report "Zero tests ran" with an exit code of 5. Remove the `--nologo` parameter.

### Full local validation

Run `pwsh -File ./test.ps1` from the repository root with Docker running Linux containers. This runs the
non-incremental Debug solution build and all test projects and frameworks. The script creates disposable PostgreSQL 17
and MySQL 8.4 containers with random credentials, temporary storage and loopback-only ports; verifies database access
from the test runner; and removes only its own containers in `finally`. Logs, a uniquely named clean-build binary log,
and separate build, test and total timings are retained under `.artifacts/test-runs/`. Existing database connection
environment variables are restored afterward.

Before every commit, also run both informational checks from the repository root after `test.ps1`:

```powershell
dotnet format style idunno.Bluesky.slnx --no-restore --severity info --verify-no-changes
dotnet format analyzers idunno.Bluesky.slnx --no-restore --severity info --verify-no-changes
```

Both must exit successfully with no outstanding diagnostics. Run them sequentially, not concurrently with each
other or build/test commands, because workspace loading can contend over generated files and produce misleading
diagnostics. `--verify-no-changes` verifies without editing files. If workspace-loading warnings appear, rerun the
affected check with `--verbosity diagnostic` and investigate the warnings before treating the result as conclusive.
A clean build alone does not establish that informational style and analyzer diagnostics are clean.

The full suite uses four parallel test modules and four xUnit workers per module. Labeler fixtures retain a unique
database per test, reuse bounded connection pools within that database, and clear only their own pool before dropping
the database. SQLite remains unpooled. Do not remove isolation, clear all pools, use a production database or change
machine-wide TCP settings to make tests faster.

For targeted database tests, provision disposable servers and set `LABELER_TEST_POSTGRESQL_CONNECTION` and
`LABELER_TEST_MYSQL_CONNECTION` in the test process. Targeted tests are useful for iteration, not a substitute for the
full pre-commit gate. Changes affecting trimming, NativeAOT or package coverage also require `pwsh -File ./trimTest.ps1`;
that validation is separate from the Debug build and test gate.

### Changing lexicons

Lexicon schemas live under `lexicons/`. Keep schemas, endpoint implementations and corresponding DTOs consistent, and add tests covering changed request and response shapes.

Before submitting a pull request, run:

```powershell
goat lex lint
goat lex breaking
```

The second command compares against published schemas and requires network access. If namespace resolution fails or a schema has not been published, there is no compatibility result for that schema.

Changes to published schemas must follow AT Protocol's evolution rules. Breaking changes require a new NSID. Include the validation results and any compatibility implications in your pull request.

### Dependencies Upgrades

Dependencies in the libraries should only be altered by maintainers.
For security reasons, we will not accept PRs that alter our `Directory.Build.props` or `nuget.config` files.
If you feel dependencies need upgrading please file an issue.
