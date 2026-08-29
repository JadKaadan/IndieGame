# Roadmap

Phases are sequential. Each has a **definition of done** — a thing you can check,
not a feeling. Do not start the next phase until the current one passes.

The governing rule: **the vehicle is the foundation.** A beautiful city around a
car that feels wrong is the worst position this project could end up in.

---

## Milestone 1 — One genuinely satisfying car

Phases 1–7. This is the real first goal. No open world.

**Done when** you would happily drive this car for an hour on an empty road.

---

### Phase 1 — Foundation ✅ COMPLETE

Project structure, assembly definitions, input abstraction, data-driven vehicle
definition, test scene, camera, debug overlay.

**Done when:** one vehicle drives, steers, brakes and stops. ✅

---

### Phase 2 — Vehicle physics ✅ COMPLETE

Engine with a torque curve and real inertia, torque-limited clutch, gearbox,
differentials, raycast suspension with anti-roll bars and bump stops, Pacejka
tyres with load sensitivity and relaxation, brakes with ABS, steering with
Ackermann and speed sensitivity, aerodynamics, traction and stability control.

**Done when:** acceleration, top speed and gearing match the design targets in
`VEHICLE_VALIDATION.md`, weight transfer is visible, and a rear-drive car can be
made to oversteer with throttle alone. ✅ (validated offline; needs your
subjective sign-off in the editor)

---

### Phase 3 — Transmission refinement 🔜 NEXT

- Real clutch pedal behaviour, including stalling when you dump it at idle
- H-pattern shifter support through `VehicleInputState.RequestedGear`
- Rev matching on downshifts, configurable per gearbox type
- Torque converter model for cars with a conventional automatic
- Shift feel: torque cut shape, ignition cut on upshifts for the DCT

**Done when:** a manual car can be stalled, launched properly, and heel-toed; and
an automatic, a DCT and a manual feel like three different gearboxes.

---

### Phase 4 — Cockpit and dashboard

- Analogue speedometer and tachometer needles driven from telemetry
- Gear indicator, odometer and trip display, drive mode indicator
- Steering wheel animation to the full configured lock
- Ignition sequence with a gauge sweep
- Warning lights: ABS, traction control, handbrake, fuel, engine
- Indicators, headlights, hazards, and their exterior lights

**Done when:** every needle and light on the cluster is driven by
`VehicleTelemetry` and none of them are decorative.

---

### Phase 5 — Audio

- Layered engine samples crossfaded on rpm **and** load, separate interior and
  exterior sets
- Exhaust with a valve state driven by the drive mode
- Turbo spool and blow-off, triggered by `Engine.BlowOffTriggered`
- Transmission shift sounds
- Tyre roll, scrub and squeal driven by slip and by surface type
- Overrun pops and bangs, gated on the conditions already computed
  (`Engine.OnOverrun`, drive mode intensity, exhaust configuration)
- Wind noise on speed

**Done when:** you can tell the gear, the load and the surface with your eyes
closed.

---

### Phase 6 — Drive mode expansion

Comfort and Sport already have real mechanical differences. Add Eco, Sport+ and
Individual, and wire adaptive damper changes to visible body control.

**Done when:** each mode is distinguishable by feel without looking at the HUD.

---

### Phase 7 — Tuning and the garage

- Tuning UI over the multipliers that already exist in `VehicleSaveData`
- Dyno screen: power and torque against rpm, redrawn after every change
- Garage stats: power, torque, weight, 0–100, top speed, drivetrain, mileage
- Studio lighting, reflective floor, orbit camera, engine revving

**Done when:** a tune visibly changes the dyno curve *and* the measured 0–100,
and nothing in the UI reports a number the physics does not produce.

---

## Milestone 2 — A world worth driving in

### Phase 8 — Open world

Road meshes with correct lane widths and markings, junctions, roundabouts,
tunnels, a highway, a mountain road, an industrial area, a city core, a garage
and gas stations. Sector streaming through Addressables. Day/night cycle.
HDRP conversion happens here.

**Density over scale.** A small dense map with great roads beats a large empty
one.

**Done when:** you can drive a 10-minute loop that stays interesting, at 60 fps.

### Phase 9 — Traffic

Lane graph, AI drivers with gap acceptance and priorities, traffic lights, driver
personalities, density settings, simulation LOD tiers.

**Done when:** traffic runs for 10 minutes at high density without a pile-up.

### Phase 10 — Polish

Real HUD, menus, settings with rebinding, damage, weather, VFX, optimisation
pass, upscaling.

---

## Deliberately deferred

Police, formal racing, economy, multiplayer, on-foot player. The architecture
does not block any of them — input, physics, state and presentation are already
separated for networking — but none is worth starting before Milestone 1.
