# Vehicle validation

A vehicle is not "realistic" because its code looks complicated. It is realistic
when its measured behaviour matches its specification. This document records the
targets and the measurements.

`Tools/validate_longitudinal.py` reproduces the C# engine, clutch, gearbox, tyre
and aero equations in one dimension so headline figures can be checked without
opening Unity. Run it after changing any engine, gearing or aero value:

```
python3 Tools/validate_longitudinal.py
```

---

## Meridian GT-S (fictional prototype vehicle)

**This is not a real car.** Every figure was authored to be internally consistent
for a 1,520 kg rear-drive turbocharged coupe. When a real vehicle is added, its
numbers must come from manufacturer data and anything unverified must be labelled
as an approximation — see `VEHICLE_AUTHORING.md`.

### Specification

| | |
| --- | --- |
| Layout | Front engine, rear-wheel drive |
| Mass | 1,520 kg, 52% front |
| Wheelbase / track | 2.75 m / 1.58 m |
| Centre of mass | 0.48 m high, 0.055 m behind mid-wheelbase |
| Engine | 3.0 L turbocharged inline-6 |
| Idle / redline / limiter | 750 / 7,000 / 7,100 rpm |
| Transmission | 8-speed dual clutch |
| Gear ratios | 5.25, 3.36, 2.17, 1.72, 1.32, 1.00, 0.82, 0.64 |
| Final drive | 3.15 |
| Tyres | 245/40 R19 equivalent, 0.34 m rolling radius, μ 1.15 |
| Drag | Cd 0.30, frontal area 2.10 m² |

### Measured (simulation, 200 Hz, dry asphalt)

| Quantity | Target | Measured | |
| --- | --- | --- | --- |
| Peak torque | 500 Nm | **500 Nm @ 3,200 rpm** | ✅ |
| Peak power | ~350 hp | **350 hp @ 6,140 rpm** (261 kW) | ✅ |
| Power to weight | — | 230 hp/tonne | — |
| Static suspension compression | 80–110 mm | front 97 mm, rear 94 mm | ✅ |
| Rear axle traction limit, static | — | 8,301 N = 0.56 g | — |
| Rear axle traction limit, 0.6 g transfer | — | 9,651 N = 0.65 g | — |
| 0–100 km/h, Sport, TC off | 5.0–5.5 s | **5.51 s** | ✅ |
| 0–100 km/h, Sport, TC on | — | 5.89 s | ✅ |
| 0–100 km/h, Comfort, TC on | slower than Sport | 6.92 s | ✅ |
| 0–200 km/h, Sport | — | 18.6 s | — |
| Top speed, Sport | drag-limited, 260–280 km/h | **268 km/h in 6th** | ✅ |
| Top speed, Comfort | — | 280 km/h in 7th | ✅ |

### Gearing check

Road speed at redline and engine speed at 100 km/h, per gear:

| Gear | Overall ratio | km/h at redline | rpm at 100 km/h |
| --- | --- | --- | --- |
| 1 | 16.54 | 54 | — |
| 2 | 10.58 | 85 | — |
| 3 | 6.84 | 131 | 5,333 |
| 4 | 5.42 | 166 | 4,227 |
| 5 | 4.16 | 216 | 3,244 |
| 6 | 3.15 | 285 | 2,458 |
| 7 | 2.58 | 347 | 2,015 |
| 8 | 2.02 | 445 | 1,573 |

### Notes on the results

- **Top speed is emergent.** Nothing clamps it. The car stops accelerating at
  268 km/h because drag (≈2,100 N) plus rolling resistance has caught up with the
  drive force available in 6th. Nothing ever reaches 7th or 8th at full throttle,
  because the upshift point in 6th sits above the drag-limited speed — which is
  exactly how a real 8-speed behaves: the top two gears are cruising overdrives.
  Comfort mode, whose earlier upshifts do reach 7th, tops out 12 km/h *higher*
  (280 km/h). That is correct: past peak power, taller gearing wins on top speed
  while shorter gearing wins on acceleration. Nobody wrote that rule — it falls
  out of the torque curve meeting the drag curve.
- **Launch is traction-limited, not power-limited.** The rear axle can only carry
  about 0.65 g once weight has transferred, so no amount of extra power improves
  the first 40 km/h. This is why the car is a mid-5s car rather than a low-5s one.
- **Traction control costs about 0.4 s** in Sport and about 1.1 s in Comfort. It
  should — it is cutting throttle to hold slip inside the mode's allowance
  (0.22 in Sport, 0.08 in Comfort).
- **Comfort is 1.0 s slower than Sport** to 100 km/h. That difference comes
  entirely from the throttle map, the response half-life and the shift points.
  Drive modes are mechanical, not cosmetic.

---

## Test protocol for every new vehicle

Run these in the editor with the F3 overlay open and record the results in this
file.

**Static**
1. Idle rpm settles at the configured value
2. Ride height is stable on spawn — no drop, no bounce
3. Suspension compression is roughly even left to right

**Longitudinal**
4. 0–100 km/h, each drive mode, assists on and off
5. Top speed, and which gear it is reached in
6. 100–0 km/h braking distance, ABS on and off
7. Engine braking: coast down from 100 km/h in gear, note the deceleration

**Rotational**
8. rpm in each gear at 100 km/h — must match the gearing table
9. rpm drop across each upshift — must equal the ratio step
10. Steering lock to lock matches the configured angle

**Lateral**
11. Steady-state cornering: largest sustained lateral g on a constant radius
12. Does it understeer or oversteer at the limit, and is that intended?
13. Can it be provoked into oversteer on throttle with ESC off?

**Persistence**
14. Drive a measured distance, quit, reopen — odometer must carry over
15. Teleport or respawn — odometer must not increase

Record what you find. A number that disagrees with the specification is a bug in
the configuration, not a reason to adjust the specification.
