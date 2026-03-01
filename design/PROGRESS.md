# FlatRedBall2 — Implementation Progress

## Phase 1 — Core Value Types & Interfaces
- [x] `src/Math/Angle.cs`
- [x] `src/FrameTime.cs`
- [x] `src/IAttachable.cs`
- [x] `src/Rendering/IRenderable.cs`
- [x] `src/Rendering/IRenderBatch.cs`
- [x] `src/Rendering/Layer.cs`
- [x] `src/Collision/ICollidable.cs`

## Phase 2 — Input System
- [x] `src/Input/IKeyboard.cs`
- [x] `src/Input/ICursor.cs`
- [x] `src/Input/IGamepad.cs`
- [x] `src/Input/GamepadAxis.cs`
- [x] `src/Input/IInputDevice.cs`
- [x] `src/Input/I2DInput.cs`
- [x] `src/Input/IPressableInput.cs`
- [x] `src/Input/Keyboard.cs`
- [x] `src/Input/Cursor.cs`
- [x] `src/Input/Gamepad.cs`
- [x] `src/Input/KeyboardInput2D.cs`
- [x] `src/Input/KeyboardPressableInput.cs`
- [x] `src/Input/GamepadInput2D.cs`
- [x] `src/Input/GamepadPressableInput.cs`
- [x] `src/Input/InputManager.cs`

## Phase 3 — Service Layer
- [x] `src/TimeManager.cs`
- [x] `src/ContentManagerService.cs`
- [x] `src/Audio/AudioManager.cs` (stubbed)

## Phase 4 — Camera
- [x] `src/Rendering/Camera.cs`
- [x] `src/Rendering/Batches/WorldSpaceBatch.cs`
- [x] `src/Rendering/Batches/ScreenSpaceBatch.cs`

## Phase 5 — Entity
- [x] `src/Entity.cs`

## Phase 6 — Rendering Implementations
- [x] `src/Rendering/Sprite.cs`

## Phase 7 — Collision
- [x] `src/Collision/AxisAlignedRectangle.cs`
- [x] `src/Collision/Circle.cs`
- [x] `src/Collision/Polygon.cs`
- [x] `src/Collision/ShapeCollection.cs`
- [x] `src/Collision/CollisionDispatcher.cs`
- [x] `src/Collision/CollisionRelationship.cs`

## Phase 8 — Screen + Factory
- [x] `src/Screen.cs`
- [x] `src/Factory.cs`

## Phase 9 — Diagnostics
- [x] `src/Diagnostics/BatchBreakInfo.cs`
- [x] `src/Diagnostics/RenderDiagnostics.cs`
- [x] `src/Diagnostics/DebugRenderer.cs` (stubbed)

## Phase 10 — Root Service
- [x] `src/FlatRedBallService.cs`

## Phase 11 — Integration Stubs
- [x] `src/Gum/GumBatch.cs` (stubbed)
- [x] `src/Gum/GumRenderable.cs` (stubbed)
- [x] `src/Tiled/TiledMapLayerRenderable.cs` (stubbed)
- [x] `src/Tiled/TiledCollisionGenerator.cs` (stubbed)

## Phase 12 — Tracking Documents
- [x] `design/TODOS.md`
- [x] `design/PROGRESS.md`

## Phase 13 — Tests
- [x] `tests/FlatRedBall2.Tests/FlatRedBall2.Tests.csproj`
- [x] `tests/FlatRedBall2.Tests/Math/AngleTests.cs`
- [x] `tests/FlatRedBall2.Tests/PhysicsTests.cs`
- [x] `tests/FlatRedBall2.Tests/CollisionTests.cs`
- [x] `tests/FlatRedBall2.Tests/FactoryTests.cs`
- [x] `tests/FlatRedBall2.Tests/RenderListTests.cs`

## Verification
- [ ] `dotnet build src/FlatRedBall2.csproj` — no errors
- [ ] `dotnet test tests/FlatRedBall2.Tests/` — all pass
- [ ] Game1.cs wired to FlatRedBallService — window opens and runs
