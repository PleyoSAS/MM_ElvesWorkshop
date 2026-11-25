# Pleyo projects — shared Claude Code guide

Shared by every Pleyo game built on the `com.pleyo.template` package. The reference copy ships with the template
(`Documentation~/Claude/CLAUDE_PLEYO_TEMPLATE.md`, see `CLAUDE_NEW_PROJECT.md` next to it). The template's editor script
`PleyoClaudeDocsSync` copies it to the project root at each Editor load (and refreshes it when the template brings a new
version: **never edit the project copy**), and creates a blank `CLAUDE.md` if there is none. The project's `CLAUDE.md`
imports it with a line `@CLAUDE_PLEYO_TEMPLATE.md`. Project-specific content (layout, game rules, scene setup) stays in
`CLAUDE.md`. Facts below were checked against template 5.3.1: re-check names if the template changed a lot.

**Finding something in the template**: read `TEMPLATE_INDEX.md` first (next to this file in the template:
`Documentation~/Claude/`, in a game project `Library/PackageCache/com.pleyo.template@*/Documentation~/Claude/`). It maps
folders, key classes and "where to look for X", so you only open the files you need.

## Language

- **Code, identifiers, comments, commit messages and docs are written in English** (international team).
- Chat with each developer in the language they write in, unless the table below says otherwise. Recognize the
  developer from the git user name / email or the OS user name given in your context. A personal `CLAUDE.local.md`
  (git-ignored) still overrides this.

| Developer | Recognized by | Chat language |
|---|---|---|
| Matthieu Caille | git user `LtkStark`, `matthieu.caille@pleyo.fr`, OS user `Matthieu` / `Mattc` | French |

## Working method

- **Plan first.** Unless the change is truly trivial (a typo, a value, a one-line fix), present a short plan of what
  you intend to do (files, approach, open questions) and wait for the developer's OK before writing code or changing
  assets.
- **Git: never commit, never push** (not even local commits) unless the developer explicitly asks for that commit.
  Leave the changes uncommitted: the developer reviews them in GitHub Desktop's changed files, then commits and pushes.
- **Keep plans and reports short**: the main lines in 5-6 lines at most, no step-by-step detail. Go into detail only
  when it is really necessary (a risky or irreversible choice), or when the developer asks for it.
- Keep the light optimization reflexes of "Performance" (in Coding conventions) in every change.

## Pleyo Claude skills — check them at the start of each session

The template ships Claude skills in `Documentation~/Claude/skills/<name>/SKILL.md`, each with a `version: x.y.z` in its
front matter (e.g. `pleyo-new-camera-project`: creates a new camera game project; `pleyo-deploy-game`: builds a game and publishes it through the AutoUpdater). They are installed per developer in
`~/.claude/skills/<name>/` (user level: some are needed outside any project), so a template update brings new versions
that each developer installs once.

- In your **first answer of a session**, before the rest, compare the template's skills (game project:
  `Library/PackageCache/com.pleyo.template@*/Documentation~/Claude/skills/`; MM_Tower: `Packages/PleyoTemplate/Documentation~/Claude/skills/`)
  with `~/.claude/skills/`: a skill missing there, or with a lower `version`, is new for this developer.
- If there is one, say it in one line, in the developer's language ("New Pleyo skill in the template: `<name>` v<x>
  (installed: v<y> / none). Install it?") and on yes copy its whole folder to `~/.claude/skills/<name>/` (it is
  available from the next session). If the developer declines, don't ask again in the same session.
- Without Claude: menu `Pleyo/Claude/Install Claude Skills` in Unity (the Unity console also warns at Editor load).

## Pleyo context

Pleyo makes **multiplayer motion games**. Depending on the product, players are tracked by a depth camera (Azure
Kinect / Orbbec Femto Bolt), by sonic sensors under trampolines, or play on a touch surface (see Input methods below).
The games were originally played on trampolines; trampolines are now being phased out, but the vocabulary stays.

- Each player has **their own play area: a circle on the floor** (this may change later).
- A player is **activated only when standing on their circle**.
- Once active, the player's position is expressed **relative to the center of their circle**.

### Input methods — not every product has a camera

The input comes from the machine's configuration file, **`C:\pleyo_installations\ConfigurationFile\ConfigurationFileExtended.json`**
(`"InputMethod"`, read through `PleyoDLL_ConfigurationFileExtended.LoadConfigurationData()`), as an
`EPleyoInputMethods` value (`PS_Sensors/PS_Sensors_Common/Scripts/SensorLoader.cs`):

| Value | Input | Notes |
|---|---|---|
| 0 / 1 | `Keyboard` / `Mouse` | Development |
| 2 | `LidarAndSonicSensors` | Single-station sensors |
| 3 | `MMSensors` | Multiplayer sonic sensors under the trampolines (`PleyoMMSensors`: jumps / landings, no body) |
| 4 | `PSV2Kinect` | **Depth camera**: skeletons, calibrated circles, `TrampolinePov` joints, color picture |
| 5 / 6 | `MMSensors_FromRecord` / `Kinect_FromRecord` | Recorded data |
| 7 | `TouchFrame` | Touch surface (L-shape: floor + wall) |
| 8 | `TouchFrameAndCamera` | Touch surface + camera |

- Camera-only features (skeleton / stickman, avatars, body outlines, player face / body pictures, the coordinate
  spaces below) **do not exist** with sonic sensors or a touch surface. Know which input methods the product supports
  (ask the developer) before building on the camera, and keep camera features optional when a product supports several.
- Touch surface: the reference scene is the template's
  `Runtime/Pleyo/_PleyoTemplate/LShape/PleyoTouch/Demo/Scene/PleyoTouch_Demo.unity` (manager prefabs in
  `LShape/PleyoTouch/PleyoTouch_Manager/Prefab/`, `PleyoTouch_AllInOne` / `PleyoTouch_Manager`; touchable objects use
  `PleyoTouch_InteractibleObject`). Its calibration is `C:\pleyo_installations\ConfigurationFile\PleyoTouchSurfaceCalibration.json`.

### Coordinate spaces (camera products) — read this before touching any player position

- **`KinectPov`**: raw coordinates returned by the AzureKinectExamples asset, relative to the **camera's point of view**.
- **`TrampolinePov`**: our own transposition. It is computed from a small manual calibration and expresses positions
  **relative to the center of the player's floor circle**. The name is historical: it is used with or without an
  actual trampoline.
- The conversion lives in the template: `PleyoKinectCalibratedTrampoline` (`FromKinectPovToTrampolinePov`,
  `FromKinectRotToTrampolineRot`, `TryGetJointPos_TrampolinePosition`,
  `PleyoKinectCalibratedTrampoline_PlayerInfos.TryGetJointsPosition_TrampolinePov`, ...).
- **Gameplay code should work in `TrampolinePov`.** Only use `KinectPov` when you have a specific reason, and say so
  in a comment.
- **Never change the calibration from code** (no automatic ground / height correction): if positions look off (e.g.
  the player sinks into the floor), the camera may have moved and the developer recalibrates.

## The Pleyo template (read-only in game projects)

All Pleyo projects are built on the **`com.pleyo.template`** package (git: `PleyoSAS/PleyoTemplate`).

- In a game project it lives in `Library/PackageCache/com.pleyo.template@<hash>/` (shown as `Packages/Pleyo template`).
- **It is read-only there. Never edit files in the package cache.** Edits are lost on the next resolve.
- The template itself is developed in the **MM_Tower** project, where it is an embedded package
  (`Packages/PleyoTemplate`, git submodule, `file:PleyoTemplate` in the lock file): only there is it editable.
- If something in the template is wrong or genuinely blocking, **stop and tell the developer**: explain what is wrong
  and propose the fix (or a prompt for an agent working in the template project). Don't work around it silently.
- Prefer **extending** template classes (they are designed for inheritance and expose `virtual` members) over
  duplicating them.
- Updating: `Packages/manifest.json` points at the git repo without a version, but `Packages/packages-lock.json` pins a
  commit hash. A pushed template change only arrives after "Update" in the Package Manager (or a new lock hash).
- Assemblies: template runtime code is in `Runtime/Pleyo.Template.asmdef` (auto-referenced; references
  `AzureKinectExamples`, `MoreMountains.Tools`, `MoreMountains.Feedbacks`, `Unity.TextMeshPro`, `Unity.InputSystem`).
  Game scripts (Assembly-CSharp) see all of it. Template code can never depend on game scripts.
- Template classes are in the **global namespace** (the Kinect asset uses `com.rfilkov.kinect`).

Key template areas (`Runtime/`):

| Path | Content |
|---|---|
| `Pleyo/_PleyoTemplate/00_Common` | Game manager base (`PleyoGameManagerTemplate`), localization, multi-screen, NATS networking, tools |
| `Pleyo/_PleyoTemplate/20_Game/210_PleyoStations/210_Common/PS_Sensors` | Sensor layer: `PleyoSensors`, Kinect skeletons, calibration (`PleyoKinectCalibratedTrampoline`, `PleyoKinectCalibratedTrampolineManager`, `PleyoKinectInterface` in `PS_Sensors_Kinect/Scripts/3DGames`), gesture recognition |
| `Pleyo/_PleyoTemplate/20_Game/230_PleyoMM` | Multiplayer ("MM") framework: `MMGameManager`, `PleyoMMPlayer`, `PleyoMMSensors`, split-screen cameras (`MMCameraPlayer`), timers, individual player UI |
| `Pleyo/_PleyoTemplate/30_Leaderboard` | Leaderboard and score UI |
| `Pleyo/_PleyoTemplate/LShape` | L-shape setup (floor + wall), floor calibration, touch |
| `ThirdParty/AzureKinectExamples` | Depth camera asset (namespace `com.rfilkov.kinect`, `KinectManager`, `KinectInterop.JointType`). Almost all player tracking is based on it |
| `ThirdParty/MoreMountains` | MMFeedbacks / MMTools (events, feedbacks) |
| `ThirdParty/LeanTween`, `ThirdParty/PathCreator` | Tweening, bezier paths |
| `Plugins/Sirenix` | Odin Inspector |

## Template API cheat sheet

### Game flow

- `PleyoGameManagerTemplate` → `MMGameManager` → the game's manager. States are `protected virtual` hooks to
  override (call `base`): `GameLoading_Start/Update/End`, `GamePlaying_Start/Update/UpdateTimer/End`,
  `GameOver_Start/Update`. `MMGameManager.GameLoading_Start` sets up the split view (`InitSplitView`).
- `MMGameManager.NB_PLAYER_MAX = 12` (hard-coded in several places).

### Players and ids — three different ids

- **PlayerId** (`PleyoMMPlayer.PlayerId`, `MMCameraPlayer.PlayerId`): the game's player slot.
- **trampolineId**: the floor circle, letters A..L = 0..11. Players alternate between the two halves
  (A..F = 0..5, G..L = 6..11): trampolines A, B, C... are PlayerIds 0, 2, 4..., G, H, I... are 1, 3, 5...
  Convert with `PleyoMMSensors.PlayerId_to_TrampolineId` / `TrampolineId_to_PlayerId`.
  Pitfall: `PlayerId_to_TrampolineId` returns **0** for an unknown id (not -1).
- **Kinect userId** (`ulong`): the body tracked by the camera, changes when someone leaves and comes back.
- Chain from a player to the camera data:
  `PlayerId` → `PleyoMMSensors.PlayerId_to_TrampolineId` → the `PleyoKinectCalibratedTrampoline` whose `TrampolineId`
  matches → `GetLastKinectPlayerInfos()` (`isUserOnTrampoline`, `userIdOnTrampoline`) →
  `PleyoKinectInterface.Instance.TryGetKinectSensorIndex(userId, out sensorIndex)` → `KinectManager` calls with that
  userId / sensor index (`GetBodyIndexByUserId(userId)` for body index maps).
- Calibrated trampolines can sit on **inactive GameObjects**: search with
  `FindObjectsByType<PleyoKinectCalibratedTrampoline>(FindObjectsInactive.Include, ...)`.
- Per-frame tracking: `PleyoKinectCalibratedTrampoline.OnUpdate` (`UnityEvent<PleyoKinectCalibratedTrampoline_PlayerInfos>`).
  `PlayerInfos.jointsPosition_TrampolinePov` (`Dictionary<KinectInterop.JointType, Vector3>`, meters) plus
  orientations / velocities / confidences; `leftFootPos3D` / `rightFootPos3D` are TrampolinePov **meters** (their code
  comment says centimeters: it is wrong), `leftFootPos2D` / `rightFootPos2D` are normalized by the circle radius.
- Player color: `MMDefaultPlayersColor.Instance.GetPlayerColorFromPlayerId(playerId)`.
- A player's split-screen camera: the `MMCameraPlayer` with the same `PlayerId` (Camera on the same object).

### Kinect asset (AzureKinectExamples) pitfalls (camera products)

- `KinectManager.GetJointKinectPosition(userId, joint, false)`: **depth camera space**, meters, y pointing **down**.
- `KinectManager.MapSpacePointToColorCoords` projects with the color intrinsics only and **skips the depth-to-color
  extrinsics** (~100 px off on an Azure Kinect). To put a joint on the color picture use
  `MapSpacePointToDepthCoords(sensor, p)` then `MapDepthPointToColorCoords(sensor, depthPixel, (ushort)(p.z * 1000))`
  (its own distance, in mm: works off-picture and without a depth measure).
- `sensorData.colorImageTexture`: raw camera rows (row 0 = top of the picture, `colorImageScale` = (-1, -1, 1)).
- `sensorData.colorBodyIndexBuffer` (color-camera-space body index, one byte per color pixel packed 4 per `uint`,
  255 = nobody, intermediate values on edges) is only computed while the buffer exists: create it if null, and only
  dispose it if you created it (other tools share it).
- The **color camera sees less than the depth camera**: 16:9 color modes cover about ±29° vertically, 4:3
  (`2048x1536`) about ±37°, WFOV depth about ±60°. Players close to the camera are tracked but their head can be out
  of the color picture. Color features need `Get Color Frames = Color Texture` on the KinectManager.
- Testing without a camera: set the sensor interface (e.g. `Kinect4Azure_Cam_ID0`) to **Play Recording** with a
  `.kinect` file (Pleyo machines: `C:\pleyo_installations\Recording\`). It fakes the tracked players.

### Player pictures from the color camera (camera products)

Template tools in `PS_Sensors/PS_Sensors_Kinect/PlayerRGB` (use them, don't rewrite them):

- `PlayerFaceTexture` / `PlayerBodyTexture` (base `PlayerRGBTexture`): a square texture of a player's face or whole
  body, background removed, from their `PlayerId` (`GetTexture`, `HasTexture`, `GetStatus` tells why there is none).
  `PlayerBodyTexture.TryGetJointUV` gives each joint's position in the texture. `KinectColorBodyIndexService` shares the
  color-space body index buffer between users.
- KinectManager requirements, set **in its inspector** (read in its Awake, too late from code): `Get Color Frames =
  Color Texture`, `Get Body Frames = Body And Body Index Data` (`StickManBodyDisplay` logs an error otherwise). Games
  without player pictures keep `None` / `Body Data Only` (cheaper). Players close to the camera can have their head out
  of the color picture (status `OutOfColorView`).
- `StickMan/StickManFaceDisplay` / `StickManBodyDisplay` show those pictures on a game's stickman: the game's stickman
  script inherits `StickManRGBTarget` (stencil contract in its comment). The display shaders need URP.
- Objects that must stay visible in front of the pictures (swords, balls...): add `StickManRGBOccluder` on their root.
  It sets the reserved stencil bit 128 where they are visible (player values stay 1..127); the picture and its halo skip
  those pixels (`StickManRGBDisplay` "Respect Occluders", on by default). The game's own stickman shaders draw over
  occluders unless they respect the bit too (skip where it is set, write with WriteMask 127).

## Coding conventions

These apply to all **new or modified** code. Don't reformat untouched legacy code just to match them.

### Types

- **Never use `var`.** Always write the explicit type.
  ```csharp
  List<MyMMPlayer> players = new List<MyMMPlayer>();   // yes
  var players = new List<MyMMPlayer>();                // no
  ```

### Access modifiers — everything is designed for inheritance

Any teammate may need to subclass and override anything.

- **Methods**: default to `protected virtual` or `public virtual`. Use `private` only when it is a deliberate choice,
  and add a short comment saying why.
- **Fields**: default to `protected` or `public`. Use `private` only when it is a deliberate choice.
- Overrides should call `base.X()` unless there is a reason not to.

### Naming

| Member | Convention | Example |
|---|---|---|
| protected field | `_camelCase` | `protected float _jumpHeight;` |
| public field / property | `PascalCase` | `public Camera PlayerCamera;` |
| methods | `PascalCase` | `public virtual void ResetPlayer()` |

Serialized protected fields use `[SerializeField] protected Type _name;`.

### Comments

- **Never remove existing comments.** Update them if the code they describe changes.
- Comment the **why** and anything non-obvious: coordinate space, calibration assumptions, units, ordering
  constraints, workarounds.
- Don't write comments that restate the code (`protected int _score; // the player's score`).
- `[Tooltip]` on serialized fields is welcome for designers.

### Class layout with `#region`

Organize every class in this order:

```csharp
public class ExamplePlayer : PleyoMMPlayer
{
    #region Variables
    [SerializeField] protected float _speed = 1f;
    public Camera PlayerCamera;
    #endregion

    #region Unity Methods
    // Awake, OnEnable, Start, Update, FixedUpdate, LateUpdate, OnDisable, OnDestroy, OnTrigger*/OnCollision*...
    protected override void Awake()
    {
        base.Awake();
    }
    #endregion

    #region Public Methods
    // Public API, excluding getters/setters
    public virtual void ResetPlayer() { }
    #endregion

    #region Getters / Setters
    public virtual Camera GetPlayerCamera() { return PlayerCamera; }
    public virtual void SetPlayerCamera(Camera camera) { PlayerCamera = camera; }
    #endregion

    #region Internal Methods
    // protected virtual helpers
    protected virtual void UpdateMovement() { }
    #endregion
}
```

### Reusable tools

- Tools meant to be copied between projects may depend on the template, **never on game / scene scripts**: game code
  drives them through getters / setters.

### Performance — always keep a light optimization in mind

The games run up to 12 players with split screens at 4K: small savings add up. Without over-engineering, by default:

- **Pool what spawns often**: never `Instantiate` / `Destroy` objects spawned during gameplay (cubes, effects,
  projectiles...): reuse them (the game projects have a pooling tool, e.g. NeonStudio `Tools/Pooling`).
- **Few `Update` methods**: a manager with an `Update` is fine, but 200 cubes each with their own `Update` are not. Let
  one parent / manager update them all in a loop, or drive them by events.
- **Nothing expensive per frame**: cache components and references (no `GetComponent`, `Find*`, `FindObjectsByType`
  in `Update`), avoid per-frame allocations (LINQ, `new` lists / arrays, string building), prefer non-allocating
  physics queries.
- Skip work that is not visible or not needed (disabled players, hidden visuals, untracked users), and say in a short
  comment when a choice is made for performance.

## Working with the Unity Editor

- If the Editor is open, prefer driving it through the Unity CLI (`unity status`, `unity command ...`; the
  `com.unity.pipeline` package must be installed in the project) **over hand-editing `.unity`, `.prefab` or `.asset`
  YAML**. Several Editors open at once: target one with `--project-path`.
- After C# changes, recompile (`unity command recompile`, then poll `recompile_status`) and check for compile errors
  before going further.
- Running C# in the Editor: write the snippet to a file **inside the project** (e.g. `Temp/claude/x.cs`, never
  `Assets/`) and run `unity command eval_file --file <path>`. Use fully qualified type names.
- Play mode: after `editor_play`, wait ~10 s before `eval_file` (the main thread is busy loading; calls time out).
  With a recording, poll until the player is tracked instead of assuming a fixed delay.
- Screenshots: `unity command screenshot --view game --output <absolute path>`.
- Prefabs: `PrefabUtility.LoadPrefabContents` → edit with `SerializedObject` → `SaveAsPrefabAsset` → `UnloadPrefabContents`.
- Moving / renaming assets: `AssetDatabase.MoveAsset` (keeps GUIDs, so prefab and scene references survive).
- On Windows, a fresh PowerShell may not see the `unity` command: reload `PATH` from the User + Machine environment.
