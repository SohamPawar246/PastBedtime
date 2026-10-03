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

Max is on the left hand, the torch on the right (the mouse).

| Action | Keyboard + mouse |
|---|---|
| Move | A / D |
| Up / down (launcher, ground slam) | W / S |
| Jump (hold for higher) | Space |
| Punch (3-hit chain: jab, cross, haymaker) | E |
| Kick (W + Q launcher, Q in the air dive kick, S + Q in the air ground slam) | Q |
| Dodge (passes through enemies) | Left Shift |
| Splash Page super | F |
| Aim the torch | Mouse |
| Beam follows Max | Hold left mouse |
| Torch on / off | Right click |
| Twist the crank (charge) | Scroll wheel (or R) |
| Twist the lens wheel | Middle click, mouse side buttons or Tab (1 to 4 pick a lens) |
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
| `Assets/Scripts/Core` | App root, scene transitions, audio, settings |
| `Assets/Scripts/UI` | Front-end screens and the comic UI kit (built in code) |
| `Assets/Scripts/Game` | The game: the light rule, the torch, Max, the Inkies and Baron Blot, Mom, the pages, the HUD, the finale |
| `Assets/Scripts/Game/Dev` | Editor-only playtest driver (scripted runs that check the rules) and screenshot capture |
| `Assets/Shaders/Comic` | The comic's printing: ink, tone, hatching, gradation and the torch's light on the page |
| `Assets/Editor` | Menu tools: *Build Shell*, *Build Comic Cast*, *Build Game Assets*, *Build Pages* |
| `Assets/Resources` | Fonts, audio, music, the room renders, the page definitions, credits text |

## Licence

Code: MIT. Third-party assets keep their own licences.
