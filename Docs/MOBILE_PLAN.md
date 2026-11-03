# Past Bedtime: mobile redesign plan (Android first)

Past Bedtime was designed for two hands on a keyboard and mouse: the left hand is Max, the right hand is Roshan's
torch. A phone has two thumbs, a small screen they cover, no hover, no right click, short interrupted sessions,
often no sound, and it's held in bed in the dark, which suits this game better than any desk.
This plan redesigns the game for that, piece by piece. It doesn't just port the PC version.

Written 2 Nov 2026. Evidence: the first device test (Unity Remote), screenshots at phone (20:9) and tablet (4:3)
shapes, and a read through every screen and system.

**Ground rules**
- One project, three **form factors**: Desktop (today's game, unchanged), Phone, Tablet. Every change is behind
  the form factor, so PC and web stay exactly as they are (the PC playtest suite runs after every milestone).
- Touch sizes are set in **millimetres**, never in screen pixels.
- The comic look, the bedroom, the caption-box menus and the light rule stay. They're reworked for thumbs, not
  replaced.
- Nothing is called done until it has been tried on a real phone.

Legend: ✅ done or decided · 🔨 to build · 🧪 decide by playtest

---

## 1. What a phone changes, and what that means for this game

| On a phone… | …so in Past Bedtime |
|---|---|
| **Two thumbs, both busy.** Movement takes one, actions the other. | The torch loses its own hand. It needs a new home: a smarter beam, gestures on the torch itself, and puzzles that still work (§3). |
| **Thumbs cover the bottom corners** (≈ 25% of the screen each). | Max, enemies, hazards and the HUD must stay out of the bottom corners: the camera keeps Max near the middle, the HUD lives at the top. |
| **Small screen** (6.5" ≈ 15 × 7 cm; 1 mm ≈ 16 px). | Today Max is ≈ 6 mm tall and captions ≈ 1 mm: the comic must fill the screen and the camera must come closer. |
| **Every screen shape** (16:9 to 21:9 phones, 16:10 and 4:3 tablets, foldables) and notches. | The bedroom is one 16:9 picture scaled to fill: the torch hand, Mom's door and the comic's edges get cut off (screenshots, §2). |
| **No hover, fat fingers.** | Every menu target ≥ 8 mm with ≥ 2 mm gaps, a pressed state instead of a hover state, nothing that needs a right click, a scroll wheel or a key. |
| **Short, interrupted sessions.** The system kills apps in the background. | Resume exactly where you were (panel checkpoint saved to disk), pages as bite-sized chapters, a contents page to replay pages for stars. |
| **Often muted, phone speakers.** | Mom's warnings and every audio cue need a visual and a **haptic** (feel her footsteps). Low bass (the heartbeat) doesn't play on phone speakers. |
| **Played in bed, in the dark, screen dimmed.** | The theme for free, but a dark game on a dimmed screen can disappear: brightness/gamma setting, check the darkest pages. |
| **Fingers are less precise than keys.** | Phone tuning: more jump buffer and coyote time, bigger hit areas, telegraphs that read at small size; difficulty checked by playtest. |
| **Phones heat up and run on battery.** | Steady 60 fps on mid-range phones, a 30 fps battery saver, quality tiers. |

---

## 2. What's wrong today (screenshots, 1 Nov)

| | 20:9 phone | 4:3 tablet |
|---|---|---|
| Torch hand | Almost entirely off the bottom; the crank can't be seen or circled. | Half off the right edge; crank off-screen. |
| Mom's door | Visible; the slippers under it are cut off. | **Off-screen entirely.** |
| The comic | Bottom strip and folio cut off. | Right edge cut off; the star counter reads "0/". |
| Size | Max ≈ 6 mm, caption lettering ≈ 1 mm (needs ≥ 2 mm). | Fine. |
| Title menu | Boxes ≈ 5 mm tall, ≈ 1 mm apart. | OK. |
| Settings | Rows ≈ 3 mm apart, slider cells 2 mm. | Small. |
| On-screen buttons | 8–13 mm (OK), but over the right page and its star counter. | Oversized (sized in screen units). |
| Wording | "PRESS ANY KEY", "ESC RESUME ←/→", "ANY KEY BACK TO THE MENU", the mouse-and-keyboard card, How to Read. | Same. |
| Dead settings | "Flat page view" and "Follow assist" are saved but nothing reads them. | |

---

## 3. The core redesign: the torch on a phone

On PC the fun is doing two jobs at once: fight with the left hand while the right hand chooses what's lit.
On a phone both thumbs are Max's, so the torch has to become **mostly automatic, with deliberate moments
where you take hold of it**.

### 3.1 Control model: A (decided 2 Nov)

| | How it plays | For | Against |
|---|---|---|---|
| **A. Stick + buttons, smart torch** *(prototyped)* | Left thumb: floating stick. Right thumb: JUMP / POW / KICK / DODGE. The beam follows Max by itself; you take the torch when you need it: drag the page to aim, tap the torch to switch it off, circle the crank to wind it. | Familiar to every mobile player; keeps the full moveset; already working. | The torch is "assisted" most of the time; the two-jobs-at-once tension is weaker. |
| **B. Left thumb is Max, right thumb is the torch** | Left half: stick plus gestures for Max (tap = punch, flick up = jump, flick down = slam, double-tap = dodge). Right half: the torch, always (drag to aim, tap off, circle to wind). | True to the game's idea (one thumb per character). | Gesture combat on the same thumb as the stick is hard; big redesign; risky. |
| **C. A with B as an option** | A by default; "Torch thumb" layout in the touch settings. | Keeps the door open. | Two schemes to test and balance. |

**Decided: A**, built so that the torch moments feel deliberate and good (below). Keep B as a later
experiment if A's playtests say the torch has become boring.

### 3.2 The smart beam (whatever the model)

- 🔨 **Follow with an offset you set.** The beam follows Max; dragging on the page moves the beam *relative to Max*,
  and it **keeps that offset** as Max moves (lean it ahead, behind, above). A double-tap on the page, or a few
  seconds without touching, brings it back to centre. This is what keeps the "leave it dark" puzzles possible:
  freeze a bat beside Max and jump on it, keep a press dark above him.
- 🔨 The lit spot sits a little above the fingertip, so the finger doesn't hide what it's lighting.
- 🧪 Return speed, how long the offset holds, and whether a release parks the beam or lets it drift back.
- 🔨 Optional **gyro nudge**: tilt the phone to push the beam (off by default; it's a great "the phone is the
  torch" moment in bed).
- 🔨 **Puzzle audit, page by page** (the most important gameplay task): play every puzzle and fight with the touch
  controls and the smart beam, and list where it breaks. Known risks:
  - **Page 2:** frozen bats as stepping stones.
  - **Pages 4–5:** presses frozen high or low; conveyor motors kept dark.
  - **Page 7:** invisible-ink ledges under the Ghost lens.
  - **Mom's wedge** waking crowds.
  - **The Blot fights.**

  Each problem gets one of three fixes: the offset is enough; a light assist (the beam leans away from a hazard it
  would wake); or a small change to the puzzle on phones.

### 3.3 Winding the crank

On PC you scroll while still fighting. On a phone, winding means the right thumb leaves the buttons and Max
stands still. Today a refill is ≈ 30 twists.
- 🔨 Accept three gestures at once: **circle** round the crank (taught), **swipe** back and forth on the torch,
  **fast taps** on it.
- 🧪 **Touch charge economy:** more charge per full circle and/or slower drain on phones, so winding happens in the
  pauses between fights, not in the middle of them. Tune by playtest; the PC economy stays.
- 🔨 The crank is **big and right under the right thumb** (the torch hand moves into the right margin, §5).
- 🔨 Every notch ticks (sound + haptic); the overwind ratchet buzzes harder; a flare is a heavy pulse.

### 3.4 Mom on a phone

Her visit is the game's tension. Today it relies on sound (footsteps, heartbeat) plus the light under her door.
- 🔨 **Feel her coming**: each footstep is a haptic tap, getting stronger; the heartbeat becomes a pulse (phone
  speakers can't play it).
- 🔨 Stronger visuals for muted play: the screen's left edge warms with the hallway light, the door in the margin is
  never covered by a thumb, the torch in the hand shakes when it must go off.
- 🔨 **OFF must never miss**: tapping anywhere on the torch hand switches it off (a big target), and the "OFF!" hint
  points at it.
- 🧪 Approach time on phones: the same 4 s (6 s in Act 1)? Check it's fair with a thumb on the stick.

### 3.5 Combat and platforming on glass

- 🔨 **Buttons appear as moves are taught.** The pages already teach the moves one by one in their captions; in code
  every move works from page 1. On phones each button arrives when its page first teaches it: page 1 starts with
  the stick and JUMP, then POW with the first Inkie; the launcher on page 2, the slam on page 4, SPLASH! on page 5,
  LENS on page 7. Each arrives with a stamp ("NEW MOVE: KICK!"), and they stay once learned (saved). Fewer buttons
  early, less clutter, a tutorial for free.
- 🔨 Moves that need a direction + a button (launcher = up + KICK, slam = down + KICK in the air) also work as a
  **swipe on the KICK button** (up = launcher, down = slam), because the stick's up/down is easy to miss.
- 🔨 **Slide between buttons** (roll from POW onto JUMP), **hold POW** to keep chaining.
- 🔨 **Phone tuning profile**: jump buffer 0.1 → ~0.15 s, coyote 0.1 → ~0.15 s, a little ledge forgiveness, slightly
  longer enemy wind-ups. 🧪 by playtest, never touching the PC numbers.
- 🔨 **Telegraphs that read at 6 mm**: enemy wind-ups get a clear mark (the manga "!" exists), projectiles get
  outlines.

---

## 4. Screen layout for thumbs

### 4.1 Phone, in a page (20:9)

```
┌───────┬──────────────────────────────────────────────────────────┬────────┐
│ MOM'S │ ♥♥♥♥♥ ▬▬▬splash           (no HUD here: top centre)  ★ 2/5 │  ▮ ⏸   │
│ DOOR  │┌────────────────────────────────────────────────────────┐│bookmark│
│  ▒▒   ││ caption ▢                                              ││        │
│  ▒▒   ││                                                        ││ [LENS] │
│ light ││            Max is kept in the middle third             ││        │
│ under ││                        ☺                               ││ (DODGE)│
│ door  ││ ────────────────── the lane ────────────────────────── ││  (KICK)│
│ ✕✕✕   │└────────────────────────────────────────────────────────┘│(POW)(JUMP)
│strikes│   ◯ stick (anywhere in the left 40%)                     │ TORCH+ │
│       │                                                          │ CRANK  │
└───────┴──────────────────────────────────────────────────────────┴────────┘
```

- **The comic faces you straight on and fills the height** (this becomes "Flat page view", on by default on
  phones). The 16:9 spread leaves margins on a 20:9 screen: **Mom's door in the left margin, the torch hand and
  crank in the right margin**, so neither is ever cropped or covered.
- 🧪 **Closer page camera** (the comic camera zooms in ~15–25% and follows Max, as it already does with a dead
  zone), so Max and captions grow and Max stays out of the thumb corners. Prototype 1.0 / 0.85 / 0.75; decide by
  feel.
- HUD at the top only: hearts and Splash top-left, stars top-right, bookmark in the right margin's top. Mom's
  strikes move up beside her door (the left thumb would cover them at the bottom).
- Buttons ≈ 55% opacity until pressed; sized in mm: JUMP 14, POW/KICK 11, DODGE/LENS 10, ≥ 2 mm apart, the hit
  area 20% bigger than drawn.

### 4.2 Tablet

The room view stays (there's space): the bedroom re-rendered with spare picture round the edges so 4:3 and 16:10
show the door and the hand. Buttons in mm (so they're not huge), the comic a little smaller than the screen.

### 4.3 All screen shapes

- 🔨 Re-render the bedroom plates in Blender **with bleed** (decided): same camera, bigger frame (≈ 2520 × 1440 around the
  same centre), so 21:9 phones and 4:3 tablets find picture past the edges. Code positions stay valid. Fallback
  without art: fade the edges to black (it's a dark bedroom).
- 🔨 Draw under notches and camera holes; keep every control and the HUD inside the safe area.
- 🔨 Recheck the full-screen effects (the iris, vignette, Mom's light, the flare, the Splash Page) at 21:9 and 4:3.

---

## 5. Menus and screens, redesigned

Thumb reach in landscape: the bottom corners and the middle are easy, the top centre is a stretch. The main thing
on each screen goes where the right thumb rests; nothing important hides top-centre.

### 5.1 First launch → first page (fewer screens)

Today: studio intro → photosensitivity warning → mouse-and-keyboard card → title (with a ~25 s opening) → page.
On a phone:
1. Studio intro (tap to skip after the first launch).
2. Photosensitivity warning: "TAP TO CONTINUE" (stays once per launch).
3. **"How to hold it" card** (first launch only; replaces the mouse-and-keyboard card): a phone drawn in the comic
   style, both thumbs, "LEFT THUMB RUNS · RIGHT THUMB FIGHTS · THE TORCH IS IN ROSHAN'S HAND". "Headphones help."
   Plus a **brightness check**: "turn it up until you can just see the bat" (the game is dark; phones get dimmed
   at night).
4. Title: the opening plays once (skippable with a tap).

### 5.2 Title

```
┌────────────────────────────────────────────────────────────────────┐
│ door           window                    PAST BEDTIME              │
│                                   2 AM. The comic only happens...  │
│            ┌────────────┐                                          │
│            │ MAX VOLTAGE│        ┌───────────────────────────┐     │
│            │    #1      │        │   CONTINUE · PAGE 4       │     │
│            │ tap to read│        └───────────────────────────┘     │
│            │            │        [  PAGES  ]  [ SETTINGS ]         │
│            └────────────┘        [HOW TO READ] [ CREDITS ]         │
│ clock                                                  torch hand  │
└────────────────────────────────────────────────────────────────────┘
```
- **Tap the comic to read**: the cover on the bed is the big play button (the menu's CONTINUE does the same).
- Main entry big (≥ 12 mm tall), the rest in a 2 × 2 grid of caption boxes (≥ 9 mm).
- **PAGES** (new): the contents page (§5.6).
- The torch beam follows your finger round the room like it follows the mouse.
- No QUIT on Android; Back on the title asks "STOP READING?".

### 5.3 Settings

Today: one dense typewritten sheet, 11 lines, 3 mm apart. On phones:
- The same paper and typewriter, **split into tabs**: SOUND · READING · COMFORT · TOUCH (stamped like index tabs).
- Each line is a full-width tap target ≥ 9 mm; toggles become a big [ ON ] / [ OFF ] stamp; sliders take a **drag**;
  choices take a tap on the one you want (no cycling).
- Phone-only lines: **Text size** (Normal / Large / Huge; Large default on phones), **Brightness**, **Haptics**,
  **Battery saver** (30 fps), **Touch layout ›**. Hidden on phones: Fullscreen, key rebinding.
- **Assists** grouped and explained: follow beam, easy suspicion, easy torch (slower drain), bigger buttons.

### 5.4 Touch layout page (replaces the key-rebinding page on phones)

Live preview of the game screen: drag any button where you like, button size and opacity sliders, left-handed
(stick right, buttons left), crank style, aim style, gyro on/off, RESET. The key-rebinding page comes back when a
keyboard is in use.

### 5.5 Bookmark (pause)

Today: the book cover, four boxes and the full settings sheet side by side. Too cramped for fingers.
On phones: big **RESUME** under the right thumb, RESTART PAGE / HOW TO READ / CONTENTS / QUIT TO TITLE as boxes,
**SETTINGS** opens the tabbed sheet. Android Back resumes. The footer "ESC RESUME ←/→" goes.

### 5.6 Contents page (new)

The comic's contents page: the 9 pages as panels with their titles, stars found per page (★★★☆☆), locked pages
sketched in pencil. Tap a read page to replay it for missing stars. Fits phone habits: short replayable chunks,
completion you can see.

### 5.7 Nightstand

Three big upgrade cards (SPRING / GEAR / RATCHET) in a row, the price stamped on each, tap to buy (a stamp lands),
KEEP READING big under the right thumb. Check ≥ 9 mm for everything.

### 5.8 How to Read

On phones, a drawn phone screen with callouts on each zone (stick, buttons as they're unlocked, torch tap, crank
circle, lens, aim drag, bookmark). The PC page stays for PC.

### 5.9 Credits

"TAP TO GO BACK"; drag to scroll; Back leaves.

### 5.10 Everywhere

- **Pressed state, not hover**: a box lights while touched, and nothing stays yellow after a tap. No first-row
  highlight when a page opens by touch.
- Every page has a visible BACK box; Android Back closes the top page.
- A short haptic tick on every press (setting).
- Text minimums on phones: captions and body ≥ 2 mm cap height, buttons ≥ 3 mm. Courier at small size is thin:
  use its bold.
- **Last input wins**: a keyboard, mouse or gamepad on a Chromebook or phone puts the key names and the PC pages
  back; touching the screen brings the touch UI back.

---

## 6. Reading and learning on a phone

- 🔨 **Captions and balloons at Large** text on phones; the page layout keeps them apart and inside their panels
  (`CaptionInView` already slides them along).
- 🔨 **Tutorials show the control**: when a caption teaches a move, its button pulses and the others dim; page 1
  opens with a ghost thumb showing the stick, then JUMP. The crank lesson (page 2 starts nearly empty) shows a
  finger circling the crank. The Ghost lens lesson pulses LENS. Mom's first visit pulses the torch with "OFF!".
- 🔨 **Every caption checked in touch mode**: a playtest scenario reads all 9 pages' captions with touch on and logs
  them, so no "PRESS SPACE" or "CLICK" slips through.

---

## 7. Sessions, saves and interruptions

- 🔨 **Resume where you were**: the panel checkpoint (page, panel, hearts, charge, Splash, stars in hand) is
  written to the save at every checkpoint and when the app goes to the background. CONTINUE drops you back into
  that panel. Today only the page reached is saved.
- 🔨 A call, the home button or the notification shade bookmarks the page (✅ done); audio focus pauses the music.
- 🔨 The screen never dims while a page is being read (cutscenes have no touches); back to normal in menus.
- 🔨 Page cards show progress ("PAGE 4 · 2/5 ★"), so a 5-minute sitting has a clear goal.

---

## 8. Sound, haptics, comfort, accessibility

| | |
|---|---|
| Phone speakers | Re-check the mix on a phone speaker: music, footsteps, ratchet, hits; anything that lives only in the bass gets a mid layer or a haptic. |
| Haptics | Crank notches, overwind, flare, landing heavy hits, taking a hit, Mom's footsteps and heartbeat, menu taps. One setting, three strengths. |
| Captions for sound | On by default on phones (muted play). |
| Brightness | Setting + first-launch check (§5.1). |
| Kept | Reduce flashing, screen shake, easy suspicion. |
| Added | Text size, haptics, easy torch, button size/opacity, left-handed, gyro, battery saver. |

---

## 9. Android plumbing

| Item | Note |
|---|---|
| Back button | Arrives as Esc (✅). Closes the top page; on the title, "STOP READING?" then quit. |
| Edge gestures | Android's back swipe from the screen edge vs the stick: the stick zone starts 6% in; test immersive mode; if needed exclude the stick strip with `setSystemGestureExclusionRects`. |
| Studio intro video | Played from StreamingAssets by URL; inside an APK that's a `jar:` path: test, else make it a VideoClip. |
| Icon / splash | Adaptive icon (foreground + background), round and legacy; plain ink-black splash (Unity 6 lets the Unity splash go). |
| API levels | Minimum Android 7 (API 24); target the newest installed. |
| Signing | Upload keystore outside the repo, backed up; Play App Signing. |
| Permissions | Vibrate only. |
| Size | AAB base under 200 MB: room plates ASTC, music Vorbis streaming. |
| Saves | PlayerPrefs; Android auto-backup restores them on a new phone. |
| Frame rate | 60 cap (✅); 30 with battery saver. |

## 10. Performance and heat

- Profile a development build on a mid-range phone: the comic camera's 1600 × 900 texture with MSAA, the
  full-screen light mask and halftone, UI overdraw, the room plates.
- Quality tiers by device: page texture 1280 × 720 and MSAA 2× on weak GPUs; fewer ink particles; identical look on
  good phones.
- Ten-minute heat test (phones slow down when hot); no per-frame garbage; Vulkan and OpenGL ES 3 both tested.

## 11. Testing

| What | How |
|---|---|
| Screen shapes | Device Simulator + a scripted screenshot sweep of every screen at 16:9, 19.5:9, 20:9, 21:9, 16:10, 4:3. |
| Touch play | `PlaytestDriver` scenarios with simulated touches: run, jump, button combos, swipe moves, crank gestures, torch off at Mom, lens, aim offset, bookmark. |
| Puzzle audit | Every puzzle and boss phase played on a phone with the smart beam (§3.2). |
| PC | The existing suite with touch off, after every milestone. |
| Devices | Your phone each milestone; before release one low-end (≈ 3 GB, Mali-G52 class), one mid-range, one tablet. Play Console's pre-launch report for crashes. |
| People | 3–5 people who've never seen it, phone in hand, no explanation. Where do they get stuck, what do they miss, when do they put it down? |

## 12. Release: Google Play only (decided 2 Nov)

The Play closed test is the longest wait in the whole plan, so it runs **alongside** the redesign, not after it.

1. **Now:** create the Play developer account (personal accounts need identity and device verification, which
   takes days). Make the upload keystore and back it up outside the repo.
2. **As soon as R2 is playable on a phone:** internal test track (instant, up to 100 testers): your own devices
   plus friends.
3. **Then the closed test: 12+ testers who stay opted in for 14 days in a row** (required before a new personal
   account can publish). Every later build goes to the same track, so the 14 days count while R3–R8 are built.
   Line up the 12+ people early (friends, the jam's community, Discord).
4. **Store listing** (built during R8): app icon 512 px, feature graphic 1024 × 500, phone and 7"/10" tablet
   screenshots (from real devices, showing the new phone framing), short and full description, a short trailer
   (optional).
5. **Forms:** content rating questionnaire (cartoon violence), privacy policy URL (one page: "the game collects
   nothing"), Data safety ("no data collected or shared"), target audience 13+ (under-13 brings the Families
   policy), ads: none.
6. **Production** once the closed test has run its 14 days and R9 is signed off; staged rollout (20% → 100%).
7. Play's **pre-launch report** runs every uploaded build on a set of real devices for free: read it each time.

Free, no ads, no purchases.

---

## 13. Build order

Each step: build → try on the phone → PC suite → commit (you).

**Progress (3 Nov):**
- **R0 ✅** `Form` (Desktop / Phone / Tablet from the screen's real size; millimetre sizing); last input wins
  (`GameInput.Poll`: touching the screen is touch mode, a key or click on a Chromebook/keyboard is keys mode;
  Esc doesn't count, it's Android's Back). *Not yet:* per-prefab phone layouts (comes with R4's menus).
- **R1 ✅** Phone framing: Flat page view now works (it was saved but did nothing) and is on by default on phones.
  The comic faces you straight on and fills the safe height, with Mom's door in the left margin.
  - The torch hand slides in to keep its crank 11 mm inside the corner (at 80% size on phones).
  - Mom's slippers move under her door, and her captions and footprints stay on screen.
  - The bookmark is a red ribbon hanging from the comic's spine.
  - Tablets show the whole bedroom (contain).
  - The bedroom was re-rendered with bleed (`room_bleed.py`, 2520 × 1440): the old plates are pixel-identical
    inside the 16:9 frame, and the room beam is measured in frame heights.
  - The closer page camera is on for phones at 0.85, keeping the top (HUD and captions) and cropping only the
    floor strip.
  - Desktop is unchanged.
- **R2 (mostly) ✅** Buttons in mm (JUMP 14, POW/KICK 12, DODGE/LENS 10) in an arc above the hand, 60% opacity at
  rest; 25% hit slack, a gap goes to the nearer button.
  - KICK from page 2, LENS once the Ghost lens is found (needs the LENS button added to the prefab), SPLASH! from
    page 5.
  - Swipe on KICK: up is the launcher, down the slam. A thumb rolls between buttons. Holding POW keeps punching.
  - The beam keeps its offset beside Max for 3 s after a drag; a double tap recentres it.
  - Haptics: crank notches, overwind, flare, blows, hits, Mom's footsteps and heartbeat, being caught, menu taps.
  - Jump buffer and coyote time are ×1.5 in touch mode.
  - *Not yet:* the touch charge economy and the puzzle audit (R3).
- **R2 finish (3 Nov) ✅**
  - The LENS button is in the prefab (Ghost-lens purple), and each button keeps its own resting colour.
  - Settings > TOUCH: button size (small/normal/big), button opacity, left-handed (mirrored buttons, the stick on
    the right), haptics.
  - Simulated-touch test (`TouchPlaytest.Run()`) passes: stick, JUMP, KICK and its swipe-up launcher, holding POW
    for the chain, rolling JUMP onto POW, the beam's held offset and double tap, a tap on the torch.
- **R4 (most) ✅** Menus for fingers, desktop unchanged (checked side by side):
  - **Title:** 8 mm boxes in a column that fits under the tagline; no QUIT on touch devices (Back twice quits);
    tap the comic to read; the beam follows a finger.
  - **Settings:** tabs on touch (SOUND / READING / COMFORT / TOUCH), one section at a time, lines 80 apart, on a
    card that `PhoneFit` blows up. Key bindings and Fullscreen are hidden. Sliders drag; a choice line takes a tap
    on the word.
  - **Pause:** big centred boxes with a SETTINGS box; the sheet is its own page with BACK; the keyboard hint goes.
  - **How to Read:** touch rows and headings. The Nightstand fills the screen.
  - **Everywhere:** no sticky highlight after a tap, no auto-selected first line on touch, a pressed state while
    touched, a haptic tick on every press. "PRESS ANY KEY" → "TAP TO CONTINUE" (warning, credits) via `TouchText`.
  - The bookmark ribbon tucks up while a lesson or warning is printed across the top of the page.
- **R5/R6 (part) ✅**
  - Caption audit in touch mode: five lines reworded (crank, launcher and slam as swipes, Max's lines).
  - Resume mid-page: the panel is saved as Max walks in (touch devices); CONTINUE goes back to it.
  - The screen stays awake during a page; the mouse-and-keyboard card is skipped on touch devices.
- **R5/R6 more (3 Nov) ✅**
  - **Contents page** (`Art/UI/Contents.prefab`, `ContentsPanel`): on a touch device the title's START OVER is
    CONTENTS. It shows the nine pages with titles and stars found; tap a read page to replay it. START OVER lives
    there and wants a second tap.
  - **"How to hold it":** a touch device's first launch shows How to Read in its touch version with GOT IT (once per
    install), in place of the mouse-and-keyboard card.
  - **Tilt to aim** (Settings > TOUCH, off by default): tipping the phone leans the following beam; it re-centres
    slowly to how the phone's held. *Axes and strength need checking on a real phone.*
- **Still to do:** brightness check; caption text size; puzzle audit + touch charge economy (R3, needs play on a
  phone); performance on a device (R8); icon; signing; store.

| # | Step | What it delivers | Size |
|---|---|---|---|
| R0 | **Foundations** | Form factor (Phone/Tablet/Desktop from screen size and touch); mm sizing helper; per-form-factor layout overrides on the UI prefabs (one prefab, a Phone layout stored beside the Desktop one); last-input-wins switching. | 1–2 d |
| R1 | **Framing** | Phone framing (comic straight on, door left margin, torch right margin), HUD rearranged, safe area; bedroom re-render with bleed; tablet view; closer-camera prototype. | 3 d |
| R2 | **Touch play v2** | Controls in mm, buttons that appear as moves are taught, swipe moves on KICK, slide/hold, crank gestures, smart beam with offset, phone tuning profile, haptics. | 3 d |
| R3 | **Puzzle audit + fixes** | Every page played on the phone; assists or phone tweaks where the beam breaks a puzzle; touch charge economy; Mom on a phone. | 2–3 d |
| R4 | **Menus** | Title (tap the comic, grid), tabbed Settings, Touch layout page, Bookmark, Nightstand cards, Credits, pressed states, Back everywhere. | 3 d |
| R5 | **First minutes** | How-to-hold card, brightness check, touch How to Read, tutorials that pulse the control, caption text size, caption audit. | 2 d |
| R6 | **Sessions** | Resume mid-page, Contents page, screen-sleep rule, progress on page cards. | 1–2 d |
| R7 | **Feel + comfort** | Phone-speaker mix, haptics everywhere, captions default, gyro option, accessibility settings. | 1–2 d |
| R8 | **Android + performance** | Back/gestures, video, icon, splash, signing, quality tiers, heat test. | 2 d |
| R9 | **Test + ship** | Screenshot sweep, touch scenarios, device matrix, people tests; store listing; production release on Play. | 2 d |
| ‖ | **Play track, in parallel** | Developer account + keystore now; internal test from R2; closed test (12+ testers, 14 days) running through R3–R8. | 14 d, overlapping |

---

## 14. Decisions (2 Nov)

| # | Question | Decided |
|---|---|---|
| D1 ✅ | Control model (§3.1) | **A: stick + buttons, smart torch** (follow with an offset you set by dragging) |
| D2 ✅ | Phone framing (§4.1) | **Comic straight on, filling the height; door in the left margin, torch and crank in the right**; closer page camera decided by prototype |
| D3 ✅ | Bedroom re-render with bleed (§4.3) | **Yes**, in Blender |
| D4 ✅ | Where it ships (§12) | **Google Play only** (closed test runs in parallel) |
| D5 ✅ | Phone extras | **All four:** resume mid-page (§7), Contents page (§5.6), haptics (§8), gyro torch option (§3.2) |
| D6 🧪 | Smart beam details, touch charge economy, phone tuning numbers, Mom's approach time | By playtest on the phone |
