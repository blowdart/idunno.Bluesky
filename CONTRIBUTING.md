# Contributing

🎉 Firstly a huge thank you for taking the time to contribute.🎉

The following is a set of guidelines for contributing to these libraries.
These are just guidelines, not rules, so feel free to use your own judgement, and to propose changes to these guidelines in a pull request.

## Building locally

You will need [.NET SDK 11.0.100](https://dotnet.microsoft.com/en-us/download/dotnet/11.0) or a compatible later .NET 11 SDK, as specified in `global.json`.

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

### Database provider tests

CI provisions disposable PostgreSQL 17 and MySQL 8.4 services for the Bot SDK provider tests. Each service is limited to
two CPUs, 4 GiB of memory with no additional swap, and a 2 GiB tmpfs data directory. CI runs the shared provider cases
separately on .NET 8, 9, and 10, treats skipped cases as failures, and requires at least the current 87 cases per framework.
The normal test step excludes these cases so they are not run twice.

To run them locally, set `BOTSDK_TEST_POSTGRESQL_CONNECTION` and `BOTSDK_TEST_MYSQL_CONNECTION` to disposable databases.
The PostgreSQL account must be able to create and drop schemas; the MySQL account must be able to create and drop databases.
Never point these tests at a production database. Without these variables, local runs explicitly skip the corresponding
provider cases; SQLite cases need no server.

### Dependencies Upgrades

Dependencies in the libraries should only be altered by maintainers.
For security reasons, we will not accept PRs that alter our `Directory.Build.props` or `nuget.config` files.
If you feel dependencies need upgrading please file an issue.
