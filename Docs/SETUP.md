# Setup

Written for someone who is new to Unity. Follow it in order; it ends with you
driving the car.

Expected total time: 15–25 minutes, most of it Unity importing packages.

---

## 1. Install Unity

1. Install **Unity Hub** from unity.com.
2. In Hub, go to **Installs > Install Editor** and pick the newest **Unity 6 LTS**
   version offered. (Prefer LTS over a Tech Stream release.)
3. In the modules list, tick **Windows Build Support (IL2CPP)**. Everything else
   is optional for now.

The project also compiles on Unity 2022.3 LTS if that is what you already have.

---

## 2. Open this repository as the Unity project

This repository *is* the Unity project — `Assets/`, `Docs/` and `Tools/` are the
whole thing. Unity generates `Library/`, `ProjectSettings/` and `Packages/` on
first open; those are either ignored by git or created for you.

1. Clone or download this repository.
2. In Unity Hub: **Add > Add project from disk**, and select the repository
   folder (the one containing `Assets`).
3. Click the project to open it.

The first open takes several minutes while Unity imports. **You will see compile
errors at this point** — that is expected, and step 3 fixes them.

---

## 3. Install the Input System package

The one required package.

1. **Window > Package Manager**
2. Top-left dropdown: **Unity Registry**
3. Search for **Input System**, select it, click **Install**
4. Unity asks to enable the new input backend and restart. Click **Yes**.

After the restart the console should be clean.

> **If you still see** `The type or namespace name 'InputSystem' does not exist`
> or `Assembly reference 'Unity.InputSystem' not found` — the package did not
> install. Repeat this step. The project cannot compile without it.

> **If you would rather use the old input manager**, remove `"Unity.InputSystem"`
> from the `references` array in `Assets/Scripts/IndieGame.Runtime.asmdef`.
> `PlayerVehicleInputSource` has a legacy fallback and will still work.

---

## 4. Configure the project settings

**Tools > IndieGame > Configure Project Settings**

This sets:

- **Fixed Timestep to 0.005 s (200 Hz).** Important. The tyre model oscillates on
  Unity's 50 Hz default. If the car ever feels like it is vibrating, check this
  first.
- Physics solver iterations to 12 / 6
- The layers `Vehicle`, `Wheel`, `Ground`, `Environment`, `Traffic`

The console prints what it changed. You only need to do this once.

---

## 5. Build the prototype scene

**Tools > IndieGame > Build Prototype Test Scene**

This creates, from code:

- `Assets/Data/Vehicles/MeridianGTS.asset` — the vehicle's engineering data
- `Assets/Scenes/Prototype_TestTrack.unity` — a test surface with a 1.4 km
  straight, 100 m distance markers, a 6% incline for hill starts, a crest and dip
  for the dampers, and kerbs
- A fully wired vehicle: rigidbody, four raycast suspension corners, wheel
  visuals, camera anchors, input source, controller
- A camera rig and the debug overlay

The console prints the car's peak power and torque when the definition is
created.

> The scene is built from primitives on purpose. It is a **physics test mule**,
> not the art direction — it exists so suspension and tyre behaviour can be
> measured before any model exists. Real geometry arrives in Phase 8.

---

## 6. Drive

Press **Play**.

| Key | Action |
| --- | --- |
| `W` / `S` | Throttle / brake |
| `A` / `D` | Steer |
| `Space` | Handbrake |
| `E` / `Q` | Shift up / down |
| `T` | Toggle automatic / manual |
| `M` | Cycle drive mode (Comfort / Sport) |
| `C` | Cycle camera (chase / cockpit / hood / bumper) |
| `I` | Start / stop engine |
| `Left Shift` | Clutch |
| `F3` | Toggle the telemetry overlay |

Gamepad: right trigger throttle, left trigger brake, left stick steer, shoulder
buttons shift, B handbrake, Y camera.

### What you should see

- The car sits at its ride height immediately, without dropping or bouncing.
- Idle around **750 rpm**.
- Full throttle from rest in Sport: wheelspin, traction control cutting in,
  **0–100 km/h in roughly 5.5 s**.
- Shifts drop rpm by the ratio step, not to a fixed number.
- Under braking the nose dives; in a corner the body rolls and the inside rear
  unloads.
- Top speed levels off near **270 km/h** in 6th because drag has caught up with
  the available drive force — nothing clamps it.
- `M` to Sport: the pedal gets noticeably sharper and gears are held ~1,100 rpm
  longer. Both are visible in the overlay.
- The odometer counts up. Stop play, press Play again — **it carries over.**

---

## 7. Common problems

| Symptom | Cause | Fix |
| --- | --- | --- |
| Compile errors about `InputSystem` | Package not installed | Step 3 |
| Car vibrates or jitters at rest | Fixed Timestep still 0.02 | Step 4 |
| Car falls through the ground | `Ground Mask` on the definition excludes the ground layer | Select `MeridianGTS.asset`, set **Wheels > Ground Mask** to Everything except `Vehicle` |
| Car sinks into the road or floats | Suspension anchors at the wrong height | See `VEHICLE_AUTHORING.md` § anchor placement |
| Nothing responds to keys | No input source on the vehicle | Add **IndieGame > Input > Player Vehicle Input Source** to the vehicle root and assign it to the controller's *Input Source* field |
| `MissingReferenceException` on play | Definition not assigned | Assign the `VehicleDefinition` on the `VehicleController` |
| Car understeers into a wall and will not turn | Speed-sensitive steering at very high speed, working as intended | Reduce **Steering > Speed Sensitivity** curve values, or switch to Sport (lower assist) |
| Wheels spin visually but the car does not move | Ground has no collider, or `Ground Mask` is wrong | Check both |
| Engine dies and will not restart | Fuel tank empty | Call `Fuel.FillTank()` or set `FuelLitres` in the save file. Gas stations arrive in Phase 8 |

### Resetting

- **Delete the save** (mileage, tuning, preferences): delete
  `indiegame_save.json` from `Application.persistentDataPath`. On Windows that is
  `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\`. Or call
  `SaveSystem.DeleteSave()`.
- **Rebuild the scene**: run the menu item again. It overwrites the scene but
  keeps the existing vehicle definition, so your tuning survives.
