---
name: bump-nuget-version
description: Bump the NuGet package version for the FlatRedBall2 library project. Queries NuGet to check if a version exists for today, then sets the new version to YYYY.M.D.V where V increments from the latest published version today (or starts at 1). Creates a release branch named ReleaseCode_YYYY_M_D_V and commits the changes. Run this before triggering the NuGet release workflow.
disable-model-invocation: true
---

Invoking coder agent to bump the NuGet package version.

You are bumping the NuGet `<Version>` tag in the FlatRedBall2 `.csproj` file(s), then creating a release branch. Follow these steps in order:

## Step 1: Get today's date

The current date is available in your system context. Use the year, month, and day as integers (no leading zeros) to form:
- The version prefix: `YYYY.M.D` (e.g., `2026.2.28`)

## Step 2: Query NuGet for today's highest V

Fetch the version list for the FlatRedBall2 package from the NuGet flat container API (package ID must be all-lowercase in the URL):

`https://api.nuget.org/v3-flatcontainer/flatredball2/index.json`

The response has a `versions` array of strings. Filter for entries starting with `{today_prefix}.` (e.g., `2026.2.28.`). Parse the last segment of each match as an integer and find the maximum. The new version is `{today_prefix}.{max+1}`. If no versions for today exist, use `{today_prefix}.1`. If the package does not exist yet on NuGet, use `{today_prefix}.1`.

## Step 3: Create the release branch

From main, create and check out the new branch. The branch name includes the full version with underscores:
`ReleaseCode_YYYY_M_D_V` (e.g., `ReleaseCode_2026_2_28_1`)

```bash
git checkout main
git checkout -b ReleaseCode_YYYY_M_D_V
```

## Step 4: Update .csproj files

Search the repo for all `.csproj` files containing a `<Version>` tag. Read each file first, then use the Edit tool to replace the `<Version>...</Version>` line with the new version string.

## Step 5: Commit the changes

Stage only the modified `.csproj` files and commit:

```bash
git add <list of modified .csproj files>
```

Commit message should be `Bump version to {new_version}`.

**Do NOT push automatically.** After committing, inform the user that the branch is ready and ask if they want to push.

## Step 6: Report

Print a summary:
- New version string
- Whether today already had a published version (and what the previous V was) or if this is the first release today
- Branch name
- Reminder that the branch has not been pushed yet

$ARGUMENTS
