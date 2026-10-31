# rayonixdiwali

Unity 6000.3 WebAR projects on the Imagine WebAR World Tracker, published to
GitHub Pages from the `webar` folder.

| Scene | What it is | Web path |
| ----- | ---------- | -------- |
| `Assets/Scenes/ARTest.unity` | Diwali diya on the floor | `webar/unity/` |
| `Assets/Scenes/RoomConfigurator.unity` | Room configurator: TV, chair, table | `webar/room/` |

## Room configurator

Place a TV, dining chairs and a table around one spot on the floor, then move,
turn, resize, duplicate, delete and undo with on-screen buttons.

### Build

In the Unity editor:

1. `Rayonix AR / 1. Configure Project (URP + WebGL)` (once, as for the diya scene)
2. `Rayonix AR / Room Configurator / 1. Create Scene`
3. `Rayonix AR / Room Configurator / 2. Build WebGL`

Or close the editor and run:

```powershell
.\build-room.ps1
```

The build lands in `Builds/RoomWebGL` and is copied to `webar/room`. Pushing
`webar/**` to `main` or `webtest` deploys it with the Pages workflow.

Step 1 of the menu regenerates everything under `Assets/RayonixAR/Room`
(textures drawn in code, materials, the three prefabs) and the scene, so it is
safe to run again after changing `RoomConfiguratorSetup.cs`.

### How it works

| File | Role |
| ---- | ---- |
| `Room/Scripts/RoomConfigurator.cs` | Spawning, selection, button actions, undo, UI state |
| `Room/Scripts/RoomItem.cs` | Marks a catalogue item: kind, label, wall or floor, bounds |
| `Room/Scripts/UiMaxWidth.cs` | Keeps the bottom panels phone- and tablet-friendly |
| `Room/Editor/RoomConfiguratorSetup.cs` | Generates models, scene, UI; WebGL build |

- Items are children of the tracker's `MainObject`, because Imagine's 6DOF
  mode tracks the camera around that one placed origin and expresses depth by
  scaling `MainObject`. Positions are kept in that local space.
- Selection is a short single-finger tap. Touches on the outer 10% of the
  screen (thumbs holding a tablet), multi-finger touches and touches on UI are
  ignored. Imagine's whole-scene pan, pinch and swipe gestures are removed, so
  holding and moving the device never moves items.
- The project's `TrackingSmoother` and `PlaceOriginAtIndicator` are used as in
  the diya scene.

### Limitations of this tracker

Imagine World Tracker runs its own camera tracking in the browser (OpenCV), not
ARCore. Its 6DOF mode follows the image around the placed origin, so:

- Items close to the origin, such as a table with chairs, track best.
- Items more than about 1.5 m from the origin, such as a TV on a far wall, are
  tracked less steadily. The app says so when one is selected.
- There is no wall or plane detection; a TV is placed at 1.2 m height and
  pushed to the wall with the Farther button.

For a TV on a wall across the room, the WebXR + ARCore version in the separate
`webar` project tracks the whole room and is steadier on Android.
