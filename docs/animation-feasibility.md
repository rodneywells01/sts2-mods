# Character-toggle animation feasibility — September 5, 2026

Requested behavior: a single character preview at the left; disabling plays death, enabling plays the power-card animation, with quick entry/exit fades. Every individual toggle immediately cancels and hides the previous preview. Include all produces no animation.

Installed v0.111.0 inspection confirms CharacterModel.CreateVisuals creates an independent NCreatureVisuals scene. The standard animator maps Dead to die and PowerUp to cast. Character overrides must be respected rather than assuming every character uses those literal clips. NCreatureVisuals initializes its SpineBody from the scene without requiring a combat creature. MegaSprite exposes animation state plus completion/interruption signals.

This is technically feasible as a local cosmetic overlay without changing lobby selection, combat state, or network messages. Reuse installed visual assets rather than redistribute them. Preload/cache visuals when options open, use a single noninteractive preview slot, and cancel its tween/animation immediately before any replacement. Guard deferred completion callbacks with a generation ID so an old animation cannot hide a new one. Free previews and cancel callbacks when the screen closes. Keep preference changes immediate even if an asset is unavailable. Include all already uses SetPressedNoSignal, providing a natural no-animation path.

This is source-level feasibility, not a completed animation implementation. Remaining validation: per-character power/death mapping, asset availability on character select, sizing/position and fade timing, rapid alternating toggles, and missing-animation fallback. The existing 0.1.0 installed package does not contain this flair.

## Implemented in 0.1.1

CharacterPreview owns one active visual and caches the game's scene instances for the current selection screen. Opening the dropdown warms the cache. Individual toggles save first, cancel and hide any prior visual, and restart the requested clip. Include all uses silent toggles and creates no preview. Closing the panel, hiding the screen, or exiting the scene cancels playback; scene exit also detaches the frame callback.

The built-in power clips are cast (Ironclad/Silent/Regent), cast_mighty (Necrobinder), and process (Defect). The implementation reads the virtual AnimationStates mapping so it honors these differences. Death uses die for the built-in roster. Missing resources or unsupported animations skip the cosmetic effect without blocking preferences. Custom-character compatibility remains unverified.

The preview fits within a 220×250 logical-pixel area immediately to the left of the Random options dropdown, anchored to its lower edge with a 24-pixel gap, ignores pointer/focus input, fades in over 120 ms, plays once, and fades out over 180 ms. One frame callback handles timing with no delayed callbacks or animation queue that could hide a replacement preview.


