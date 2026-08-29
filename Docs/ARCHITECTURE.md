# Architecture

Analysis and technical decisions for the project. This is the document to read
before touching the code.

Everything here is a decision with a reason attached. Where I recommended the
scalable option over the quick one, the trade-off is stated so you can overrule
it knowingly.

---

## A. Recommended technology stack

### Engine and version

**Unity 6 LTS** — use the current 6.x LTS patch shown in Unity Hub. I am not
naming a specific patch number here because I cannot verify from this machine
which one is current; check the Hub and prefer the newest LTS over any Tech
Stream release. The code in this repository compiles on Unity 2022.3 LTS as well
(see `Core/RigidbodyCompat.cs`), so a version change is not a trap.

### Render pipeline: HDRP — but not yet

**Recommendation: HDRP.** For a PC-only realistic driving game it is the correct
choice, and the gap is not small:

| What you need | HDRP | URP |
| --- | --- | --- |
| Car paint with a real clear coat | Built into HDRP/Lit | Custom shader work |
| Physical light units (lux, lumens, EV) | Yes | No |
| Volumetric fog and light shafts (tunnels, night) | Built in | Custom |
| Screen-space and ray-traced reflections | Built in | SSR only, limited |
| Area lights for the garage | Yes | No |
| Physically based camera (exposure, aperture) | Yes | Partial |
| Decal system for road markings and grime | Full | Limited |

URP can be pushed toward this, but the work is months of shader engineering that
is not your game.

**The cost is real** and you should know it before committing: HDRP is heavier
per frame, needs more care to hit 60 fps once traffic and a city exist, has
longer shader compile times, and has more ways to be configured wrongly.

**The important part: you do not have to decide yet.** Nothing in the vehicle
simulation — engine, gearbox, tyres, suspension, aero, save system, input,
cameras, HUD — touches a renderer API. Phases 1 through 7 are pipeline-agnostic.
You can build and tune the entire car in whatever pipeline the project opens
with, and convert at Phase 8 when you start the world. That is deliberate, and it
is why the prototype scene builder looks up `HDRP/Lit`, then `URP/Lit`, then
`Standard`, and works either way.

### Physics: PhysX, custom raycast suspension, custom tyre model

**Do not build on `WheelCollider`.** It is the default suggestion everywhere and
it is wrong for this project:

- It runs its own hidden sprung-mass integrator that you cannot inspect or
  correct, so the suspension you author is not quite the suspension you get.
- Its friction model is a two-point curve (extremum, asymptote). A real tyre
  curve is a slip-dependent function with a peak, a falloff, and load
  sensitivity. You cannot express that shape, so you cannot express the
  difference between a summer tyre and a semi-slick.
- It behaves poorly below walking pace and under combined slip — exactly the
  conditions of a parking manoeuvre and a corner exit.
- It does not expose slip ratio, normal load or wheel angular velocity in a form
  the drivetrain, ABS, traction control, dashboard and audio can share. Those
  systems all need the same numbers, and faking them separately is how a project
  ends up with a dashboard that disagrees with the physics.

The replacement is roughly 400 lines (`VehicleWheel` + `TireModel`) and gives you
every one of those quantities as a first-class value. Retrofitting it later would
mean re-tuning every car from scratch, which is why it is in from day one.

**Timestep: 200 Hz** (`Time.fixedDeltaTime = 0.005`). A stiff slip curve on a
50 Hz timestep overshoots within a step and oscillates. This is the single most
common reason a competent tyre model "feels bad" in Unity. `Tools/IndieGame >
Configure Project Settings` sets it.

### Input: Unity Input System, behind an abstraction

`IVehicleInputSource` returns a `VehicleInputState` struct. Physics never reads a
device. That one interface is what later makes a Logitech/Fanatec wheel, AI
traffic drivers, replays and network clients possible without touching the
vehicle.

Actions are currently built in code rather than from an `.inputactions` asset, so
the project runs the moment it is opened with nothing to wire up. When the
rebinding UI is built (Phase 10) an `InputActionAsset` slots in behind the same
interface.

### Cameras: hand-written now, Cinemachine optional later

The chase camera needs speed-driven FOV, acceleration-driven offset and a
collision probe, all reading vehicle telemetry. As a Cinemachine setup that is an
extension component plus an asset to configure; written directly it is one file
with no extra package. `VehicleCameraRig` is fully isolated — moving to
Cinemachine changes exactly one file.

### Save system: JSON files, not PlayerPrefs

Mileage, tuning and a list of owned cars are structured data. PlayerPrefs is a
flat string table with no atomicity and a registry-backed store on Windows.
`SaveSystem` writes JSON to `Application.persistentDataPath` through a temp file
and a swap, so a crash mid-write cannot corrupt the save. Encryption or a
checksum can be added inside `SaveSystem` later without any caller changing.

### Traffic (Phase 9): lane graph + LOD tiers

Not full physics per car. A directed lane graph (nodes, lanes, connections,
priorities), kinematic AI vehicles that follow it, and three simulation tiers:
near cars get simplified physics, mid cars get kinematic movement with collision
avoidance, far cars are just positions on the graph. Pooled, spawned and
despawned by distance.

### World streaming (Phase 8): Addressables + additive scene sectors

Cut the map into sectors as additive scenes, load by distance around the player,
with LOD groups and occlusion culling inside each. Terrain streaming only if you
end up with real terrain rather than authored road meshes.

---

## B. Project folder structure

```
Assets/
  Scripts/                        <- IndieGame.Runtime assembly
    Core/                         Units, math helpers, Unity version compat
    Input/                        Input abstraction + player input source
    Vehicle/
      Data/                       ScriptableObjects: VehicleDefinition, drive modes, enums
      VehicleController.cs        The one MonoBehaviour; owns and orders everything
      VehicleEngine.cs            Torque curve, inertia, boost, ignition
      VehicleTransmission.cs      Gears, shift schedule, clutch
      VehicleDrivetrain.cs        Axle split, differentials
      VehicleWheel.cs             Raycast suspension + tyre contact patch
      TireModel.cs                ITireModel + Pacejka implementation
      VehicleBrakes.cs            Bias, handbrake, ABS
      VehicleSteering.cs          Rack, speed sensitivity, Ackermann
      VehicleAerodynamics.cs      Drag, downforce
      VehicleStabilitySystems.cs  Traction control, ESC
      VehicleOdometer.cs          Validated distance accumulation
      VehicleFuelSystem.cs        Tank and consumption
      VehicleTelemetry.cs         Read-only snapshot for presentation
    World/                        Surface descriptors (roads, dirt, grass)
    Camera/                       VehicleCameraRig
    UI/                           Debug telemetry overlay
    Persistence/                  SaveSystem, save data types
  Editor/                         <- IndieGame.Editor assembly
    ProjectBootstrapTool.cs       Physics/time/layer settings from a menu
    PrototypeSceneBuilder.cs      Builds the test scene and vehicle from code
  Data/Vehicles/                  VehicleDefinition assets, one per car
  Data/DriveModes/                Shared drive mode presets (later)
  Prefabs/Vehicles/               Vehicle prefabs
  Prefabs/World/                  Road pieces, props
  Art/Vehicles|Environment|Materials/
  Audio/Vehicles/
  Scenes/
  Settings/                       Render pipeline assets, quality settings
Docs/                             This folder
Tools/                            Offline validation scripts
```

Two assembly definitions (`IndieGame.Runtime`, `IndieGame.Editor`) keep compile
times down and make it impossible to accidentally reference editor code from a
build.

Folders that will be needed later (`Traffic/`, `Garage/`, `Tuning/`) are not
created empty — they arrive with their phase.

---

## C. Main game architecture

Four layers, and data only flows one way:

```
  DATA                SIMULATION              STATE            PRESENTATION
  ────                ──────────              ─────            ────────────
  VehicleDefinition   VehicleController  ->  VehicleTelemetry  ->  HUD
  (ScriptableObject)    Engine                (read-only)          Cockpit gauges
  DriveModeSettings     Transmission                               Engine audio
  SaveData              Drivetrain                                 Exhaust VFX
       │                Wheels / Tyres                             Cameras
       └──── read ────> Brakes, Steering                                │
                        Aero, Stability                                 │
                              ▲                                         │
                              └──── IVehicleInputSource <───────────────┘
                                    (player, AI, replay, network)
```

**Rules this enforces:**

1. **Presentation cannot lie.** A gauge reads `VehicleTelemetry`. If the
   tachometer says 4,500 rpm, the engine's integrated angular velocity is
   4,500 rpm, because there is no other source for that number.
2. **No branching on vehicle identity.** There is no `if (carName == ...)`
   anywhere. Behaviour differences come from `VehicleDefinition` data.
3. **Input is a value, not a device.** Anything that can produce a
   `VehicleInputState` can drive a car.
4. **One tick order.** The whole car advances inside one `FixedUpdate`. Script
   execution order is not a variable.

**Communication:** direct references for the hot path (no `GetComponent` per
frame, no `GameObject.Find` ever), C# events for discrete moments the camera or
audio needs to react to (`CameraToggleRequested`, `DriveModeChanged`).

**Singletons:** one — `SaveSystem`, a static class. It is the only genuinely
global thing (a single save file on disk), and it holds no Unity object
references.

**Why subsystems are plain classes, not MonoBehaviours.** The spec listed them as
components, and I have deliberately built them as plain C# owned by a single
`VehicleController` instead. The reasons: guaranteed tick order, no per-component
lifecycle overhead across 4 wheels x N vehicles, unit-testable without a scene,
and a simulation state that is trivially serialisable for replays and networking.
They remain fully separate files with single responsibilities, so the modularity
you asked for is intact — you can replace `TireModel.cs` without opening any
other file. If you would rather have them as inspectable components, say so and
I will split them; it is a contained change.

---

## D. Vehicle physics architecture

### The chain

```
throttle pedal
   │ drive mode throttle map + response lag
   ▼
ENGINE   T = curve(rpm) x throttle x boost x tune,  minus friction
   │     integrated on its own inertia:  I_e * dω_e/dt = T - T_clutch
   ▼
CLUTCH   torque-limited coupling.
   │     required = engine torque + (ω_engine - ω_driveline) * stiffness
   │     transmitted = clamp(required, ±capacity)
   │     within capacity -> rigid: engine rpm IS wheel speed x gearing
   │     beyond capacity  -> slips: launches, dumped clutch, shift cut
   ▼
GEARBOX  x gear ratio x final drive x efficiency
   ▼
DIFFERENTIAL   open: equal torque.  LSD: bias toward the slower wheel.
   │           AWD additionally splits front/rear first.
   ▼
WHEEL    I_w * dω_w/dt = T_drive - F_x * r - T_brake - rolling resistance
   ▼
TYRE     slip ratio  κ = (ω_w r - v_long) / max(|v_long|, v_ref)
   │     slip angle  α = atan(v_lat / max(|v_long|, v_ref))
   │     relaxed over a relaxation length (transient tyre + numerical stability)
   │     F = μ(load) x F_z x Pacejka(slip),  combined via a friction circle
   ▼
RIGIDBODY   AddForceAtPosition at the contact patch
```

The car accelerates because a force was applied at four contact patches. No
script writes to `Rigidbody.velocity` to make the car move — the only velocity
writes in the codebase are `Teleport()` and the parking hold, both of which only
remove motion.

### Vertical load, which is what makes cars feel different

```
suspension raycast -> compression -> spring + damper + bump stop + anti-roll bar
                   -> normal force -> tyre load F_z
```

Load transfer is not scripted. Braking pitches the body forward on its springs,
which compresses the front struts, which raises front `F_z`, which raises front
grip — and because the tyre model has **load sensitivity** (grip coefficient
falls as load rises), the outside tyre in a corner gains less than the inside one
loses, so total grip drops. That single coefficient is what makes weight transfer
matter rather than being cosmetic.

### Step order inside one FixedUpdate

Order matters and is fixed in `VehicleController.FixedUpdate`:

1. Read input, apply discrete commands (ignition, shifts, drive mode)
2. Suspension: cast all four, apply anti-roll bars, then apply forces
   *(loads must be final before tyres are evaluated)*
3. Steering *(slip is measured in the steered frame)*
4. Tyre forces, applied to the rigidbody
5. Sample driveline speed from the driven wheels
6. Traction control trims driver throttle
7. Gearbox and clutch, then engine, then differential
8. Brakes, then ESC *(which may only add brake torque)*
9. Integrate wheel rotation
10. Aerodynamics
11. Parking hold, fuel, odometer, telemetry

### Assists have only the levers the real ones have

- **ABS** watches per-wheel slip ratio and modulates brake torque at ~12 Hz,
  disengaging below walking pace.
- **Traction control** reduces the throttle the engine is given. Nothing else.
- **ESC** applies brake torque to one wheel: inside rear when understeering,
  outside front when oversteering.

None of them can add grip, so a car past the limit still slides. Drifting is an
emergent consequence of rear tyre saturation, not a mode.

---

## E. Development roadmap

See `ROADMAP.md` for the phase breakdown and the definition of done for each
milestone.

---

## F. First playable prototype

See `ROADMAP.md` § "Milestone 1". In short: one car that is genuinely satisfying
to drive on a test surface, with working gauges, two real drive modes, manual and
automatic, engine audio, and an odometer that survives a restart. No city.

---

## G. Risks

**Tyre model tuning.** The model is in; making it *feel* right is the hard part
and it is subjective. Mitigation: the coefficients are per-car data with the
peak-slip formula documented, `Tools/validate_longitudinal.py` checks headline
numbers offline, and the debug overlay shows slip and saturation live. Budget
real time here — this is the difference between the project working and not.

**Low-speed and parked behaviour.** A raycast rig has no static friction, so a
stopped car creeps on a slope. Currently handled by an explicit parking hold.
Proper fix is a static-friction constraint per contact patch; noted as technical
debt.

**Open-world performance.** The classic killer. Mitigation is architectural and
listed in section A: sector streaming, LOD groups, pooling, traffic simulation
tiers. Do not build the city before the car is finished — a beautiful city around
a car that feels wrong is the worst possible position to be in.

**Traffic.** Realistic traffic that does not constantly crash is genuinely hard.
Plan for a lane graph with explicit priorities and gap acceptance, not
steering-behaviour AI. Start with a single roundabout and a single signalised
junction and make those perfect.

**Engine audio.** Pitch-shifting one loop always sounds like pitch-shifting one
loop. You need layered samples crossfaded on rpm *and* load, with separate
interior and exterior sets. This is a content problem more than a code problem —
budget for recording or licensing a proper sample set.

**Licensed vehicle models.** A downloadable model is not a licence to ship. See
`ASSETS.md` for the checklist that must be completed before any asset enters the
project. Assume every branded car is unusable commercially until proven
otherwise, and keep the fictional prototype cars as the fallback.

**Dashboards.** Working gauges need the needles to be separate transforms with
correct pivots. Most downloaded models have the cluster as one merged mesh. This
is a Blender task per car, and it is the most common reason a "working dashboard"
turns out not to work. Check before buying or committing to a model.

**Scaling to many cars.** Handled by `VehicleDefinition` being the only source of
per-car behaviour. The risk is discipline: the first time someone writes
`if (car.name == ...)` the property is lost. Treat that as a build-breaking
review comment.

---

## H. Recommended starting point

1. Follow `SETUP.md` end to end. It ends with you driving.
2. Drive the prototype for twenty minutes with the F3 overlay open. Watch slip
   ratio under acceleration, load under braking, rpm through a shift.
3. Compare what you feel against `VEHICLE_VALIDATION.md` and tell me what is
   wrong. "Turn-in is lazy", "it spins too easily in second", "the brakes feel
   wooden" are all actionable — they map to specific coefficients.
4. Then Phase 4 (cockpit and dashboard) and Phase 5 (audio), which is where the
   car stops being a physics test and starts being a car.

Do not start the open world until you would happily drive this car for an hour
on an empty road.
