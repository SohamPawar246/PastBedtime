# PAST BEDTIME

*It's 2 AM. Roshan is reading a comic in bed by the light of a wind-up torch, and the comic only happens where it is being read.*

**▶ Play in your browser: _itch.io link coming soon_**

A 2.5D action-platformer brawler for the **TGC Game Jam × GDAI × Infinium '26** (themes: **Comic · Twist · Light**).
The mouse is Roshan's torch and the keyboard is Max Voltage, the comic's hero. Everything inside the beam is alive;
everything outside it is frozen mid-panel. Twist the crank to keep the light alive, flip to the purple Ghost lens to
see what's drawn in invisible ink, and switch it off when Mom's door opens.

- Proposal: [proposal.pdf](proposal.pdf)

## Team

| Name | IndieConnect | Role |
|---|---|---|
| Soham Pawar (Studio Kamikaze) | @sohampawar246 | Solo developer: design, code, art, audio |

## Controls

Max is on the left hand, the torch on the right (the mouse). These are the defaults: every one of them can be
changed under **Settings > Controls** (on the title screen and in the Bookmark pause), with a main and a spare
control per action and a **DEFAULTS** button to put them back.

| Action | Keyboard + mouse |
|---|---|
| Move | A / D |
| Up / down (launcher, ground slam) | W / S |
| Jump (hold for higher) | Space |
| Punch (3-hit chain: jab, cross, haymaker) | E |
| Kick (W + Q launcher, Q in the air dive kick, S + Q in the air ground slam) | Q |
| Dodge (on the ground; passes through enemies) | Left Shift |
| Splash Page super | F |
| Aim the torch | Mouse |
| Beam follows Max | Hold left mouse |
| Torch on / off | Right click |
| Twist the crank (charge) | Scroll wheel or trackpad (or tap R) |
| Ghost lens on / off (from page 7) | Middle click, mouse side buttons or Tab (or 1 clear, 2 ghost) |
| Pause ("Bookmark") | Esc or P |

Menus also work with arrow keys + Enter. Settings include the controls, follow assist, easy suspicion,
flat page view, screen shake, reduce flashing and sound captions. Esc / P (the Bookmark) and 1 / 2 (clear / ghost
lens) stay as they are. In the browser, clicking outside the game opens the Bookmark, so Mom never catches you away.

## Features

- **The light rule.** Only what the torch lights is alive: Inkies, hazards and platforms move in the beam and freeze
  mid-panel outside it. The torch runs on a wind-up crank, so the light is something you spend.
- **Nine pages in three acts**, ending in a three-round fight with Baron Blot (his office, the ink vat, the roof)
  and a quiet twist ending in Roshan's room.
- **Brawler combat.** A three-hit punch chain, a launcher, dive kick, ground slam, a dodge that passes through
  enemies, and the **Splash Page** super, which fills the whole page with one panel.
- **Five Inkies** (Splotch, Smudge, Dot-Shot, Eraser, Bruiser), each with its own way to fight, and frozen ones become tools (a frozen ink bat is a stepping stone).
- **The purple Ghost lens** (from page 7) shows platforms and secrets drawn in invisible ink, with a short tutorial.
- **Mom.** Her footsteps and the shadow under the door are the warning; get the torch off before the door opens
  or she catches you. Suspicion builds across a page.
- **Stars and the Nightstand.** Hidden stars on the pages buy torch upgrades (Spring, Gear, Ratchet) between acts.
- **Comic feel.** Impact frames and hit-stop on big blows, SFX lettering that leans with the hit, Inkies knocked
  out toward the reader, a hurt flash and blink, squash and stretch on landing, the room darkening as Mom comes,
  and a halftone/ink shader that prints the page as you play.
- **Accessibility.** Fully rebindable controls (keyboard, mouse and trackpad), follow assist, easy suspicion, flat
  page view, screen shake and flashing toggles, and sound captions.
- **Built for the browser.** A WebGL build with a comic-style loading page; right click, wheel and side buttons
  stay inside the game.

## Developer cheats

Available in the Unity editor and in Development builds only (a release build doesn't include them). They work
while a comic page is open. Press **F1** for an on-screen list showing what's switched on, plus the current page,
torch charge, Mom's state and fps.

| Key | Cheat |
|---|---|
| F1 | Show / hide the cheat list |
| F2 | Fill the Splash meter |
| F3 | God mode (Max can't be hurt or knocked out) |
| F4 | Refill the torch (a flared bulb cools at once) |
| F5 | Endless torch (the charge never runs down) |
| F6 | Mom comes now and opens the door |
| Shift + F6 | Mom off / on for this page |
| F7 | Mom catches you (BUSTED) |
| F8 | Knock out every awake Inkie in the light (Baron Blot takes 40 per press) |
| F9 | Finish this page (its end card, the Nightstand between acts) |
| Page Up / Page Down | Jump to the next / previous page |
| F10 | Unlock the Ghost lens and add 5 stars |
| F11 | Slow motion (quarter speed) |
| F12 | Room light on / off (the whole page awake) |

In the editor, **Assets/Scripts/Game/Dev/PlaytestDriver.cs** also runs scripted playtests that check the game's
rules and take screenshots.

## Run from source

1. Install **Unity 6000.3.10f1** (Unity 6) with **WebGL Build Support**.
2. Clone the repo and open the folder in Unity Hub.
3. Open `Assets/Scenes/Boot.unity` and press **Play** (any shell scene can also be played directly).
4. If fonts or scenes are missing, run **Past Bedtime > Build Shell** from the menu bar.

## Build for the web

1. **File > Build Profiles**, switch to **Web**. The scene order is already set: Boot, StudioIntro, EpilepsyWarning,
   MouseKeyboard, Title, Game, Credits.
2. Run **Past Bedtime > Build Web (itch.io)**. It applies the release settings (Gzip with decompression fallback:
   itch.io serves gzip natively and the loader copes on hosts that don't; Code Optimization *Runtime Speed*, since
   *with LTO* links for over an hour), builds to `Builds/Web` and zips the folder's contents to
   `Builds/PastBedtime-web.zip` with `index.html` at the root.
3. The `PastBedtime` page template makes the game fill itch's frame with a comic-style loading bar, and keeps the
   mouse's right click, wheel, middle click and side buttons inside the game (no menu, no scrolling the page, no
   going back a page). *Build Shell* also gives the browser the PC quality tier.
4. Upload the zip to itch.io as an HTML5 game: "This file will be played in the browser", viewport 1280 × 720, with
   the fullscreen button on.

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
