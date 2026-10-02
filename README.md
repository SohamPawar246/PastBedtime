# PAST BEDTIME

*It's 2 AM. Roshan is reading a comic in bed by the light of a wind-up torch, and the comic only happens where it is being read.*

**▶ Play in your browser: _itch.io link coming soon_**

A 2.5D action-platformer brawler for the **TGC Game Jam × GDAI × Infinium '26** (themes: **Comic · Twist · Light**).
The mouse is Roshan's torch and the keyboard is Max Voltage, the comic's hero. Everything inside the beam is alive;
everything outside it is frozen mid-panel. Twist the crank to keep the light alive, twist the lens wheel to change
what it does, and switch it off when Mom's door opens.

- Proposal: [proposal.pdf](proposal.pdf)

## Team

| Name | IndieConnect | Role |
|---|---|---|
| Soham Pawar (Studio Kamikaze) | @sohampawar246 | Solo developer: design, code, art, audio |

## Controls

| Action | Keyboard + mouse |
|---|---|
| Move | A / D |
| Look up / drop through | W / S |
| Jump (hold for higher) | Space |
| Punch (3-hit chain) | J |
| Kick (W+K launcher) | K |
| Dodge | Left Shift |
| Splash Page super | U |
| Aim the torch | Mouse |
| Beam follows Max | Hold left mouse |
| Torch on / off | Right click |
| Twist the crank (charge) | Scroll wheel |
| Twist the lens wheel | Q / E |
| Pause ("Bookmark") | Esc or P |

Menus also work with arrow keys + Enter. Settings include follow assist, twist input, easy suspicion,
flat page view, screen shake, reduce flashing and sound captions.

## Run from source

1. Install **Unity 6000.3.10f1** (Unity 6) with **WebGL Build Support**.
2. Clone the repo and open the folder in Unity Hub.
3. Open `Assets/Scenes/Boot.unity` and press **Play** (any shell scene can also be played directly).
4. If fonts or scenes are missing, run **Past Bedtime > Build Shell** from the menu bar.

## Build for the web

1. **File > Build Profiles**, switch to **Web**.
2. The scene order is already set: Boot, StudioIntro, EpilepsyWarning, Title, Game, Credits.
3. Compression is Brotli with decompression fallback (set by *Build Shell*), which itch.io serves correctly.
4. Build, zip the output folder and upload it to itch.io as an HTML5 game.

## Project layout

| Path | What |
|---|---|
| `Assets/Scripts/Core` | App root, scene transitions, audio, settings, input |
| `Assets/Scripts/UI` | Front-end screens and the comic UI kit (built in code) |
| `Assets/Shaders` | `LightMask`: torch beam with a Ben-Day halftone edge |
| `Assets/Editor` | `ShellSetup` (scene/font generation), `ShellCapture` (dev screenshots) |
| `Assets/Resources` | Fonts, audio, music, credits text |

## Licence

Code: MIT. Third-party assets keep their own licences.
