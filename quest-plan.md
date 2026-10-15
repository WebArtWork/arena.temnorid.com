# Temnorid Arena on Meta Quest 3: plan

Status: **steps 1–7 and 9 done in the Editor (2026-10-15), step 8 waits for the headset.** Checked with
simulated headset and controllers; Build Quest APK works (96 MB, OpenXR loader inside, phone settings restored
after); nothing has run on a real Quest yet, and the phone APK has not been rebuilt since XR was added. Update the checkboxes as steps land.

## Goal

Temnorid Arena as an **immersive VR arena** on Quest 3: you stand in the stone arena as the mage,
move with the stick, turn with the other, and cast spells with your hands. Same arenas, rules,
spells and bots as PC and phones, and **cross-play**: a Quest player joins the same matches as PC
and phone players and is just another combatant.

Agreed (see `CLAUDE.md`, "Decisions 2026-10-15"):

| Question | Decision |
|---|---|
| Kind of Quest game | VR arena (immersive). Mixed reality in your room maybe later, through `3d-unity-spatial`. |
| XR stack | **OpenXR + XR Interaction Toolkit** (vendor-neutral). Meta-only features (passthrough, MRUK, Meta platform) only as optional extras. |
| Matches | Cross-play with PC and phones. |
| Project | The same `temnorid-arena` project; Quest is one more build target. |

Out of scope for this slice: mixed reality / room arenas, hand tracking (controllers only),
Meta Horizon Store submission, PC VR (works later almost for free thanks to OpenXR), voice chat.

## What exists already

* Quest runs Android: IL2CPP ARM64, the Android module and `adb` are installed and used for the phone APK.
* `3d-unity-input`: an **XR** scheme with controller bindings for Move, Look, Jump, Sprint, Crouch, Primary,
  Secondary, Interact and Pause; scheme detection switches to XR when a controller is used.
* `3d-unity-xr`: `XRPrimaryButtonAction`, `StatusHud` (a world-space panel that can follow the view), `XRStatus`.
* `3d-unity-character`: `CharacterMotor` driven by `CharacterCommand` (move, yaw, jump, sprint, crouch),
  the same for local, network and bot characters. `CharacterAnimator` drives the mage.
* `3d-unity-network`: `NetworkCharacter` sends `CharacterCommand`s, so any device that produces commands plays online.
* Game: `SpellCaster` (server-authoritative casting), `ArenaHud` / `ArenaMenus` (uGUI built in code),
  `ArenaCharacterVisuals` (animations, colors).

## Architecture

```text
3d-unity-input        + XR hand poses (Aim/Grip position+rotation per hand), Turn action
3d-unity-xr           + XRCharacterRig: head = camera, rig follows a CharacterMotor, room-scale offset,
                        comfort (snap/smooth turn, vignette), world-space UI pointing, XR on/off at runtime
3d-constructor-vr-controls (new)
                      XR input → CharacterCommand (stick move relative to head, turn), hand aim ray,
                        same role as 3d-constructor-character-view-controls for flat screens
temnorid-arena-spells + aim source: screen centre ray (PC/phone) or casting hand ray (VR)
temnorid-arena-hud    + VR layout: wrist panel (health, mana, spells), floating feed/death text, scoreboard
temnorid-arena-menus  + menus on a world-space panel, pointed at with the controller
temnorid-arena        + OpenXR/XRI packages, Meta Quest build target, VR player prefab, Build Quest APK
```

Dependencies stay one-way: the constructor uses `3d-unity-xr`, `3d-unity-input` and
`3d-unity-character`; game packages only use engine interfaces (an aim source, a "VR active" flag)
and never the XR SDK directly.

## Steps

Each step ends compiling with 0 errors and checked in Play mode (XR Device Simulator) before the next.

### 1. Project setup — `temnorid-arena`

- [x] Add packages from Unity's registry: `com.unity.xr.management` **4.7.0**, `com.unity.xr.openxr` **1.18.0**,
      `com.unity.xr.interaction.toolkit` **3.6.1** (its **XR Interaction Simulator** sample is imported by
      Temnorid → VR Simulator in Play Mode), and `com.unity.xr.meta-openxr` **2.6.1** (brings AR Foundation,
      Core Utils, Composition Layers).
- [x] XR Plug-in Management: Quest and phones are both the Android target, so XR is **off for every target in
      the project** and Build Quest APK assigns the OpenXR loader for Android only while it builds, then removes
      it (Build Android APK removes it too, in case a Quest build was interrupted).
- [x] OpenXR features for Quest: Meta Quest Support (system keyboard on), **Oculus Touch** and Touch Plus
      controller profiles; render mode single pass (multiview), foveated rendering (SRP foveation API).
- [x] Quest player settings (applied during the Quest build, phone values restored after): ARM64, IL2CPP,
      minimum API 32, Vulkan only, landscape left, the same app id `arena.temnorid.waw` (or
      `arena.temnorid.waw.quest` if the stores need it separate — decided when publishing).
- [x] Quality: a `Quest` quality level (`Assets/Settings/Quest_RPAsset.asset`, a copy of Mobile: MSAA 4x,
      no HDR, 20 m shadows; the XR camera has no post-processing), Android's default only during the Quest build.
- [x] `Temnorid → Build Quest APK` (like Build Android APK), output `Builds/Quest/TemnoridArena.apk`;
      `Temnorid → Set Up Quest XR` makes the settings without building.

Check: the project compiles on every target; the Windows, phone and iOS builds still start without XR.

### 2. Input — `3d-unity-input`

- [x] XR pose actions per hand: `LeftAimPosition/Rotation`, `RightAimPosition/Rotation`
      (`<XRController>{LeftHand}/pointerPosition` …), and tracking state; `HeadPosition/Rotation`
      (`<XRHMD>/centerEyePosition` …), in an always-on "XR" map. Read through `EngineInput.TryReadHandAim(hand)` /
      `TryReadHead` (straight from the bound controls, so a device reset doesn't lose them). A layout override
      lets XRI's simulated controllers answer to the aim pose paths.
- [x] `Turn` in the XR scheme on the right stick x (Look stays for flat screens). Also `NextSlot` (right
      stick click), UI `TrackedDevicePosition/Orientation` (controllers point at world-space canvases), haptics
      (`EngineInput.Haptic`).
- [x] Which hand casts: a setting (`DominantHand`, default right), saved like the other input settings; it
      swaps the XR triggers of `Primary` and `Secondary`.

Check: in the XR Device Simulator the actions report poses and stick values.

### 3. XR rig — `3d-unity-xr`

- [x] `XRCharacterRig`: builds an XR Origin (camera offset, head camera with tracked pose driver, two
      controller anchors) and keeps it on a `CharacterMotor`:
  * the rig's origin follows the motor's position; the motor's height drives the camera offset;
  * real head movement (stepping in your room) moves the character too, so walls still block you
    (the head offset is fed into the motor as movement, then the rig is re-centred; while the stick walks, the
    head moves with the body and room steps wait, held to a 0.3 m leash);
  * eye height from the headset, so short and tall players see from the mage's eyes.
- [x] Comfort: snap turn (default 30°) or smooth turn (setting), a vignette while moving (setting, default on),
      `Recenter()` (called on the first tracked frame; no button yet: the Quest's own recentre covers it).
      Extra: the view follows the body with 25 ms smoothing, because the network moves the body at 60 Hz.
- [x] World-space UI: the controller ray points at uGUI panels through the Input System's own UI module and
      `TrackedDeviceRaycaster` (no XRI interactors needed), reading `3d-unity-input`'s UI map; the rig and the
      menu camera (`XRHeadDriver`) give the module its tracking origin. Trigger clicks.
- [x] `XRStatus.IsActive`: is a headset running (start XR when the Quest build starts; off everywhere else), used
      by game code to pick VR or flat layouts. True with the Editor simulator too.
- [x] Update `StatusHud` for the new rig if needed (not needed: it follows `Camera.main`, which is the rig's camera).

Check: Device Simulator: head moves the view, the rig follows the motor, snap turn works, a test panel can be clicked.

### 4. VR controls — new `3d-constructor-vr-controls`

- [x] New package (`node tools/new-package.mjs 3d-constructor-vr-controls --asm ITKamianets.Engine.VR.Controls …`),
      namespace `ITKamianets.Engine.VR`, depends on core, input, character, xr.
- [x] `VRControls` (role of `CharacterViewControls`): left stick → `CharacterCommand.Move` relative to the head's
      facing (or the hand, a setting), yaw from the head + turns, Jump/Crouch/Sprint from the buttons,
      sprint as a toggle, crouch also by physically crouching (head height below a threshold).
- [x] `HandAim`: the aim ray of the casting hand (origin and direction), for gameplay to read, as the new
      `IAimSource` interface in `3d-unity-character`.
- [ ] Add the package to `workspace.json`, the `CLAUDE.md` map (done), the website plan and catalog (other session).

Check: Device Simulator: walk, turn, jump, crouch; commands identical in shape to the flat controls.

### 5. Game: spells — `temnorid-arena-spells`

- [x] An aim source on `SpellCaster`: screen centre ray (now) or a hand ray (VR). The server still validates
      every cast exactly as now, so cross-play stays fair.
- [x] Spell selection in VR: the four spells on the wrist (point and click; the trigger doesn't cast while
      pointing at the wrist), and the next spell on the right stick click.
- [x] Casting feedback in the hand: the selected spell's color glows at the casting hand (dim while cooling
      down); a short aim line, always shown (not only on a half press; change if it distracts on the headset).
- [x] Haptics: a short buzz on cast, a stronger one on hit taken (through `3d-unity-input`, a new
      `EngineInput.Haptic(hand, strength, seconds)`).

Check: Device Simulator: cast each spell in the hand's direction; a bot is hit; PC host sees it.

### 6. Game: HUD and menus — `temnorid-arena-hud`, `temnorid-arena-menus`

- [x] VR HUD (when `XRStatus.IsActive`): a wrist panel on the off hand (health, shield, mana, the spell bar with
      cooldowns); kill feed and "Defeated by X, respawning" floating ahead at a comfortable distance;
      scoreboard while holding Y or while dead; no crosshair (the hand aims). The floating panel is 2.4 m away
      so menus (1.6 m) sit in front of it.
- [x] Menus on a world-space panel 1.6 m ahead, pointed at with the controller ray; the name field uses
      the system keyboard (Meta Quest feature on; untested on the headset); Pause on the menu button. Settings
      in VR: turning, vignette, casting hand. All of it through `ArenaVRCanvas` on every `ArenaUi` canvas.
- [x] Flat layouts unchanged on PC and phones.

Check: Device Simulator: play a full round: menu → Play vs bots → fight → die → respawn → pause → leave.

### 7. Game: the VR player — `temnorid-arena`

- [x] One `ArenaPlayer` prefab for both (the network has one player prefab): it carries the flat controls and
      `XRCharacterRig` + `VRControls` + `HandAim`, all off; `XRModeSwitch` (its only owner-only component)
      turns on the right set on the owner's copy. Cross-play needs no new network code.
- [x] First person: the mage's head and body hidden for the local VR player (shadows kept), simple hands
      (placeholder spheres or controller shapes until hand models exist — add "VR hands" to `models-to-do.md`).
- [x] Others see your mage; its head turns toward where you look (the existing yaw), cast animations play
      as now. Later (not this slice): send head and hand poses so other players see real arm movement.
- [ ] Arena scale and comfort check: wall height, pillar size, spell speeds feel right at real scale (on the headset, step 8).

Check: Device Simulator + a PC client: both see each other, cast at each other, scores count.

### 8. On the headset

- [ ] Quest 3 in Developer Mode (Meta Horizon app → headset → Developer Mode), connected by USB-C, allowed for `adb`.
- [ ] `Temnorid → Build Quest APK`, `adb install -r`, launch, read `adb logcat` for errors.
- [ ] Performance: steady **72 Hz** (target 90): watch frame time with the Meta tools (OVR Metrics / perf
      traces); reduce draw calls (static batching of arena props), texture sizes, shadow distance if needed.
- [ ] Cross-play on the network: the Quest joins a PC host on the same Wi-Fi (`-client`), and the other way round.
- [ ] Comfort pass: turning, movement speed, vignette; nobody gets sick in a 5-minute match.

### 9. Docs

- [x] READMEs (Logic/Status) of `3d-unity-input`, `3d-unity-xr`, `3d-constructor-vr-controls`, spells, hud,
      menus; the game README ("Meta Quest" section like "Phones"); CHANGELOGs; `CLAUDE.md` map;
      workspace README build table (Quest row).

## Testing without a headset

**Temnorid → VR Simulator in Play Mode** turns on the XR Interaction Toolkit's **XR Interaction Simulator**:
Play mode starts with a simulated headset and two controllers driven by keyboard and mouse, and the game takes
its VR layout. Steps 1–7 were checked with simulated devices driven from code (the same device layouts); step 8
needs the real Quest 3.

What was checked (2026-10-15, Arena scene): the menu camera follows the head; the controller ray clicks Play vs
bots; the rig puts the view at the mage's eyes (1.70 m) and holds still to the millimetre; a 0.5 m room step is
followed by the body; real crouching crouches; snap turn turns 30° once per push; the stick walks where the head
looks; the trigger casts along the hand; right stick click and the wrist pick spells; pause, Settings (VR items
only), a toggle, Back, Resume and Leave match work; the flat camera comes back after leaving.

Not checked yet: haptics (no simulated haptics), the system keyboard, cross-play against a PC client, anything
on the headset.

## Risks and open points

| Risk | Plan |
|---|---|
| Phone APK accidentally starting XR | XR on only for the Meta Quest target; checked by building the phone APK in step 1. |
| Performance on Quest (stylized but real-time shadows, skinned mages, URP) | Quest quality level, foveated rendering, profile on device in step 8 before adding effects. |
| Fairness PC mouse vs VR hand aim | Server checks stay as they are; tune spell speeds/areas after real cross-play matches. |
| Motion sickness | Snap turn and vignette on by default; no camera shake; no forced movement. |
| Real-room movement vs walls | The head offset goes through the motor (walls block it); if your head is inside a wall, the view fades. |
| Store release (Meta Horizon Store) | Separate later task: Meta developer account, app entry, VRC checks, signing key. |

## Needed from you

* Quest 3 in Developer Mode and a USB-C cable when we reach step 8.
* Later, for hands: "VR hands" models (will be added to `models-to-do.md`).
