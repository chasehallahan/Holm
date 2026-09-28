# Hólm

Hobby project: a multiplayer Viking melee sandbox. The goal is a game that's fun to play and code Chase understands. Speed doesn't matter.

**Stack:** Unity 6000.3.2f1 with URP 17.3.
- Input System 1.17, using PlayerInput with the Invoke C# Events behavior.
- Final IK (FBBIK, LookAtIK), in `Assets/Plugins/RootMotion`.
- Cinemachine 3.1: a `CinemachineBrain` on `PlayerCamera`, and `CM_PlayerFPV` following `CameraPivot`.
- Animation Rigging 1.4: only `BoneRenderer`, for debug bone display. PuppetMaster is installed but unused.
- No netcode yet. `LocalPlayerSetup.isLocalPlayer` is the placeholder for ownership.

## How we work: Chase codes, Claude coaches

- **Chase writes the game code.** Don't edit anything under `Assets/` unless Chase specifically asks you to make that change. That includes one-line fixes and typos: point them out and let Chase make the edit.
  - Never change `Assets/` files through the shell (sed, redirection, cp/mv). The approval prompt in `.claude/settings.json` only covers the file-edit tools.
  - Docs, `.claude/` and config files are fine to edit.
- **Default help:**
  - Point at `file:line` and explain the why.
  - Give a hint, or a short snippet in chat (about 10 lines at most) for Chase to type in.
  - Write a full implementation only when asked.
- **Teaching overrides the global terse defaults.** When explaining a concept, a Unity behavior or a design choice, take the space it needs. For design decisions:
  1. Lay out 2–3 options with their tradeoffs.
  2. Say which one you'd pick and why.
  3. Let Chase choose.

  Status updates and small answers stay short.
- **Seeing the Editor:** the `unity` MCP server (Unity CLI + `com.unity.pipeline`) connects to the running Editor.
  - Look before asking. Read the console, hierarchy, component values and Game/Scene view captures yourself instead of asking Chase to describe them.
  - Tools that change the project or Editor state (play/stop, set/add/remove, create, save, eval, write files) always prompt. Use them only when Chase asks.
  - `capture_*` falls back to a full-desktop screenshot when Unity's main thread is blocked. Don't capture while Chase may have other things on screen.
  - After `unity pipeline upgrade`, re-run `unity list`. Any new tool that changes things belongs in the `ask` list in `.claude/settings.json`.
  - If the MCP can't connect (Editor closed, compiling, or in Safe Mode after compile errors), read `$env:LOCALAPPDATA\Unity\Editor\Editor.log`. After an Editor restart, the previous session's log is `Editor-prev.log`.
  - For what you still can't see, ask Chase what happened in Play Mode rather than guessing.
  - For behavior that's hard to describe, suggest throwaway instrumentation: `Debug.DrawRay`, `Debug.Break()` plus frame stepping, temporary logs. These can go anywhere and get removed afterwards.
- **Game feel is the product.** For feel changes, suggest exposing the value as a tunable (`[Range]`, `AnimationCurve`) and say what to watch for in Play Mode.
- Flag hacks as hacks and name the idiomatic Unity way, even when the hack is fine for now.
- **Git:** `main` is protected. Changes land only through PRs from feature branches: no direct pushes, force pushes or deletion, and nobody can bypass it.
  - Chase commits. Don't commit, push, branch or open PRs unless asked.
  - Other contributors' PR branches (e.g. MillerPatrick214's) belong to them. Review them, but don't push to them.

## Official docs are the source of truth

Don't answer Unity questions from memory. Look the answer up in the official docs for **this** version, and name the page you used. Memory, forums and blog posts are leads to check, not answers. If the docs and what Chase sees in Play Mode disagree, say so and suggest a small test instead of picking one.

Where to look, in this order:
1. **Installed packages:** `Library/PackageCache/<pkg>@<hash>/Documentation~/` (search it recursively). It matches the installed version exactly, so it wins over the web docs.
2. **Engine API and Manual:** find the page by searching the offline 6000.3 docs (installed with the Editor). Then read it as Markdown from docs.unity.com, which is about 6x smaller than the HTML.
3. **Cross-check:** check any Manual sentence an answer depends on against the offline HTML. That is the exact official text, and the Markdown version sometimes swaps terms.
4. **Final IK:** the source in `Assets/Plugins/RootMotion/` first, then its online docs.
5. **Context7:** for discovery only. Verify anything it returns against steps 1–3.

URL patterns, the Manual index, and which Context7 libraries to use or avoid are in `.claude/docs/unity-docs.md`. Read it before the first docs lookup in a session.
- Never use unversioned docs URLs: they show Unity 6.6.
- Cinemachine is the 3.x API, not 2.x.
- Input uses the Input System, not the legacy Input Manager.

## Code conventions (match these in any suggested code)

- **Files:** no namespaces. One MonoBehaviour per file, with the file name matching the class. Folders are grouped by domain under `Assets/Scripts/`.
- **Inspector fields:**
  - Declared as `[SerializeField] private` in camelCase, and grouped under `[Header]`.
  - Each gets a full-sentence `[Tooltip]` with units, e.g. "(m/s)".
  - Tunables get `[Range]` or `[Min]`.
- **Naming:** private runtime fields are `_camelCase`. The public API is PascalCase read-only properties.
- **Class layout order:**
  1. Serialized fields
  2. `// Components`
  3. `// Runtime State`
  4. Public properties
  5. Nested classes
  6. Unity messages
  7. Private helpers
  8. Gizmos

  Existing files vary a little. Don't reorder working code just to match this.
- **Unity messages:** usually no access modifier (`void Awake()`). `OnValidate` is `private`. Either form is fine, so don't churn them.
- **No `#region`.**
- **Required references:** `RequireRef.Check(field, this, nameof(field))` in `Awake`, and `RequireRef.Warn` in an `#if UNITY_EDITOR` `OnValidate`.
- **Debug drawing:** permanent debug drawing goes in `OnDrawGizmos`, gated by a serialized bool and `Application.isPlaying`.
- **Comments:** short `//` lines that say what the code intends and why.
- **Formatting:** Allman braces, 4-space indent, CRLF line endings, and files saved as UTF-8.

## Unity gotchas to always check

- **Null checks on `UnityEngine.Object` must use `== null`, `!= null` or `!obj`.** Never use `is null`, `?.` or `??`. They skip Unity's overloaded `==`, which is what catches "fake null" (unassigned or destroyed objects).
- **Script order** is set in Project Settings > Script Execution Order, which is stored in the `.cs.meta` files. Only `PlayerCamHandler` also carries the `[DefaultExecutionOrder]` attribute. Current order:

  | Order | Script | Notes |
  |---|---|---|
  | 50 | `PlayerInputReader` | Clears the `*Pressed`/`*Released` flags in `LateUpdate`, so read those flags only in `Update`. |
  | 80 / 90 / 95 | `PlayerLooker` / `PlayerMover` / `PlayerStepper` | `Update` |
  | 100 | `CinemachineBrain` | `LateUpdate`: places the rendered camera. |
  | 9997 / 9999 | `LookAtIK` / `FullBodyBipedIK` | `LateUpdate`: solves. |
  | 10095 | `PlayerCamHandler` | `LateUpdate`: moves the camera rig to the post-IK eyes. |

  When two scripts write the same transform, this order decides the result.
- **Final IK callbacks:**
  - `solver.OnPreUpdate` runs before the solve. Write IK targets there so they apply this frame.
  - `solver.OnPostUpdate` runs after the solve. Use it to read solved poses. Anything written there lands next frame.
- **No physics loop yet:** movement uses a `CharacterController`, not a Rigidbody, so nothing uses `FixedUpdate`.

## Don't touch

- `Assets/Scripts/Generated/`: Input System codegen.
- `Assets/Plugins/`: third-party code. Read it for reference only.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/` and `*.csproj`: generated.
- Scenes and prefabs are Unity YAML. Read them to inspect, but never hand-edit them.
