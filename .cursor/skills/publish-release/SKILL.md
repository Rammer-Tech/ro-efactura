---
name: publish-release
description: Automates RoEFactura releases by analyzing commits vs main, inferring semver bump, drafting CHANGELOG, then after a single user approval creates r/x.x.x, packs, pushes NuGet, and optionally tags/GitHub release. Use when the user asks to release, publish, bump version, push to NuGet, or cut a new release.
---

# Publish RoEFactura Release (automated)

## Overview

- Branch naming: `r/x.x.x` (e.g. `r/1.2.0`)
- Version source of truth: `<Version>` in `RoEFactura/RoEFactura.csproj`
- Packages output to: `nupkgs/`
- CHANGELOG: Keep a Changelog with `## [Unreleased]` at the top (`CHANGELOG.md`)
- **One user interaction only:** show the approval block below; if the user approves (`yes`), run the rest without further prompts.

---

## Phase A — Automated analysis (read-only, no questions)

Run this phase fully before showing the approval prompt.

### A1 — Preconditions (fail fast)

- **`NUGET_API_KEY`** must be set in the environment. If missing, stop immediately and tell the user to `export NUGET_API_KEY=...`. Do **not** show the approval prompt until this is set.
- **`dotnet`** must be available.

### A2 — Current version

```bash
grep '<Version>' RoEFactura/RoEFactura.csproj
```

Parse `MAJOR.MINOR.PATCH` (strip any `-local` / pre-release suffix for bump math).

### A3 — Commit range vs `main`

Record the branch being released:

```bash
SOURCE_BRANCH="$(git rev-parse --abbrev-ref HEAD)"
```

Update refs, then list everything not on upstream main:

```bash
git fetch origin main 2>/dev/null || true
git log origin/main..HEAD --oneline
git log origin/main..HEAD --no-merges --format='%s%n%b%n---'
git diff --stat origin/main...HEAD
```

If `origin/main..HEAD` is **empty**, stop: there is nothing to release from this branch relative to `main` (or explain if `main` is not the right base).

### A4 — Semver bump rules (conventional commits)

Inspect **subject lines and bodies** of commits in `origin/main..HEAD`. Apply the **highest** matching rule:

| Priority | Signal | Bump |
|----------|--------|------|
| 1 | `BREAKING CHANGE` in body, or subject matches `^[a-z]+(\([^)]+\))?!:` (e.g. `feat(api)!:`) | **major** |
| 2 | Subject starts with `feat` (`feat:` or `feat(scope):`) | **minor** |
| 3 | Everything else (`fix`, `chore`, `docs`, `refactor`, `style`, `perf`, `test`, …) | **patch** |

Compute **NEW_VERSION** from current `MAJOR.MINOR.PATCH` accordingly.

If the user explicitly stated a version or bump type **in the same request**, that overrides the table (still show it in the plan for transparency).

### A5 — CHANGELOG draft

1. Read `CHANGELOG.md` from `## [Unreleased]` until the next `## [` heading.
2. Reuse those bullets under the correct `### Added` / `### Changed` / `### Fixed` headings when possible.
3. For commits not yet reflected, add concise bullets:
   - `feat` → usually `### Added` or `### Changed`
   - `fix` → `### Fixed`
   - `docs` / `chore` / internal-only → `### Changed` or omit if not user-facing
4. If nothing meaningful remains, use a single `### Changed` bullet: maintenance / release alignment.

Use today’s date in **ISO** format (`YYYY-MM-DD`) for the new section.

### A6 — GitHub CLI (optional step)

If you will create a GitHub release, require `gh` authenticated; if missing, note in the plan that step P will be skipped or manual.

---

## Phase B — Single approval prompt (only user gate)

Show **exactly** this structure (fill in placeholders). Do not ask follow-up questions in the same turn—wait for one reply.

```text
RELEASE PLAN
============
Current version : OLD_VERSION
Proposed version: NEW_VERSION  (MAJOR|MINOR|PATCH bump — one-line reason)
Source branch   : SOURCE_BRANCH
Base            : origin/main..HEAD (N commits)
Branch to push  : r/NEW_VERSION

Files changed vs main (summary):
  <key paths or stat one-liner>

CHANGELOG entry:
  ### Added
  - ...
  ### Changed
  - ...
  ### Fixed
  - ...

Next steps after approval (no further prompts):
  merge SOURCE_BRANCH into r/NEW_VERSION from main, bump .csproj, update CHANGELOG, build, pack, push branch, nuget push, tag vNEW_VERSION, gh release.

Approve? (yes to proceed, anything else to cancel)
```

**Approval:** proceed only if the user clearly agrees (e.g. `yes`, `y`, `approve`, `go`). Otherwise stop.

---

## Phase C — Execute without pausing (after approval)

Assume repository root is ro-efactura. Replace `NEW_VERSION` / `SOURCE_BRANCH` with concrete values.

### C1 — Release branch with all analyzed commits

```bash
git fetch origin main
git checkout main
git pull origin main
git checkout -b "r/NEW_VERSION"
git merge --no-ff "SOURCE_BRANCH" -m "merge(SOURCE_BRANCH): release NEW_VERSION"
```

Resolve conflicts if any; release must not proceed with unresolved conflicts.

### C2 — Bump version in `.csproj`

Set `<Version>NEW_VERSION</Version>` in `RoEFactura/RoEFactura.csproj`. Remove `-local` or other pre-release suffixes from that property.

### C3 — Write `CHANGELOG.md`

Insert below `## [Unreleased]`:

```markdown
## [Unreleased]

## [NEW_VERSION] - YYYY-MM-DD

### Added
...

### Changed
...

### Fixed
...
```

Leave `## [Unreleased]` empty (or a blank line) under it for future work.

### C4 — Build and pack

```bash
dotnet build -c Release
dotnet pack -c Release -o nupkgs/
```

Confirm `nupkgs/RoeFactura.NEW_VERSION.nupkg` exists.

### C5 — Commit version + changelog (if not already committed by merge)

If merge did not include `.csproj` / `CHANGELOG.md` edits:

```bash
git add RoEFactura/RoEFactura.csproj CHANGELOG.md
git commit -m "chore(release): bump version to NEW_VERSION"
```

Then:

```bash
git push -u origin "r/NEW_VERSION"
```

### C6 — NuGet.org

```bash
dotnet nuget push "nupkgs/RoeFactura.NEW_VERSION.nupkg" \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

### C7 — Tag and GitHub release

```bash
git tag -a "vNEW_VERSION" -m "vNEW_VERSION"
git push origin "vNEW_VERSION"
```

Create the GitHub release with the **`## [NEW_VERSION] - …` block from `CHANGELOG.md` verbatim** as the release notes (copy that section into `gh` `--notes` or a temp file and pass `--notes-file`). Attach the nupkg:

```bash
gh release create "vNEW_VERSION" \
  --title "vNEW_VERSION" \
  --notes-file /path/to/release-notes.md \
  "nupkgs/RoeFactura.NEW_VERSION.nupkg"
```

If `gh` is unavailable, skip and tell the user to create the release manually.

### C8 — Done

Print a short summary: branch, tag, NuGet package version, and links if known.

---

## Checklist (agent self-verify)

- [ ] `NUGET_API_KEY` verified before approval
- [ ] Semver bump justified from commit scan
- [ ] Single approval obtained
- [ ] `r/NEW_VERSION` includes merged `SOURCE_BRANCH` work
- [ ] `.csproj` version and `CHANGELOG.md` updated
- [ ] `dotnet build` / `dotnet pack` succeeded
- [ ] Branch pushed; NuGet push succeeded
- [ ] Tag `vNEW_VERSION` pushed; GitHub release created or skipped with reason

---

## Notes

- Package ID is **`RoeFactura`** (no hyphen).
- Do not merge `r/*` into `main` automatically unless the user asks; release branches can stay open for hotfixes.
- Multi-targeting (`net9.0` / `net10.0`) still yields **one** `.nupkg` per version.
- If the analyzed branch should **not** be merged (e.g. release from `main` only), the user must say so before approval; otherwise default is merge `SOURCE_BRANCH` as in C1.
