# Status

Updated at the end of each phase. See `ROADMAP.md` for what each phase means.

---

## COMPLETED

### Foundation
- Project structure, two assembly definitions, Unity 6 / 2022.3 compatibility shim
- One-click project settings tool (200 Hz timestep, solver iterations, layers)
- One-click prototype scene builder — scene, vehicle rig, camera, overlay from code
- JSON save system with atomic writes and a version field

### Data
- `VehicleDefinition` ScriptableObject covering chassis, engine, transmission,
  drivetrain, wheels, tyres, per-axle suspension, brakes, steering, aero,
  electronics and fuel
- `DriveModeSettings` with 14 real mechanical/electronic overrides per mode
- Derived garage figures: peak power, peak torque, rpm at speed in gear

### Physics
- Engine: normalised torque curve x throttle x boost x tune, real rotating
  inertia, friction and engine braking, idle governor, rev limiter with fuel cut,
  ignition state machine with a cranking starter, stall detection
- Turbo: rpm- and load-dependent spool with separate build and decay rates,
  blow-off detection, supercharger variant
- Clutch: torque-limited coupling. Within capacity the driveline is rigid and rpm
  genuinely equals wheel speed through the gearing; beyond it, it slips. Slipping
  torque tracks engine output, so launches are modulated and traction control has
  authority
- Gearbox: load-sensitive automatic schedule, kickdown, downshift over-rev
  protection, hysteresis, real torque cut during shifts, manual mode
- Differentials: open, limited-slip (separate power and coast ramps plus
  preload), locked; front/rear split for AWD
- Suspension: raycast strut, asymmetric compression/rebound damping, progressive
  bump stops, anti-roll bars, damper velocity clamp
- Tyres: Pacejka magic formula, load sensitivity, friction-circle combined slip,
  relaxation-length transient, surface friction scaling
- Brakes: front/rear bias, adjustable, separate handbrake circuit, per-wheel ABS
  at 12 Hz that disengages below walking pace
- Steering: rate-limited rack, speed-sensitive assist, Ackermann geometry, full
  steering-wheel lock output for the cockpit
- Aerodynamics: v² drag and per-axle downforce. Top speed is emergent
- Traction control (throttle only) and ESC (single-wheel braking only)
- Odometer: per-vehicle, validated against teleports and airtime, persisted
- Fuel: consumption derived from actual engine work

### Presentation
- Camera rig: chase with velocity-aligned follow, speed FOV, collision probe and
  lateral offset; cockpit with subtle acceleration motion; hood; bumper
- Debug telemetry overlay (F3) showing every simulation quantity

### Verification
- `Tools/validate_longitudinal.py` reproduces the C# equations offline and checks
  power, torque, gearing, launch traction, 0–100, 0–200 and top speed

---

## IN PROGRESS

Nothing. Phase 2 is complete and awaiting your subjective sign-off in the editor.

---

## TODO

Next, in order:

1. **Your feedback on how the car feels.** Everything below is downstream of it.
2. Phase 3 — clutch/stall, H-pattern, rev matching, torque converter
3. Phase 4 — cockpit gauges, warning lights, exterior lighting
4. Phase 5 — layered audio, pops and bangs, exhaust flames
5. Phase 7 — tuning UI and dyno over the multipliers that already exist

---

## BUGS

None known. **Nothing in this repository has been compiled or run** — there is no
Unity installation on the machine it was written on. The C# is structurally
checked and the physics is numerically validated, but the first Unity compile may
surface typos. `SETUP.md` § 7 covers the likely ones.

---

## TECHNICAL DEBT

Things simplified on purpose. Each names what would replace it.

| Item | Current | Proper fix | When |
| --- | --- | --- | --- |
| Static friction | A parking hold force damps horizontal velocity when stopped on the brake | Per-contact static friction constraint | When hill starts feel wrong |
| Suspension contact | Single downward raycast | Sphere cast, or multi-ray for kerb strikes | Phase 8, when kerbs exist |
| Locked differential | Very stiff LSD approximation | Solve both wheels as one inertia | If a locked-diff car feels wrong |
| Stalling | Only below `StallRpm` with the clutch engaged; cannot stall by dumping the clutch at idle | Full clutch-pedal engagement model | Phase 3 |
| Tyre transient | Relaxation length on slip only | Separate longitudinal/lateral relaxation, plus temperature and wear | After Milestone 1 |
| Input bindings | Built in code | `.inputactions` asset + rebinding UI | Phase 10 |
| Drive mode assets | Embedded in each `VehicleDefinition` | Shared `DriveModeSettings` ScriptableObjects | When two cars need the same mode |
| Aero | Fixed coefficients | Ride-height and yaw dependence | Only if it becomes noticeable |
| Refuelling | `Fuel.FillTank()` from code | Gas stations | Phase 8 |
| Body collider | One box | Convex hull matching the real body | Phase 8 |

---

## DECISIONS TAKEN

Recorded so they are not silently reversed.

| Decision | Reason | Reversible? |
| --- | --- | --- |
| Custom raycast suspension, not `WheelCollider` | WheelCollider's hidden integrator and two-point friction curve cannot express a real tyre, and hides the values every other system needs | Expensive — every car would need re-tuning |
| Subsystems are plain C# classes owned by one MonoBehaviour | Deterministic tick order, testable, serialisable for replay/network | Cheap |
| 200 Hz physics | Stiff slip curves oscillate at 50 Hz | Cheap; relax to 100 Hz when traffic arrives |
| `PeakTorqueNm` means peak **at full boost** | Manufacturers publish the boosted figure; this way you type what the brochure says | Cheap |
| HDRP recommended but deferred to Phase 8 | No vehicle code touches a renderer, so the decision costs nothing to delay | Free |
| JSON save, not PlayerPrefs | Structured data, atomic writes | Cheap |
| Camera written directly, not Cinemachine | Fewer moving parts for a rig that reads telemetry | Cheap — one file |
