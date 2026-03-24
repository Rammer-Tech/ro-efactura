---
name: publish-release
description: Create a new versioned release branch (r/x.x.x), bump version in the .csproj, update CHANGELOG.md, pack, and push the NuGet package to NuGet.org. Use when the user asks to release, publish, bump version, push to NuGet, or cut a new release of RoEFactura.
---

# Publish RoEFactura Release

## Overview

Releases follow this pattern:
- Branch naming: `r/x.x.x` (e.g. `r/1.2.0`)
- Version source of truth: `<Version>` in `RoEFactura/RoEFactura.csproj`
- Packages output to: `nupkgs/`
- CHANGELOG format: Keep a Changelog with `[Unreleased]` section at the top

## Step 1 — Determine new version

Read `RoEFactura/RoEFactura.csproj` to find `<Version>`. Ask the user which semver component to bump (major / minor / patch) if not already specified.

**Current version discovery:**
```bash
grep '<Version>' RoEFactura/RoEFactura.csproj
```

## Step 2 — Confirm before proceeding

Show the user:
- Current version → New version
- CHANGELOG `[Unreleased]` contents (if any)
- Confirm they want to proceed

## Step 3 — Create and switch to release branch

```bash
git checkout main
git pull origin main
git checkout -b r/NEW_VERSION
```

## Step 4 — Bump version in .csproj

Update `<Version>OLD</Version>` → `<Version>NEW</Version>` in `RoEFactura/RoEFactura.csproj`.

Also remove any `-local` or pre-release suffix if present (the `.csproj` sometimes has `1.1.2-local` for local dev).

## Step 5 — Update CHANGELOG.md

Move content from `[Unreleased]` to a new `[NEW_VERSION]` section. Add a release date in ISO format.

Template:
```markdown
## [Unreleased]

## [NEW_VERSION] - YYYY-MM-DD

### Added/Changed/Fixed
- (content from [Unreleased] goes here)
```

If `[Unreleased]` is empty, add a minimal placeholder:
```markdown
## [NEW_VERSION] - YYYY-MM-DD

### Changed
- Maintenance release.
```

## Step 6 — Build and verify

```bash
dotnet build -c Release
```

Fix any build errors before continuing.

## Step 7 — Pack

```bash
dotnet pack -c Release -o nupkgs/
```

Confirm the new `.nupkg` file appears in `nupkgs/`.

## Step 8 — Commit

```bash
git add RoEFactura/RoEFactura.csproj CHANGELOG.md
git commit -m "chore(release): bump version to NEW_VERSION"
git push -u origin r/NEW_VERSION
```

## Step 9 — Push to NuGet.org

```bash
dotnet nuget push nupkgs/RoeFactura.NEW_VERSION.nupkg \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

`NUGET_API_KEY` must be set in the environment. If not set, remind the user to export it:
```bash
export NUGET_API_KEY=your_key_here
```

`--skip-duplicate` prevents failure if the version was already published (safe to include always).

## Step 10 — Tag and GitHub release (optional but recommended)

```bash
git tag v/NEW_VERSION
git push origin v/NEW_VERSION
```

Then create a GitHub release via `gh`:
```bash
gh release create v/NEW_VERSION \
  --title "v NEW_VERSION" \
  --notes "$(sed -n '/## \[NEW_VERSION\]/,/## \[/p' CHANGELOG.md | head -n -1)" \
  nupkgs/RoeFactura.NEW_VERSION.nupkg
```

## Checklist

- [ ] New version confirmed with user
- [ ] On branch `r/NEW_VERSION` based off `main`
- [ ] `<Version>` updated in `.csproj` (no `-local` suffix)
- [ ] CHANGELOG.md updated (`[Unreleased]` promoted to `[NEW_VERSION]`)
- [ ] `dotnet build -c Release` passes
- [ ] `dotnet pack` produced `nupkgs/RoeFactura.NEW_VERSION.nupkg`
- [ ] Committed and pushed `r/NEW_VERSION`
- [ ] NuGet push succeeded
- [ ] Tag and GitHub release created

## Notes

- The NuGet package ID is `RoeFactura` (note: single-word, no hyphen).
- Do not merge the release branch to `main` automatically — leave it open for hotfixes.
- If pack produces packages for both net9.0 and net10.0, `dotnet pack` on the multi-targeted project still produces a single `.nupkg` with multiple TFMs inside — this is correct.
