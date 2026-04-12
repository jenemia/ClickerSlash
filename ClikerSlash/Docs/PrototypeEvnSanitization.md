# PrototypeEvn Strip Map

`PrototypeEvn` is a project-owned copy of `Assets/FactoryAsset/Scenes/urp_scene_with_training.unity`.
The source vendor scene remains reference-only, and `PrototypeEvn` is the additive environment scene used with `PrototypeBattle`.

## Keep Active

- `env`
- `ImmersiveTraining`
- `RobotParts`
- `mottoes`
- `reflectionProbe`
- `NavMesh_GO`
- existing passive colliders, markers, and baked-data carriers that do not own gameplay state

## Force Inactive

- `DistributionCenterScripts`
- `cam`
- `ManagementSuite`
- `StateManager_Training`
- `extraBakedLights`
- `audio`
- `Canvas`
- `EventSystem`
- `Directional Light`
- `stopTargets`
- `ModalCanvas`
- `FullscreenPanel`
- `assembly_instruction Variant`
- `CameraRT`
- `immersiveTraining_screen_prefab`
- `moto_bot_forassembly`

## Runtime Ownership Contract

- `PrototypeBattle` stays the active scene at runtime.
- `PrototypeEvn` is loaded additively and remains passive.
- ECS bootstrap, HUD, main camera, presentation bridge, and gameplay state stay owned by `PrototypeBattle`.
- If `PrototypeEvn` is missing from build settings, the battle scene continues without additive environment content and logs a warning.
