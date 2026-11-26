# MMElvesWorkshop — Claude Code guide

Unity 6000.3.9f1 project in C#, rendered with URP. It is built by Pleyo for an international team, on the
`com.pleyo.template` package.

Shared Pleyo knowledge (language, player tracking and coordinate spaces, the read-only template and its API, coding
conventions, Unity CLI workflow) is in `CLAUDE_PLEYO_TEMPLATE.md`, imported here. That file is kept in sync by the
template at each Editor load: never edit it here. This file only holds what is specific to MMElvesWorkshop.

@CLAUDE_PLEYO_TEMPLATE.md

## Game

Elves' Workshop: MM co-op, 1-8 players on 1-4 screens, 2 min (10 s tutorial + 3 s countdown + 120 s). One assembly
line across all screens (one 3D scene, one camera per screen side by side): Carve, Paint, Wrap, Load the sleigh.
A team = a step (Carve / Paint / Wrap / Load). Since 2026-11-24 each Carve / Paint / Wrap player has his own machine
(ElfTeam.Machines; 2 players = lanes 0 / 1, each owning every other toy, shown by a colored ring under the toy only on that player's screen; a
player may process the other lane's toy only while its owner is inactive); any landing fires his machine. The claw (Load) too: one claw per player (2026-11-25).
(older note: the claw was one synced machine: all its active players had to land within 0.5 s (any landing counts;
no high-jump condition, decided with the user on 2026-11-24). A toy passing a station unprocessed is "unfinished" for
good once it passed the last machine of its step. Score = finished gifts in the sleigh; unfinished gifts loaded = fun
stat. Crown = team with most toys. More toys when steps have 2 machines (`ElfGame.SpawnFactor`). No robots: an absent
player's machine runs on its own (own lane only), only his "Jump to play" pill shows.

- Maps (`ElfPath`): the gameplay is 1D (distance s along the belt; screen k = s in [k x 20, (k + 1) x 20)); `ElfPath.At(s)`
  gives the belt frame (toys, machines, sleighs, boards, bin, cameras go through it). 1 screen = Straight map
  (`---- Workshop ----`); 2-4 screens = `ElfGame._multiLayout`: Ring (`---- Workshop Ring ----`, round hall around a big
  tree, cameras outside looking in: `_ringCamera`) or Terraces (`---- Workshop Terraces ----`, one row per screen, 6 m
  higher and 16 m deeper each, ramps between rows, each camera between its row and the previous one:
  `_terraceCamera`). The curved path follows the map's `Belt` object (position + yaw, `ElfPath.Origin`): move the Belt
  with its tiles to move the gameplay and cameras. Menus `Elves Workshop/Build Ring Map / Build Terraces Map In Open Scene` rebuild them (edits
  lost; tile distances in `ElfWorkshop.TileS`). The curved maps are inactive in the editor; ElfGame shows the right
  root. Sleighs ignore the straight map's sleigh height on the curved maps (flat floor).
- Station layout per screen count: see the `ElfGame` class summary. A station with no active player is run by a bot
  (no character, only the absent players' "Jump to play" pills); the claw loads on its own with 3 screens, and with 1 screen / 3
  players. Each screen holds 20 m of the line, 25 m for 1 screen / 3 players (4 stations). Camera of each screen:
  `ElfGame` fields `_cameraHeight` / `_cameraZ` / `_cameraPitch` (set by the user on 2026-11-24: 8.6 / -19.18 / 10.9),
  x = center of its screen. Belt height = top of the belt tiles as placed in the scene (the belt can be moved).
  Invisible close cross: mouse click in the top right 100 x 100 px of the window (`ElfGame.Update`).
- Toy spacing = 2-5 beats of the config BPM, slower when players bounce slower.
- Code: `Assets/Pleyo/ElvesGame/Scripts`. `GameScene` holds `ElfGame` (field `_assets` = `ElfAssets.asset`); the
  template split screen is disabled there (the game has its own cameras / HUD).
- Editable scene: `GameScene/---- Workshop ----` (`ElfWorkshop`) holds the decor for 4 screens (x 0..80): Furniture
  Cute floor (Wood_4) and walls (Gradient_Red), Christmas Pack decor, EKstudio conveyor tiles (5 m, the line of N
  screens ends at N x 20 - 2; tiles past it are hidden at runtime), spawner machine (EKstudio Machn_2) and
  `SpawnPoint`. It is an instance of `Prefabs/Workshop.prefab` (the developer's edited version): `Elves Workshop/Build Workshop In Open Scene` puts that prefab back, `Save Workshop As Prefab` (or Overrides > Apply All) stores scene edits in it.
- Machines in the scene: `GameScene/---- Machines ----` holds prefab instances placed for 4 screens: `Carve`, `Paint`,
  `Wrap` (child `Machine`), `Load` (children `ClawMachine`, `Sleigh`, `Label` anchor) and `Bin`. At runtime each
  group is moved to its station x (the layout depends on players / screens); everything else (local offsets,
  rotation, scale, added objects) is kept. A loader always uses `Load`; Carve + Paint on one machine uses `Carve`;
  unused groups are hidden. A missing group is built from the prefabs. Menu `Elves Workshop/Add Machines To Open
  Scene` recreates only the missing ones. Elves and their buttons stay generated (count and colors per player).
- Lighting: `GameScene/---- Lighting ----` (menu `Elves Workshop/Add Lighting To Open Scene`, only if missing): per
  screen two wall lamps (Furniture Cute Light_11 + warm point light) and two warm fill lights in front of the line, no
  shadows; plus the scene `Directional Light`. Ambient color is set by `ElfGame.SetupWorkshop`.
- Effect: `ElfSparkle` prefab (Flash + Stars particles, `UI/Sparkle.png`, `Materials/Sparkle.mat`) played by
  `ElfArt.Sparkle` when a machine transforms a toy (carve, paint, wrap), tinted with the step color.
- Prefabs (editable, `Assets/Pleyo/ElvesGame/Prefabs`): `ElfMachine` (gantry across the belt, toys pass under the
  head; runtime anchors Piston, Stripe, Lights = one sync light per player on the front post, Label), `ElfClaw`
  (anchors Arm / Claw / Cable), `ElfSleigh` (Pile, Counter), `ElfBin` (colliders: missed toys fall in with physics),
  `ElfButton` (Cap sinks at each landing). `Elves Workshop/Rebuild Assets and Prefabs` refills `ElfAssets.asset` and
  creates only the missing prefabs (delete one to rebuild it).
- Elves: ithappy Cute Characters like MM Skipping Rope (part meshes per letter, Hat_41 + jacket in the player color,
  procedural pointy ears, `Animation/ElfKid.controller` + Mixamo clips copied from Skipping Rope). Toys: raw block
  (EKstudio BoxWooden) -> Toy_05 / Toy_03 alternately (unpainted wood until Paint) -> Christmas Present_00x.
- Solo player on a station: piston in the player color; Paint / Wrap recolor the main color of the toy / gift to the
  player color (`ElfArt.ToyMaterial`: palette texture recolored by hue, main colors computed by the builder into
  `ElfAssets.CarvedMainColors` / `PresentMainColors`, presents ignore their bow). The transformed toy glows with the
  jump buttons (emission + light, `ElfToy.Glow`). Jump
  buttons light up (emission + small light) when their machine transforms a toy; socket stretched to the floor.
- Snow: `GameScene/---- Outside Snow ----` (menu `Elves Workshop/Add Snow To Open Scene`), just in front of
  `Walls/Outside` (no noise in depth, or the flakes drift in front of the wall).
- Score boards (`ElfBoards`, world-space wood + paper): one team board per sleigh of the claw row (line order left to
  right, Load on the sleigh under the claw); gifts / unfinished boards on the belt front, in the widest free spaces of
  each screen half.
- Load: claw in front of the belt (camera side, turned 180°), drops straight toward the camera into a row of sleighs
  (`Load/Sleigh` = template, scale 1), one per team, empty at start; a full sleigh (16 gifts) slides left, an empty one
  pops in under the claw and the leftmost shrinks away. Camera defaults 12 / -24 / 20° (scene values set the same).
- Elves + buttons stand on top of their machine, side by side (only mode since 2026-11-25): on the bottom of its `Head`
  (hidden) if the template has one, otherwise on the top of `Machine`, above the piston. The claw station keeps its elves behind the belt.
- Presence: an absent player's elf is hidden (only its "Jump to play" pill stays); 10 s without a jump = inactive, the
  elf fades out; his machine then runs on its own. Hairstyles limited
  to the ones that stay under the hat.
- HUD: MM Skipping Rope style (white rounded cards, Fredoka font copied to `Fonts`, `UI/RoundedRect.png`): timer top
  center; bottom row on wooden boards with a paper sheet (`UI/WoodPanel.png`, `UI/Paper.png`, generated) = gifts card + unfinished card (icon `UI/GiftIcon.png` rendered by the builder) + one square per
  team (step, letters, score; yellow = leading team); tutorial / results card, big outlined countdown. No station
  titles in 3D.
- Inputs: MM sensors / recordings (InputMethod 3, 5) and Kinect (4) via `MyMMPlayer` -> `ElfInput`; touch (7, 8) and
  dev keys (1..9, 0, -, = : players A..L jump; Space: all) in `ElfGame`.
- Texts FR / EN in `ElfLoc` (config `Lang`).

## Project layout (`Assets/Pleyo`)

Created by the template menu `Pleyo/New Project/Setup Camera MM Game` (one folder per theme, holding all its assets).

- `Managers/GameManager` — `MyGameManager : MMGameManager` (states driven by template overrides: `GameLoading_*`,
  `GamePlaying_*`, `GameOver_*`); `Prefab/-- MM Managers Variant --` (variant of the template's `-- Managers --`:
  `MyGameManager` instead of `PleyoStationGameManager` / `MMGameManager`, `PleyoMMSensors` instead of `PleyoSensors`,
  `LeaderboardData` disabled, `ScenesDataManager` maps `PleyoStation_MM_Game` to `GameScene`).
- `Managers/Players` — `MyMMPlayer : PleyoMMPlayer`; `Prefab/PlayerPrefab` (only `MyMMPlayer` at creation).
- `Managers/SplitScreen` — `Prefab/SplitScreenWithDynamicWidth Variant` (variant of the template's split screen, with
  the post-processing below); `Prefab/IndividualUi_NEWVersion Variant` (per-player HUD, variant of the template's
  `ExempleOfIndividualUi_NEWVersion`, set as `UIScreenContainerManager.IndividualPlayerUiInterfacePrefab`). Put the
  game's own per-player UI in this variant.
- `PostProcessing` — `PleyoGlobalVolumeProfile` (URP Volume profile: Bloom, ACES Tonemapping, Color Adjustments, Lift
  Gamma Gain) and `PleyoGlobalPostProcessing` (Post Processing Stack v2 profile).
- `Scenes/HomeLoadingPage.unity` — splash / home page, copy of the template's `HomeLoadingPage_TemplateDefault`
  (`-- LoadingPage Managers --`). Its `ScenesDataManager` maps `PleyoStation_MM_Game` to `GameScene`; its
  `NextSceneInfos` goes automatically to `PleyoStation_MM_Game` on load, through MoreMountains' `LoadingScreen`.
- `Scenes/GameScene.unity` — main game scene.
- Build Settings order: `HomeLoadingPage`, `GameScene`, then template scenes `PleyoOverlay` (version overlay, loaded
  additively by the managers' `Version` object), `LoadingScreen` (MoreMountains, used by `MMLoadScene`),
  `Template_QuitScene` (`Quit_Scene`). Play from `HomeLoadingPage` to test the full flow.

`Assets/Packages/com.pleyo.template/...` may hold empty folders created by Odin Inspector for its config: leave them.

## Scene setup

- `GameScene` hierarchy, groups separated by `---- <Group> ----` objects: Managers (`-- MM Managers Variant --`),
  Cameras (`SplitScreenWithDynamicWidth Variant`), Environment (`Directional Light`), Gameplay (`PlayerSpawnPoint`).
- Players: `PlayerSpawnPoint/PlayerTrampo0..11` (instances of `PlayerPrefab`), one per trampoline 0..11; PlayerId from
  `PleyoMMSensors.TrampolineId_to_PlayerId` (trampolines 0..5 → 0, 2, ..., 10; 6..11 → 1, 3, ..., 11), each on its
  player layer (6 + PlayerId, menu `Pleyo/Setup Player Layers`).
- KinectManager: `-- MM Managers Variant --/PleyoConfiguration/PossibleSensors/PleyoKinectSensor/KinectAssetParent_EnableOnStartIfNeeded/KinectController`
  (frames at the template defaults).
- Development without a camera: set `Kinect4Azure_Cam_ID0` to Play Recording with a `.kinect` file from
  `C:\pleyo_installations\Recording\` (write here which one).
- Localization: `%USERPROFILE%\AppData\LocalLow\Pleyo\PleyoData\ConfigurationFile\Localization_MMElvesWorkshop.csv`
  (machine file, outside the project; created from the template's default by the setup menu).
- Enter Play Mode reloads the domain (Enter Play Mode Options = none). Don't disable domain reload: the template and
  Pleyo DLL statics / singletons are never reset otherwise (NullReferenceException on
  `PleyoNetwork_Interface.instance` / `PleyoDLL_Interface.instance` from the second Play on).
- Active Input Handling = Both: the template uses the legacy Input Manager and the Input System (MoreMountains does not
  compile with the Input System only).
- Close button (top right) and FPS counter (bottom right): the template's `FpsAndQuitButton` prefab, instantiated on
  each screen by `UIContainer_ScreenID`. They follow `"DebugMode"` in
  `C:\pleyo_installations\ConfigurationFile\ConfigurationFileExtended.json`: debug off → quit button alpha 1/255 (still
  clickable) and FPS text hidden (FPS only logged every 10 s); debug on → both visible.
- Known console noise at every Play: "D1 / D2 is not available" (`Kinect4Azure_Cam_ID1/ID2` with no device). Not
  caused by game code.

## Post-processing

In `SplitScreenWithDynamicWidth Variant`:

- `Global Volume` (URP `Volume`, global, profile `PleyoGlobalVolumeProfile`): what is actually rendered under URP. The
  12 player cameras have URP post-processing on (`UniversalAdditionalCameraData.renderPostProcessing`).
- `PostProcess` (Post Processing Stack v2 volume, layer 18 `PleyoPostProcessing`, profile `PleyoGlobalPostProcessing`)
  and the cameras' `PostProcessLayer` come from the template; the player cameras also render layer 18. PPv2 is not
  rendered by URP: change the look in the URP profile.

## Flow (2026-11-25)
- Tutorial (10 s, `ElfHud.BuildTutorial`): one card with the rules, the unfinished gift picture, a time gauge and one
  chip per player of the screen, checked once the player jumped; a player who does not jump stays inactive until his
  first jump (AFK delay reset at the end of the tutorial). Then the 3 s countdown.
- End (`ElfGameEnd.cs`): big 3-2-1 over the last 3 s of play, "Time's up!" pause, fade to white, the line (machines,
  claws, sleighs, boards, belt, spawner, bin, toys) is swapped for a podium of the teams at the centre of screen 1 seen
  by every screen through screen 1's game camera, round gauge 8 s, iris close, quit. No template leaderboard.
- Indoor light only: directional lights are switched off at runtime, flat ambient, low sky reflections.
- Splash (3.5 s, before the tutorial): logo `ElfAssets.Logo` (empty: the title in big letters), each camera glides down
  to its game view. Tutorial pictures rendered at start by `ElfIcons` (studio at y -1000: the screen's machine template
  with an elf on its button, toy stages box > carved > painted > gift).
- Ring / terraces: each camera aims at the middle of its screen's machines (not the screen centre). Decor cutting a
  camera's view of its line is hidden for that camera only (`ElfOccluders`, forceRenderingOff per camera).
- Toys: carved toys and gifts scaled so their largest side matches the raw box.
- UI: one wood board per player on the belt front, in front of his machine (letter, his score); gifts / unfinished on a
  white HUD card (top left, with the timer); team boards stay on the sleighs.
- Ring / terraces (several screens): each claw stands behind the belt (tree side) with one sleigh behind it, its player
  on his button on top of it (arm joint); no UI on the sleighs (team boards and counters only with 1 screen). The 1-screen
  map keeps the claw on the camera side with the sleigh row and its boards.
- Gift pile (`ElfGameLine.cs`, ring / terraces): the claws throw each gift in an arc onto `ElfGame._giftSocle` (scene:
  `---- Workshop Ring ----/GiftSocle`, the user's cylinder), stacked in sunflower layers; no sleighs then.
- Countdown: the belt is pre-filled with ghost toys (75% transparent copies of the materials), transformed on their
  own by the machines they pass, not playable; at "Go!" they become normal toys, so every machine has toys at once.
- Juice (`ElfSquash`): the machine / claw squashes a little when an elf lands on it; the toy spawner pops (stretch then
  squash) with a light burst at each box. Ring camera 7.4 / -19.62 / 7°; player boards at 0.0065 m/px (clear the floor).
- Mood lighting (`ElfGameLight.cs`, runtime, `ElfGame._moodLighting`): warm spot per team, three-colour ambient (dim
  cool top, warm floor), garlands x4 emission + bloom (threshold 1: white HUD cards don't bloom), moonlit glass / sky
  with small blue lights at the windows (12 max), vignette, linear haze 35-90 m. Settings on ElfGame (Lighting).
- Lights: a narrow spot above each player (`_playerSpotIntensity`), a wide one only above a team with no player. The
  socle spot is the user's (`---- Workshop Ring ----/Lights/Spot Light`, tuned 2026-11-26: y 12, 62°, 45, soft shadows).
- Gift pile: grid cells, every other layer shifted half a cell and narrower (stepped pyramid), filled from the centre;
  each parked gift aligned and scaled to the layer height (0.66).
- Event (`ElfGameEvent.cs`): twice per game (at ~28-40% and ~65-76% of the play) the spawner malfunctions for
  10-15 s: shakes / wobbles (`ElfSquash.SetShake`), sparks, red flashing light, boxes x2.2 faster, big message.
- Ending on ring / terraces: no podium; one camera for every screen (from screen 1's side), the teams on the floor in
  front of the socle (front-left / front-right, best ranks in the middle), all facing it, a big "gifts delivered" label
  above the pile; no jumps from game over (celebration). (Older note: each team on its screen's side, rank /
  step / score labels turned to each camera as it renders; every camera moves in on the socle. 1 screen: podium.
- Malfunction also shakes the belt object and flickers every light (restored after), the title shakes and flashes
  (`ElfHud.SetBigShake`). Music (`ElfGameAudio.cs`): `_mainMusic` = Assets/Xmas_Christmas_Song_Loop (pitch 1.2 during a
  malfunction), `_endMusic` (empty for now) crossfades in at the white fade.
- 2026-11-26: audio listener added on screen 1's camera (the scene had none: no sound); end music =
  Mobile Games Bundle/.../Music_Box/Loop/MUSC_Toy_MusicBox_JingleBells_Loop_1. Malfunction: belt tiles sway sideways out
  of phase, toys hop (`ElfToy.Jolt`), lights turn red in waves, screen shake (`EventShake`); light camera shake on each
  landing of the screen's players. End: one camera, a straight row of elves in front of the socle (gap in the middle),
  HUD board `UI/CeremonyBoard.png` (cut out of the user's picture by `ElfImageTools.CutOutWhite`) with the gifts and the
  unfinished gifts; round gauge top right.
- Sounds (`ElfGameSfx.cs`): ElfGame > Sounds (SFX): one entry per action (clip, volume, pitch variation) + master volume;
  defaults picked from the project's packs (Christmas / Comedy Cartoon / Collectibles). 12 pooled 2D sources + 1 loop.
- Ceremony: `---- Workshop Ring ----/Ceremony Camera` (pose + FOV used by every screen; renders to Display 8 in the
  editor, disabled in game) and `Décors cérémonie` (children shown only at the end), both on ElfGame (End ceremony).
- Toys: ElfGame > Toys: box height (m) and a scale per stage (carved, painted, gift). The button glows a little (30%)
  at each jump, fully when the machine transforms a toy.
