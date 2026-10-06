# Credits

**Past Bedtime** by Soham Pawar (Studio Kamikaze), made for the TGC Game Jam × GDAI × Infinium '26.

Every third-party asset in this repository is listed here with its source and licence. Nothing
was bought, and nothing comes from a paid marketplace or a paid tier.

## Music

| Track | Author | Licence | File |
|---|---|---|---|
| "First Snow" ([Lo-Fi and Chill collection](https://opengameart.org/content/lo-fi-and-chill-collection)) | HoliznaCC0 | CC0 | `Assets/Resources/Music/menu.ogg` |
| "Snow Drift" (same collection) | HoliznaCC0 | CC0 | `Assets/Resources/Music/credits.ogg` |
| ["Into The Wilds"](https://www.scottbuckley.com.au/library/into-the-wilds/) | Scott Buckley | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | `Assets/Resources/Music/pages.ogg` (the comic pages) |
| ["Jazzy Battle Theme"](https://opengameart.org/content/jazzy-battle-theme) | MintoDog | CC0 | `Assets/Resources/Music/boss.ogg` (the Baron Blot fights) |

"Into The Wilds" is used under CC BY 4.0, re-encoded to Ogg Vorbis and turned down 6 dB so it sits under the
action (no other changes), and credited in the game's credits as the author asks:
'Into The Wilds' by Scott Buckley - released under CC-BY 4.0. www.scottbuckley.com.au

## Sound effects

| Asset | Author | Licence | Files |
|---|---|---|---|
| [Interface Sounds](https://kenney.nl/assets/interface-sounds) | Kenney | CC0 | `Assets/Resources/Audio/ui_click, ui_confirm, ui_back, ui_toggle, torch_on, torch_off`; `Assets/Resources/Audio/Game/lens, scrub, spit, star, twist` |
| [UI Audio](https://kenney.nl/assets/ui-audio) | Kenney | CC0 | `Assets/Resources/Audio/ui_hover` |
| [Impact Sounds](https://kenney.nl/assets/impact-sounds) | Kenney | CC0 | `Assets/Resources/Audio/Footsteps/*`, `Assets/Resources/Audio/door_close.ogg`; `Assets/Resources/Audio/Game/punch, punch_heavy, hit, splat, slam, clank, ping, crash, flare` |

The door creak (`Assets/Resources/Audio/door_creak.wav`) was synthesised by the developer (procedural stick-slip friction).
The heartbeat under Mom's door is synthesised in code at runtime (`GameAudio.Heartbeat`).

## Characters and animation

| Asset | Author | Licence | Files |
|---|---|---|---|
| [Universal Animation Library (Standard)](https://opengameart.org/content/universal-animation-library) | Quaternius | CC0 | `Assets/Art/Characters/Quaternius/UAL1_Standard.fbx` |

## Textures

| Asset | Author | Licence | Used in |
|---|---|---|---|
| [Wood Floor](https://polyhaven.com/a/wood_floor), [Plaster Grey 04](https://polyhaven.com/a/plaster_grey_04), [Cotton Jersey](https://polyhaven.com/a/cotton_jersey) | Poly Haven | CC0 | The bedroom render (`Assets/Resources/Room/*`) |

## Original art

The bedroom, the comic on Roshan's knees, Mom's silhouette and the door animation frames, and Roshan's hand and torch (`Assets/Resources/Room/*`) were
modelled and rendered by the developer in Blender 5.2, using
the textures above and the game's own comic cover art.

The comic's cast was made the same way: Max Voltage (built on the CC0 Quaternius UAL1 mannequin and rig, with
his extra comic moves keyframed on that rig), the five Inkies, the Smudge, Bruiser, Splotch, Dot-Shot and
Eraser, and Baron Blot, each with its own rig and clips (`Assets/Art/Characters/MaxVoltage/*`,
`Assets/Art/Characters/Inkies/*`).
The comic pages, props and HUD icons are built in code at runtime.

## Fonts

| Font | Author | Licence | Files |
|---|---|---|---|
| [Bangers](https://github.com/google/fonts/tree/main/ofl/bangers) | Vernon Adams | SIL Open Font License 1.1 | `Assets/Art/Fonts/Bangers-Regular.ttf` (+ `OFL-Bangers.txt`) |
| [Comic Neue](https://github.com/google/fonts/tree/main/ofl/comicneue) | Craig Rozynski | SIL Open Font License 1.1 | `Assets/Art/Fonts/ComicNeue-Bold.ttf` (+ `OFL-ComicNeue.txt`) |
| [Courier Prime](https://github.com/google/fonts/tree/main/ofl/courierprime) | Alan Dague-Greene | SIL Open Font License 1.1 | `Assets/Art/Fonts/CourierPrime-Bold.ttf` (+ `OFL-CourierPrime.txt`) |

Bangers and Courier Prime also letter the web build's loading page: copies sit in
`Assets/WebGLTemplates/PastBedtime/TemplateData/fonts/` with their OFL texts.

## Video

| Asset | Source | Files |
|---|---|---|
| Studio Kamikaze logo animation | Made by the developer with KlingAI 3.0 (AI-generated) | `Assets/StreamingAssets/StudioIntro.mp4` |

## Engine and tools

Unity 6 (6000.3) with the Universal Render Pipeline, the Input System, uGUI and TextMesh Pro
(TMP Essential Resources are Unity's own package files).
