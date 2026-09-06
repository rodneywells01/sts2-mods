---
name: publish-sts2-package
description: Build, verify, and manually publish an installable STS2 Mods ZIP on GitHub when asked to package, release, or publish this repository. Use for on-demand releases, not CI, runners, or automatic publishing.
---

# Publish an STS2 package

Work from the repository root. Read `AGENTS.md`, `INSTALL.md`, `scripts/Build.ps1`, and `packaging/VALIDATION.md`. The repository is `rodneywells01/sts2-mods`; verify the configured remote before writing to GitHub. The mod's display name is Random Character Options; keep its stable payload ID `RandomCharacterBlacklist`.

Rodney chose on-demand releases. Do not add GitHub Actions, self-hosted runners, startup tasks, release schedules, or background publishing. This skill guides an agent through an explicitly requested release; it is not authorization to publish unrelated changes or create a Workshop listing.

## Establish what will ship

- Inspect Git status, branch, diff, current manifest/project version, and GitHub releases/tags. Preserve unrelated work. Do not silently include uncommitted changes in a release attributed to a different commit.
- Default to the next unused patch version unless Rodney specifies another version. Treat published versions as immutable; choose a new version for changed binaries. A draft can be updated before publication.
- Confirm the installed game version using `release_info.json`. The current installer targets **v0.111.0**; a newer game requires compatibility investigation and appropriate tests before changing that guard.
- If the request is only to prepare a package, stop at a local ZIP or requested draft. An explicit request to publish authorizes the matching public GitHub release; do not ask again merely because publication is external.

## Update and build

The current build script reads the version from source; it does not automatically increment it. Keep these version references consistent:

- `src/RandomCharacterBlacklist/RandomCharacterBlacklist.json` and `.csproj`.
- Initializer version text in `Mod.cs`.
- `packaging/Install.ps1`, `packaging/README.txt`, `packaging/VALIDATION.md`.
- Versioned package paths in `tests/Installer.Tests.ps1`.
- Download links/examples in `INSTALL.md`, `README.md`, and `AGENTS.md`.
- A new `docs/release-<version>.md`. Preserve historical release notes rather than rewriting them.

Use `rg` to find additional current-version references and classify historical ones before editing. Keep the credit exactly **Built by Rodney Wells (WatersEdge) with GPT-6 Astra.**

Run from the root in PowerShell:

```powershell
./scripts/Build.ps1
./tests/Installer.Tests.ps1
```

For another Steam library, pass `-GamePath` to Build.ps1. Developers need .NET 9 and the installed game's assembly references; players need neither the SDK nor BaseLib. Build.ps1 prefers `.tools/dotnet/dotnet.exe` when available.

Use the in-game checks described in `docs/implementation.md` when game behavior changed. Do not claim a Steam friend session or full combat run from instrumented local tests. For documentation-only updates, verify the guide is present and accurate without inventing new gameplay test results.

Inspect `dist/RandomCharacterBlacklist-<version>.zip`. Expected contents:

- `RandomCharacterBlacklist/RandomCharacterBlacklist.dll` and `.json`.
- `Install.cmd`, `Install.ps1`, `INSTALL.md`, `README.txt`, `VALIDATION.md`, `checksums.json`.

Verify manifest/assembly/package versions and payload SHA-256 hashes. Do not ship GameProbe, game DLLs, SDKs, decompiled research, saves, or local project records. Game assets remain in the installed game. Preserve player-facing instructions distinguishing this installer ZIP from GitHub's source archives.

## Push and publish

Commit the release changes, push to the verified remote, and capture the **full 40-character commit SHA**. Use a `codex/` branch for new work. Follow the user's requested PR/merge scope; opening a PR does not authorize merging it. If publication must wait for a merge, leave the package/draft ready and state that dependency.

Inspect the tag/release before creation. After an uncertain response, query GitHub before retrying to avoid duplicates. Use the actual version and full SHA in these commands:

```powershell
gh release create v<version> dist/RandomCharacterBlacklist-<version>.zip --repo rodneywells01/sts2-mods --target <full-sha> --draft --title "Random Character Options <version>" --notes-file docs/release-<version>.md
gh release view v<version> --repo rodneywells01/sts2-mods --json url,isDraft,assets
```

For an authorized public release, publish the verified draft with `gh release edit v<version> --repo rodneywells01/sts2-mods --draft=false`. Mark it as a prerelease when the release notes identify it as a preview. Do not change repository visibility or publish to Workshop unless requested.

Verify the uploaded asset's SHA-256 digest matches the local ZIP, the release is no longer draft, and its direct download URL responds without authentication. Return the direct installer link, version, key changes, relevant validation, and material test limitations. Keep the source archive out of player installation directions.

Install locally only when requested or already authorized in the current task. Use the packaged installer against the verified game folder with the game closed. Publishing alone does not require replacing a player's installed mod or restarting their game.

If local AI Project records are available, reuse ignored `project-registration.json` for continuity; do not publish those records or register a duplicate project.
