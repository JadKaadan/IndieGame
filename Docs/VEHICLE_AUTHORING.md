# Adding a vehicle

Adding car number 50 must not require writing code. It does not: a car is one
`VehicleDefinition` asset plus one prefab.

---

## The short version

1. Import the model, check scale and orientation
2. **Assets > Create > IndieGame > Vehicle Definition**
3. Fill in the definition from real data (see § Research below)
4. Build the prefab: rigidbody, body collider, four suspension anchors, four
   wheel transforms, camera anchors
5. Add `VehicleController`, assign the definition, fill the wheel array
6. Add `PlayerVehicleInputSource`
7. Run the validation protocol in `VEHICLE_VALIDATION.md`

No new classes. No new components. No `if (carName == ...)`.

---

## Conventions the rig must follow

**Root transform.** Vehicle prefab roots sit at **ground level**, centred between
the wheels, facing **+Z**. All definition offsets are measured from there. Get
this wrong and the centre of mass will be wrong, and everything else follows.

**Centre of mass.** `Chassis > Centre Of Mass Offset` is the authoritative value.
Y is height above the road: roughly 0.42–0.55 m for a car, 0.65–0.80 m for an
SUV. Getting this too high is the most common cause of a car that rolls like a
boat and flips over kerbs.

Use `SuggestedCentreOfMassZ()` to convert a published front/rear weight
distribution into the Z offset.

**Suspension anchor placement.** This is the step people get wrong.

The anchor is the *top of the strut*. The ray is cast down from it. So:

```
anchor height above ground = wheel radius + (rest length − static compression)

static compression = corner weight (N) / spring rate (N/m)
corner weight      = mass × 9.81 × axle weight share × 0.5
```

Worked example, front axle of the prototype car:

```
corner weight      = 1520 × 9.81 × 0.52 × 0.5 = 3,877 N
static compression = 3,877 / 40,000            = 0.097 m
anchor height      = 0.34 + (0.30 − 0.097)     = 0.543 m
```

Place the anchor at Y = 0.543 and the car sits at exactly its design ride height
on spawn, with no drop and no bounce.

`PrototypeSceneBuilder.CreateWheel` does this arithmetic — read it if the formula
is unclear.

**Wheel meshes.** The transform assigned to `Visual Wheel` is positioned and
rotated by the simulation. Its **local X axis must be the axle.** If the model's
wheel spins about the wrong axis, wrap it in an empty parent with the correction
rotation and assign the parent.

**Lateral sign.** `-1` for left-hand wheels, `+1` for right. This drives Ackermann
and anti-roll bar pairing. Getting it backwards makes the car steer into corners
wrongly at low speed.

**Layers.** The vehicle and everything under it goes on the `Vehicle` layer, and
`Wheels > Ground Mask` must exclude it. If the suspension ray hits the car's own
body collider, the car will hover, sink or launch.

**Body collider.** Must not reach the ground. The wheels carry the car; a body
collider touching the road fights the suspension.

---

## Filling in the definition

Ordered by how much each one changes the feel. If you are short on data, get the
top of this list right first.

| Priority | Field | Why |
| --- | --- | --- |
| 1 | Mass, centre of mass | Sets everything else |
| 2 | Torque curve, peak torque, redline | The engine's character |
| 3 | Gear ratios, final drive | How the power is delivered |
| 4 | Wheel radius | Wrong radius corrupts speed, rpm and gearing at once |
| 5 | Spring rates, damping | Body control and load transfer |
| 6 | Tyre μ and Pacejka coefficients | Where the limit is |
| 7 | Max steer angle, speed sensitivity | How it turns in |
| 8 | Cd, frontal area | Top speed |
| 9 | Brake torques | Stopping distance |
| 10 | Drive modes | The differences between them |

### Torque curve

X is `rpm / redline` (0–1), Y is fraction of `PeakTorqueNm`.

**`PeakTorqueNm` is the peak at full boost** — the figure a manufacturer
publishes. `BoostTorqueGain` then says how much of it depends on boost: 0.62
means the engine makes `1/(1+0.62)` = 62% of peak when off boost. Type what the
brochure says.

Shapes worth knowing:

- **Turbo:** rises steeply from ~1,500 rpm, broad flat plateau to ~70% of
  redline, then tapers
- **Naturally aspirated:** smooth arch peaking around 60–75% of redline
- **Supercharged:** near-peak from very low rpm, gently falling
- **Diesel:** very early, very narrow, drops hard past ~55% of redline

### Tyre coefficients

The magic formula peaks at `tan(π / 2C) / B`. Useful targets:

- Longitudinal peak at slip ratio ≈ 0.10–0.15 → B ≈ 11, C ≈ 1.65
- Lateral peak at slip angle ≈ 7–10° (0.12–0.17 rad) → B ≈ 14, C ≈ 1.35

Peak friction coefficient: ~0.85 economy, ~1.05 all-season, ~1.15 performance
summer, ~1.35 track, ~1.5+ semi-slick.

`Load Sensitivity` is what makes weight transfer matter. Set `Nominal Load N` to
the static corner weight (`mass × 9.81 / 4`) so the curve is centred on normal
driving.

### Spring rates

Aim for 60–120 mm of static compression:

```
spring rate = corner weight / desired compression
```

Softer than 60 mm gives a wallowy road car; stiffer than 120 mm gives a car that
skates over bumps.

Damping: critical damping is `2 × √(rate × corner mass)`. Road cars run roughly
0.3–0.5 of critical in compression and 0.5–0.7 in rebound. Rebound is always
stiffer than compression.

---

## Research (§100–105 of the brief)

When modelling a **real** vehicle:

1. **Research first, author second.** Manufacturer press kits, technical
   specifications, owner and workshop manuals, established testing organisations.
2. **Verify important figures against two sources.** Gear ratios and kerb mass
   especially — enthusiast sites copy each other's errors.
3. **Never invent a specification.** If a value cannot be verified, write
   `"This value could not be reliably verified"` in
   `Identity > Specification Source`, propose an approximation and label it as one.
4. **Record every source** in `Identity > Specification Source`. Anyone should be
   able to audit where a number came from.
5. **Research the drive modes too.** What Sport actually changes differs by
   manufacturer: throttle mapping, shift points, damper valving, steering
   assistance, differential preload, exhaust valve position, ESC threshold. Use
   the real behaviour rather than a generic preset.

Two cars with the same power do not drive the same. Mass, drivetrain, torque
delivery, turbo lag, gearing, wheelbase, tyre size, differential and centre of
mass all differ, and all of them are fields in the definition.

---

## Interior and dashboard

For gauges to work, these must be **separate transforms with correct pivots**:

- `SpeedometerNeedle` — pivot at the needle's centre of rotation
- `TachometerNeedle`
- `FuelNeedle`, `TemperatureNeedle` (optional)
- `SteeringWheel` — pivot on the column axis
- `GearSelector` (optional)

Most downloadable models ship the instrument cluster as one merged mesh. Check
before committing to a model — this is the most common reason a "working
dashboard" turns out not to work.

Separating them in Blender: select the needle faces in Edit Mode, `P > Selection`
to split into its own object, then `Object > Set Origin > Origin to 3D Cursor`
with the cursor placed at the pivot. Export as FBX with `+Y up`, `-Z forward` and
apply scale.
