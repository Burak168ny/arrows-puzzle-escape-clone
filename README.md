# Arrows – Puzzle Escape (Clone)

A portfolio-quality Unity clone of the hyper-casual puzzle game **Arrows – Puzzle Escape**.
Tap an arrow and it slides out of the board like a snake, following its own body. If something is in the way, it bounces back and you lose a heart. Clear the board to win.

The focus of this project is **clean architecture, a testable game core, solid tooling and game feel**. The game itself matters too.

<p align="center">
  <img src="docs/media/gameplay.gif" alt="Gameplay" width="320"/>
</p>

| Menu | Gameplay | Big board | Level Generator |
|:---:|:---:|:---:|:---:|
| ![](docs/media/menu.png) | ![](docs/media/gameplay.png) | ![](docs/media/heart_30x30.png) | ![](docs/media/generator.png) |

> 📌 *Add the media files to `docs/media/` (see [Recording media](#recording-media)).*

---

## Highlights

- **Pure C# game model** (`noEngineReferences`): grid, arrows, movement rules, lives, solver, generator. It is fully unit-tested and runs outside Unity.
- **Greedy solver that is provably complete.** It also powers hints and level validation.
- **Procedural level generator** that is *solvable by construction*. It reaches 94–99 % board coverage on grids up to 30×30, supports shape masks (heart, circle, diamond, ring) and has a difficulty metric.
- **Editor tooling**:
  - Level Generator window with live preview, solver stats and a hand-drawing edit mode.
  - One-click scene setup: no manual wiring, no prefabs to assemble.
- **Game feel**:
  - Snake-like movement with ease-in exits.
  - Bump with elastic bounce, red flash and camera shake.
  - Ripple pop-in, tap punch, combo pitch and confetti.
  - Native haptics (iOS Taptic Engine / Android `VibrationEffect`).
- **Mobile-ready**: pinch-zoom, bounded pan, tap/drag disambiguation, safe area, portrait 1080×1920, 60 FPS target, no per-frame allocations, pooled views, single scene.
- **97 EditMode tests**: movement, collision, self-collision, solver, generator, repair, serialization, progress, camera math.

## Tech

| | |
|---|---|
| Engine | Unity **6000.0.58f2** (Unity 6) |
| Rendering | URP 2D Renderer, `LineRenderer` + procedural meshes |
| Input | Input System (Enhanced Touch + mouse) |
| UI | uGUI + TextMeshPro, built from code |
| Tests | Unity Test Framework (NUnit), EditMode |
| Third-party | **none**: no DOTween, no JSON or tween libraries, no art or audio packs |

All sprites (hearts, rounded panels, dots, dashes) and placeholder sounds are **generated procedurally**, so the repository contains no binary art.

---

## Getting started

1. Clone the repo and open the folder with **Unity 6000.0.58f2** (or a newer Unity 6).
2. Run **`Tools → Arrows → Setup Game Scene`**.
   - The first run imports *TMP Essential Resources*. Run the menu again when that finishes.
   - This step creates `GameConfig`, the level database, the audio library, the UI sprites and a fully wired `Scenes/Game.unity`. It also sets portrait orientation.
3. Press **Play**. Use the Game view's *Simulator* to try touch gestures.

| Menu | What it does |
|---|---|
| `Tools/Arrows/Setup Game Scene` | (Re)generates the scene and assets. Settings in `GameConfig` are kept. |
| `Tools/Arrows/Apply Classic Style` | White board, thin monochrome arrows with sharp corners, no dots (the original look). |
| `Tools/Arrows/Refresh Level Database` | Rescans `Levels/*.json` into the `LevelDatabase`. |
| `Tools/Arrows/Level Generator` | Generator window (see below). |
| `Tools/Arrows/Generate Campaign (default seed)` | Rebuilds the 30 shipped levels. |
| `Tools/Arrows/Reset Progress` | Clears saved progress and settings. |

**Run the tests:** open `Window → General → Test Runner → EditMode → Run All`.

---

## Architecture

```
┌──────────────────────────────── Arrows.Game (MonoBehaviours) ────────────────────────────────┐
│                                                                                              │
│   InputHandler ──taps/pan/zoom──►  GameManager  ◄──clicks──  UIManager ──► UIPanel / HudView  │
│   (gestures)                       (flow FSM,       ──events──►            SettingsPanel      │
│                                    composition root)                                         │
│                                      │      │    ──events──►  FeedbackController             │
│                       launch/eval    │      │ build/animate     ├─► AudioManager (+library)  │
│                                      ▼      ▼                   ├─► Haptics (iOS/Android)    │
│                              ┌────────────┐  BoardView ─► ArrowView ×N (pooled)              │
│                              │  model     │  CameraController (fit / pan / zoom / shake)     │
│                              └────────────┘  HintPathView, ConfettiEffect                    │
└──────────────────────────────────────│───────────────────────────────────────────────────────┘
                                       ▼
┌────────────────────────── Arrows.Model (pure C#, no UnityEngine) ────────────────────────────┐
│  GridPos · GridModel · ArrowModel · LevelModel · MoveResult · GameSession · LevelValidator   │
│  LevelSolver · Serialization (MiniJson, LevelSerializer) · Generation (LevelGenerator,        │
│  LevelShape, LevelRepair, Campaign)                                                          │
└──────────────────────────────────────────────────────────────────────────────────────────────┘
        ▲                                   ▲
  Arrows.Editor (GameSceneBuilder,    Arrows.Tests.EditMode
  LevelGeneratorWindow, SpriteBaker)
```

### Layers and rules

| Assembly | Depends on | Responsibility |
|---|---|---|
| `Arrows.Model` | – (`noEngineReferences: true`) | All game rules. Deterministic, testable, reusable by tools and tests. |
| `Arrows.Utils` | UnityEngine | Easing, procedural sprites and audio. |
| `Arrows.Game` | Model, Utils, Input System, TMP, uGUI | `Core` (flow, input, persistence, feedback), `View` (rendering, camera), `UI` (screens). |
| `Arrows.Editor` | all of the above | Scene builder, generator window, sprite baker. |
| `Arrows.Tests.EditMode` | Model, Game | Unit tests. |

- **Model knows nothing about Unity.** Views read the model and play animations; they never decide outcomes.
- **`GameManager` is the only composition root.** It owns the state machine `Loading → Menu → Playing → Win / Fail`.
- **UI and feedback are listeners.** `GameManager` exposes C# events (`StateChanged`, `LevelLoaded`, `LivesChanged`, `ArrowLaunched`, `ArrowImpact`, `HintShown`) and a small API (`Play`, `Restart`, `NextLevel`, `ShowHint`). `UIManager` is a presenter and `FeedbackController` turns events into sound, haptics, shake and confetti. Neither the flow nor the views contain feedback code.
- **No singletons.** References are serialized and wired by the scene builder. The only static is a fire-and-forget `UIEvents.ButtonPressed` signal.
- **Persistence behind an interface** (`IKeyValueStore`), so `ProgressService` is tested with an in-memory store.

### Life of a tap

```
Pointer up within slop  → InputHandler.Tapped(screenPos)
GameManager             → camera.ScreenToWorld → board.WorldToGrid
                        → level.FindArrowNear(x, y, tolerance)   // distance to center line, not cell hit
                        → session.TryLaunch(id) → MoveResult { Exit(freeCells) | Blocked(cell, blocker) }
                        → view.PlayExit / view.PlayBump        → ArrowLaunched event
ArrowView (bump)        → Impact → GameManager flashes both arrows → ArrowImpact event
FeedbackController      → sound (combo pitch) · haptic · camera shake
GameSession.Finished    → progress saved immediately → wait for animations → Win / Fail
```

---

## Rules and model

- The board is a `W×H` grid. Each arrow is a path of orthogonally adjacent cells. The **last cell is the head** and the **direction is `last − secondLast`**.
- Tapping an arrow launches it along its direction. The body follows its own path like a snake.
- If any cell between the head and the board edge is occupied, the arrow is **blocked**: it travels to the obstacle, bounces back and the player loses a life.

**Snake self-collision.** An arrow can curl so that its own body lies in front of its head. Cell `i` of the body (`0` = tail) becomes free after `i + 1` steps, and the head reaches a cell `k` steps ahead at step `k`. So the arrow hits itself only if

```
k ≤ i      (k = distance along the ray, i = body index of that cell)
```

`LevelModel.Evaluate` implements exactly this rule, and the tests cover both "tail already gone" and "body still there".

**Level format** (`Levels/*.json`):

```json
{ "width": 6, "height": 6,
  "arrows": [ { "cells": [[0,0],[1,0],[2,0]] },
              { "cells": [[5,0],[5,1]], "color": "#4A7BE8" } ] }
```

Unity's `JsonUtility` cannot read nested arrays, so the project ships a ~150-line `MiniJson` reader and a format-specific `LevelSerializer`. Both are covered by tests, including malformed input.

---

## Solver

**Greedy:** repeatedly remove any arrow whose path is open, until none are open.

**Why greedy is complete.** Removing an arrow only frees cells and never occupies new ones. So if an arrow can exit now, it can still exit after any other arrow has been removed: the set of exitable arrows only grows. If some order clears the board, then whatever arrow greedy picks first, the remaining board is still solvable, and by induction greedy clears it too. No search or backtracking is needed.

- **Complexity:** `O(n² · (W+H))` in the worst case. Solving the largest shipped board (151 arrows, 26×28) takes milliseconds.
- **Hint:** any open arrow is a correct move. The game picks an idle one, avoids repeating the previous hint and draws its exit path as flowing dashes.
- **Difficulty metric:** the solver records how many arrows are open at every step.
  - `AverageOpenArrows`: lower means fewer choices, which is harder.
  - `DifficultyScore = arrowCount / AverageOpenArrows`. A board of free arrows scores about 2; a forced chain of `n` arrows scores `n`.

---

## Level generator

Generating random arrows and then fixing deadlocks is slow and fragile. Instead, the generator builds levels that are **solvable by construction**.

1. **Constructive placement.** Arrows are added one at a time. A new arrow is accepted only if its exit ray is clear of every arrow already placed.
   - *Proof:* reverse the placement order. When arrow `k` is removed, only arrows placed before it remain, and its ray was clear of all of them.
   - Later arrows may freely sit in earlier arrows' rays. That is what creates the dependencies the player has to untangle.
2. **Grow from the head.** Pick an empty cell and a direction with a clear ray. Put the first body cell directly behind the head (this keeps the direction). Then random-walk away: go straight or turn with probability `TurnChance`, never onto the arrow's own ray, until a random length in `[MinLength, MaxLength]`.
3. **Center-first ordering.** Inner cells only have clear rays while the board is still empty, so heads are tried roughly from the center outwards (with jitter). Outer cells have short rays that stay clear longer.
4. **Safe tail extension.** Leftover holes are absorbed by extending an adjacent arrow's tail. This is allowed only if the hole is not on the ray of that arrow or of any arrow placed after it, because those arrows leave earlier and must not find the hole occupied. This step raised coverage on 30×30 boards from about 60 % to 94–99 %.
5. **Shapes.** A mask (`Rectangle`, `Circle`, `Diamond`, `Heart`, `Ring`) restricts which cells may hold arrows, so levels draw a silhouette. (Optional board dots, off in the classic style, mark only cells that start occupied.)
6. **Best-of-N.** Generate `Candidates` levels from consecutive seeds and keep the one closest to `TargetDifficulty` (or the hardest one).
7. **Verification.** Every result is still run through the solver, and tests check 25+ seeds across sizes and shapes.

`LevelRepair` handles hand-made or imported levels. It reverses stuck arrows when that unlocks more arrows, otherwise removes them, until the solver passes.

### Campaign

`Campaign.cs` defines 30 deterministic slots:

- **3 tutorials** (4×4 to 5×6, almost all arrows free).
- **Three tiers of 9** that grow from 6×7 to 30×30. Arrows get longer and curvier, and plain rectangles alternate with silhouettes.
- Levels are **sorted by difficulty within each tier**. A global sort would put a 30×30 board right after a 6×6 one whenever the small board happens to be more constrained.

| Tier | Sizes | Arrows | Coverage | Difficulty score |
|---|---|---|---|---|
| Tutorial | 4×4 – 5×6 | 3 – 10 | 56 – 83 % | 1.5 – 2.9 |
| Early | 6×7 – 10×11 | 8 – 27 | 87 – 96 % | 4.6 – 9.1 |
| Mid | 10×12 – 15×17 | 24 – 47 | 94 – 99 % | 6.9 – 12.8 |
| Late | 16×16 – 30×30 | 40 – 151 | 94 – 97 % | 6.4 – 14.0 |

Generating the whole campaign takes about **140 ms**.

### Level Generator window

`Tools → Arrows → Level Generator`

- **Settings:** size, shape, fill ratio, min/max length, turn chance, seed (with a random button), candidates and target difficulty.
- **Preview:** open arrows are drawn solid, blocked ones faded and stuck ones red. The solution order can be shown as numbers.
- **Stats:** arrow count, coverage, solvable or not, average and minimum open arrows, difficulty score.
- **Buttons:** Save As, Load, **Repair**, Copy JSON, Generate 30-Level Campaign.
- **Edit Mode:** drag to draw an arrow from tail to head, right-click to delete, Shift + click to reverse.

---

## Game feel

| Event | Feedback |
|---|---|
| Level start | Arrows pop in with a ripple from the center (`OutBack`). |
| Tap | Thickness and brightness punch, tick sound, light haptic. |
| Exit | Ease-in slide along the path, shrink and fade, swoosh. **Combo:** quick successive exits rise in pitch. |
| Blocked | Run to the obstacle, elastic bounce back, both arrows flash red, camera shake, thud, heavy haptic. The heart pops and drains. |
| Hint | The arrow pulses and a dashed path flows toward the edge, with a chime. |
| Win / Fail | Confetti cannons, fanfare, success haptic / sad jingle, error haptic. Panels pop with an overshoot. |
| Buttons | Squash while held, tick, light haptic. |

Several arrows can move at the same time. A moving arrow ignores taps until its animation ends.

**Haptics:**
- iOS uses `UIImpactFeedbackGenerator` / `UINotificationFeedbackGenerator` through `Plugins/iOS/ArrowsHaptics.mm`.
- Android uses `Vibrator` + `VibrationEffect.createOneShot` with an amplitude per event (API 26+).
- In the editor and on desktop, haptics do nothing.

**Audio:** `AudioLibrary` maps sound ids to clips. Any id without a clip uses a generated placeholder, so the game has sound before real assets exist.

## Performance

- **No allocations in `Update`:**
  - Arrow polylines are sampled into preallocated buffers.
  - Mesh colors are written into reused arrays.
  - Gesture state lives in fields.
  - Timers are plain floats.
- **Components sleep when idle.** `ArrowView`, `UIPanel`, `HudView` and `ButtonPressScale` disable themselves when nothing animates.
- **Pooling:** `ObjectPool<ArrowView>` across levels.
- **Single-mesh dot grid:** one draw call. Hidden UI panels are deactivated.
- `Application.targetFrameRate = 60`, single scene, no runtime asset loading beyond the level `TextAsset`.

## Project structure

```
Assets/_Project/
├── Scripts/
│   ├── Model/            # pure C#: rules, solver, serialization
│   │   └── Generation/   # generator, shapes, repair, campaign
│   ├── Core/             # GameManager, InputHandler, LevelManager, ProgressService, FeedbackController, Haptics
│   │   └── Audio/        # AudioManager, AudioLibrary
│   ├── View/             # ArrowView, BoardView, CameraController, HintPathView, ConfettiEffect
│   ├── UI/               # UIManager, UIPanel, HudView, SettingsPanel, SafeArea, ButtonPressScale
│   ├── Utils/            # Easing, ProceduralSprites, ProceduralAudio
│   └── Editor/           # GameSceneBuilder, LevelGeneratorWindow, SpriteBaker
├── Tests/EditMode/       # NUnit tests
├── Levels/               # level_001 … level_030.json + LevelDatabase
├── Plugins/iOS/          # native haptics
├── Art/ · Audio/ · Scenes/
└── GameConfig.asset      # every tunable in one place
```

## Possible next steps

- **Bottleneck-aware generation** for harder late levels: prefer heads whose ray crosses many existing arrows, aiming for a lower average number of open arrows.
- Real SFX and music, plus a sprite or vector skin pass.
- PlayMode tests for the flow, and CI with [GameCI](https://game.ci) (tests + Android build).
- Analytics hooks (level start/fail/complete, hint usage) and ad/IAP integration points for hints and extra lives.

## Recording media

- **GIF:** use Unity Recorder (*Window → General → Recorder*) at 540×960 and 30 fps, then convert with `ffmpeg` or [ezgif](https://ezgif.com).
- **Files:** put them in `docs/media/` as `gameplay.gif`, `menu.png`, `gameplay.png`, `heart_30x30.png` and `generator.png`.

---

*Fan-made clone for learning and portfolio purposes. Not affiliated with the original developer. All code is original; all art and sound in this repository is procedurally generated.*
