# Code audit TODO

Chase makes the Unity and game-code changes. Check off an item after its change is verified.

- [ ] **1. Fix the build scene list.** In Unity Build Profiles, remove the missing `Assets/Scenes/SampleScene.unity` entry and add `Assets/Scenes/DemoWorldScene.unity` as the first enabled scene. Verify the scene appears in the build list and a build opens it. (`ProjectSettings/EditorBuildSettings.asset`)
- [ ] **2. Handle PlayerStepper while airborne.** Stop planted foot targets from anchoring the legs to the ground during a jump or fall, and replant cleanly on landing. Verify with a moving jump and a step off a ledge. (`Assets/Scripts/Character/Player/PlayerStepper.cs:310`)
- [ ] **3. Fix Unity object reference checks.** Use Unity-aware `== null` / `!= null` in `RequireRef.Check` and `RequireRef.Warn`, including destroyed or missing objects. (`Assets/Scripts/Utility/RequireRef.cs:20,36`)
- [ ] **4. Guard PlayerLooker IK setup.** Check that `LookAtIK`, its head, and at least one assigned eye exist before dereferencing them; fail with a useful error if the rig is incomplete. (`Assets/Scripts/Character/Player/PlayerLooker.cs:84-89`)
- [ ] **5. Make InteractPressed match the Hold interaction.** Set it when the hold is performed, if interaction is intended to require a completed hold. Verify a short tap does nothing and a full hold triggers once. (`Assets/Scripts/Input/PlayerInputReader.cs:74-76`)
- [ ] **6. Correct the PlayerStepper validation typo.** Validate `_playerMover` after fetching it, instead of checking `_fbbik` twice. (`Assets/Scripts/Character/Player/PlayerStepper.cs:224-225`)
- [ ] **7. Check PlayerLooker gaze timing.** `OnPostUpdate` moves the target after Final IK solves, so the new target is consumed on the next solve. In Play Mode, test fast turns for visible head-gaze lag. If needed, set the aim target before the solve using the camera pivot, and compare feel. (`Assets/Scripts/Character/Player/PlayerLooker.cs:111,167-174`)
