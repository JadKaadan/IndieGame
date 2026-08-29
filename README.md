# IndieGame — realistic open-world driving

A realistic free-roam driving game built in Unity. Simulation-leaning vehicle
physics, real drivetrains, working dashboards, tuning that changes the car rather
than a number on a menu, and eventually an open world with traffic.

**Status:** Playable. Open `Assets/Scenes/VehicleTest.unity` and press Play.

---

## Start here

| Document | What it covers |
| --- | --- |
| **[Docs/SETUP.md](Docs/SETUP.md)** | Get it running and driving. Start here. |
| [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md) | Technology stack, structure, physics chain, risks |
| [Docs/ROADMAP.md](Docs/ROADMAP.md) | Phases and definitions of done |
| [Docs/STATUS.md](Docs/STATUS.md) | What works, what is next, known debt |
| [Docs/VEHICLE_AUTHORING.md](Docs/VEHICLE_AUTHORING.md) | How to add a car |
| [Docs/VEHICLE_VALIDATION.md](Docs/VEHICLE_VALIDATION.md) | Measured behaviour against specification |
| [Docs/ASSETS.md](Docs/ASSETS.md) | Asset register and licensing rules |

1. Install the **Input System** package (Window > Package Manager > Unity Registry)
2. Open `Assets/Scenes/VehicleTest.unity` — the editor opens it automatically on a
   fresh session
3. Press Play

If the scene is ever missing or damaged:
**Tools > Indie Driving Game > Build Playable Prototype** regenerates it.

---

## What is actually simulated

Not a car controller with a speed variable. The car accelerates because forces
are applied at four contact patches.

- **Engine** — normalised torque curve, real rotating inertia, friction and
  engine braking, idle governor, rev limiter with fuel cut, cranking starter,
  stall detection
- **Turbo** — rpm- and load-dependent spool with separate build and decay rates,
  blow-off detection; supercharger variant
- **Clutch** — a torque-limited coupling. Within capacity the driveline is rigid,
  so engine rpm genuinely *is* wheel speed through the gearing. Beyond capacity
  it slips, which is what a launch and a dumped clutch are
- **Gearbox** — load-sensitive automatic schedule, kickdown, over-rev protection,
  real torque cut during shifts, manual mode
- **Differentials** — open, limited-slip with separate power and coast ramps,
  locked; front/rear split for AWD
- **Suspension** — raycast struts, asymmetric damping, progressive bump stops,
  anti-roll bars
- **Tyres** — Pacejka magic formula with load sensitivity, friction-circle
  combined slip, and a relaxation-length transient
- **Brakes** — front/rear bias, separate handbrake circuit, per-wheel ABS at 12 Hz
- **Steering** — rate-limited rack, speed-sensitive assist, Ackermann geometry
- **Aerodynamics** — v² drag and per-axle downforce; top speed is emergent
- **Electronics** — traction control (throttle only), ESC (single-wheel braking
  only). Neither can add grip, so the car still slides
- **Odometer** — per-vehicle, validated against teleports and airtime, persisted
  across sessions

Verified numerically before ever opening Unity — see
[Docs/VEHICLE_VALIDATION.md](Docs/VEHICLE_VALIDATION.md).

---

## The rules this codebase follows

1. **Nothing is faked.** If the tachometer says 4,500 rpm, the engine's
   integrated angular velocity is 4,500 rpm. If a tune adds torque, the torque
   curve changed.
2. **No branching on vehicle identity.** There is no `if (carName == ...)`.
   Behaviour comes from `VehicleDefinition` data.
3. **Input is a value, not a device.** Anything producing a `VehicleInputState`
   can drive a car — player, AI, replay, network client.
4. **Presentation reads state, never drives it.** Everything visible reads
   `VehicleTelemetry`.
5. **The vehicle is the foundation.** No city before the car is right.

---

## Controls

`W`/`S` throttle and brake · `A`/`D` steer · `Space` handbrake · `R`/`Q` shift up/down ·
`M` manual/automatic · `B` drive mode · `V` camera · `E` ignition · `L` headlights ·
`Left Shift` clutch · `U` units · `F3` telemetry overlay

Gamepad supported. Wheel support is designed for but not yet implemented.

---

## Requirements

- Unity 6 LTS (or 2022.3 LTS)
- Input System package
- Any render pipeline — nothing in the vehicle code touches a renderer, which is
  why the HDRP decision is deferred to Phase 8
