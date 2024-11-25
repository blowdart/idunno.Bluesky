# Contributing

🎉 Firstly a huge thank you for taking the time to contribute.🎉

The following is a set of guidelines for contributing to these libraries.
These are just guidelines, not rules, so feel free to use your own judgement, and to propose changes to these guidelines in a pull request.

## Issues

Issues will be marked stale after 14 days of inactivity, and closed 14 days after they have been marked stale.

_If an issue has been closed and you still feel it's relevant, feel a maintainer or and a comment to the closed issue._

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
  * [Commit](https://docs.github.com/en/pull-requests/committing-changes-to-your-project/creating-and-editing-commits/about-commits)
    your changes to your branch, with a meaningful commit message.
  * [Rebase](https://docs.github.com/en/get-started/using-git/about-git-rebase)
  * [Test](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test)
  * [Push](https://docs.github.com/en/get-started/using-git/pushing-commits-to-a-remote-repository) your commits to your fork.
  * Open a [pull request](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/proposing-changes-to-your-work-with-pull-requests/about-pull-requests).
  * Act on any comments in the pull request or associated issue.
  * Finally, hopefully, your pull request is merged into the main branch.

### Dependencies Upgrades

Dependencies in the libraries should only be altered by maintainers.
For security reasons, we will not accept PRs that alter our `DirectoryBuild.props` or `nuget.config` files.
If you feel dependencies need upgrading file an issue.
