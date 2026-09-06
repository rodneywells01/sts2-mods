# Random Character Options 0.1.4 — preview

Built by Rodney Wells (WatersEdge) with GPT-6 Astra.

For Slay the Spire 2 **v0.111.0 on Windows**. Includes the merged reveal-mode feature from PR #3.

- Choose **Immediately** for the existing reveal-on-click behavior, or **At lock-in** to preserve the mystery until Embark / Ready. Both honor your current included, unlocked characters. The result becomes visible at your own lock-in, including to the lobby. Unready keeps that pick; select Random again for a new mystery.
- Custom Random off still restores exact vanilla, seeded behavior. Custom modes use fresh rolls independent of the run seed and work with unmodded teammates through ordinary lobby synchronization.
- Cleaner settings separate activation, reveal timing, and included characters. Mode-specific help, disabled filters when custom behavior is off, F8/Escape, native navigation, and saved preferences are preserved. Older preferences keep immediate reveal.
- Sharper dice texture and a private scalable font improve clarity across game resolutions. Panel bounds are checked from 720p through 4K.

Release/core checks, singleplayer lock-in, both reveal modes across three local multiplayer configurations, two native-Random teammate cases, and animation/layout probes passed. Release compilation, 1,019 core checks, installer fixtures, and final package integrity were verified again for publication. See [validation](https://github.com/rodneywells01/sts2-mods/blob/v0.1.4/packaging/VALIDATION.md) for details and limits. Rodney reported a successful Steam match on the prior version; the new mode still needs that real-session check.

Extract **RandomCharacterBlacklist-0.1.4.zip**, close the game, and run **Install.cmd**. Existing choices are preserved. No SDK, BaseLib, GameProbe, or game assemblies are shipped.
