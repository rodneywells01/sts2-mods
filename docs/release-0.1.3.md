# Random Character Options 0.1.3 — preview

Built by Rodney Wells (WatersEdge) with GPT-6 Astra.

For Slay the Spire 2 **v0.111.0 on Windows**.

Download **RandomCharacterBlacklist-0.1.3.zip**, extract the whole ZIP, close STS2, and run **Install.cmd**. Source-code archives are for developers. No SDK, BaseLib, or separate mod loader is required. Existing preferences are preserved.

- The entire rounded dice button is clickable, including the padding that previously did nothing.
- A uniform background, centered dice icon, and subtle hover/pressed colors replace the two-tone appearance.
- The options dropdown has its own padded panel. F8, focus restoration, and native character navigation are preserved.

Release compilation, 1,014 core checks, and Windows PowerShell installer tests passed. The updated UI was shown in an isolated game using synthetic saves. Prior instrumented local multiplayer coverage includes both-installed, host-only, and client-only configurations through run initialization; those multiplayer tests were not repeated for this UI-only update. A real Steam friend session and full combat run remain untested.

The package contains only the mod payload, installer, and documentation; no GameProbe or game assemblies. No Workshop listing exists.
