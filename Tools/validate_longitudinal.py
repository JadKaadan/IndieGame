"""
Longitudinal validation of the Meridian GT-S configuration.

Mirrors the equations in VehicleEngine / VehicleTransmission / VehicleWheel /
VehicleAerodynamics exactly, in one dimension, so the vehicle's headline numbers
can be checked before Unity is ever opened. Lateral dynamics are out of scope here.
"""
import math

# ---- Definitions (must match the assets in Assets/Data/Vehicles/) ----
VEHICLES = {
    "MeridianGTS": dict(
        MASS=1520.0, FRONT_DIST=0.52, WHEELBASE=2.75, COM_HEIGHT=0.48,
        WHEEL_R=0.34, WHEEL_I=1.30, ENGINE_I=0.28, IDLE_RPM=750.0,
        REDLINE=7000.0, LIMITER=7100.0, PEAK_TORQUE=500.0,
        FRICTION=22.0, FRICTION_PER_RAD=0.045,
        MAX_BOOST=0.95, BOOST_GAIN=0.62, BOOST_ONSET=1500.0, BOOST_FULL=3200.0,
        BOOST_SPOOL_HL=0.30, BOOST_DECAY_HL=0.10,
        GEARS=[5.25, 3.36, 2.17, 1.72, 1.32, 1.00, 0.82, 0.64], FINAL=3.15,
        EFF=0.90, CLUTCH_MAX=900.0, SHIFT_TIME=0.12, SHIFT_COOLDOWN=0.55,
        BASE_UP=5200.0, THROTTLE_UP_GAIN=1400.0,
        CD=0.30, AREA=2.10, MU=1.15, LOAD_SENS=0.22,
        B=11.0, C=1.65, E=0.95, SPORT_UP=1100.0,
        CURVE=[(0.00,0.30),(0.11,0.46),(0.20,0.78),(0.26,1.00),
               (0.60,1.00),(0.72,0.94),(0.86,0.82),(1.00,0.62)],
        DRIVEN_AXLE_SHARE=None,   # rear driven
        DRIVEN_FRONT=False,
    ),
    "Vantis15T": dict(
        MASS=1180.0, FRONT_DIST=0.62, WHEELBASE=2.58, COM_HEIGHT=0.55,
        WHEEL_R=0.315, WHEEL_I=0.95, ENGINE_I=0.16, IDLE_RPM=800.0,
        REDLINE=6500.0, LIMITER=6600.0, PEAK_TORQUE=250.0,
        FRICTION=13.0, FRICTION_PER_RAD=0.030,
        MAX_BOOST=0.80, BOOST_GAIN=0.55, BOOST_ONSET=1400.0, BOOST_FULL=2600.0,
        BOOST_SPOOL_HL=0.22, BOOST_DECAY_HL=0.09,
        GEARS=[4.15, 2.37, 1.56, 1.16, 0.86, 0.69], FINAL=4.06,
        EFF=0.91, CLUTCH_MAX=430.0, SHIFT_TIME=0.28, SHIFT_COOLDOWN=0.70,
        BASE_UP=4300.0, THROTTLE_UP_GAIN=1500.0,
        CD=0.32, AREA=2.20, MU=1.00, LOAD_SENS=0.26,
        B=10.5, C=1.60, E=0.95, SPORT_UP=800.0,
        CURVE=[(0.00,0.28),(0.12,0.52),(0.24,0.92),(0.30,1.00),
               (0.62,0.98),(0.74,0.88),(0.88,0.72),(1.00,0.52)],
        DRIVEN_AXLE_SHARE=None,
        DRIVEN_FRONT=True,        # front driven: weight transfers AWAY from the driven axle
    ),
}

MASS = 1520.0
FRONT_DIST = 0.52
WHEELBASE = 2.75
COM_HEIGHT = 0.48
WHEEL_R = 0.34
WHEEL_I = 1.30
ENGINE_I = 0.28
IDLE_RPM = 750.0
REDLINE = 7000.0
LIMITER = 7100.0
PEAK_TORQUE = 500.0
FRICTION = 22.0
FRICTION_PER_RAD = 0.045
MAX_BOOST = 0.95
BOOST_GAIN = 0.62
BOOST_ONSET = 1500.0
BOOST_FULL = 3200.0
BOOST_SPOOL_HL = 0.30
BOOST_DECAY_HL = 0.10
GEARS = [5.25, 3.36, 2.17, 1.72, 1.32, 1.00, 0.82, 0.64]
FINAL = 3.15
EFF = 0.90
CLUTCH_MAX = 900.0
SHIFT_TIME = 0.12
SHIFT_COOLDOWN = 0.55
BASE_UP = 5200.0
THROTTLE_UP_GAIN = 1400.0
CD = 0.30
AREA = 2.10
RHO = 1.225
G = 9.80665
MU = 1.15
NOMINAL_LOAD = MASS * 9.81 / 4.0
LOAD_SENS = 0.22
CRR = 0.013
LOW_SPEED_REF = 3.0
RELAX_LEN = 0.55
# Pacejka longitudinal
B, C, D, E = 11.0, 1.65, 1.0, 0.95

TORQUE_CURVE = [(0.00,0.30),(0.11,0.46),(0.20,0.78),(0.26,1.00),
                (0.60,1.00),(0.72,0.94),(0.86,0.82),(1.00,0.62)]

RAD_TO_RPM = 60.0 / (2*math.pi)
RPM_TO_RAD = 1.0 / RAD_TO_RPM


def torque_curve(rpm):
    x = min(max(rpm / REDLINE, 0.0), 1.0)
    for i in range(len(TORQUE_CURVE) - 1):
        x0, y0 = TORQUE_CURVE[i]
        x1, y1 = TORQUE_CURVE[i+1]
        if x0 <= x <= x1:
            t = (x - x0) / (x1 - x0) if x1 > x0 else 0.0
            # smoothstep approximates Unity's default smooth tangents better than linear
            t = t*t*(3-2*t)
            return y0 + (y1-y0)*t
    return TORQUE_CURVE[-1][1]


def magic(slip):
    bs = B*slip
    inner = bs - E*(bs - math.atan(bs))
    return D*math.sin(C*math.atan(inner))


def damp(cur, tgt, half_life, dt):
    if half_life <= 1e-4:
        return tgt
    return cur + (tgt-cur)*(1 - 0.5**(dt/half_life))


class Sim:
    def __init__(self, drive_mode="sport"):
        self.v = 0.0
        self.we = IDLE_RPM * RPM_TO_RAD      # engine rad/s
        self.ww = 0.0                        # driven (rear) wheel rad/s
        self.wf = 0.0                        # front wheel rad/s
        self.boost = 0.0
        self.gear = 1
        self.shifting = False
        self.shift_t = 0.0
        self.cooldown = 0.0
        self.clutch_lock = 0.0
        self.eff_throttle = 0.0
        self.last_engine_torque = 0.0
        self.slip = 0.0
        self.accel = 0.0
        self.mode = drive_mode

    def mode_params(self):
        if self.mode == "sport":
            return dict(up_off=1100.0, hl=0.045, shift_mult=0.55, tc_allow=0.22)
        return dict(up_off=0.0, hl=0.16, shift_mult=1.35, tc_allow=0.08)

    def step(self, dt, throttle_pedal, tc_on=True):
        mp = self.mode_params()

        # --- axle loads with longitudinal weight transfer ---
        transfer = MASS * self.accel * COM_HEIGHT / WHEELBASE
        rear_axle = MASS*G*(1-FRONT_DIST) + transfer
        front_axle = MASS*G*FRONT_DIST - transfer
        rear_axle = max(0.0, rear_axle); front_axle = max(0.0, front_axle)
        fz_rear = rear_axle/2.0
        fz_front = front_axle/2.0

        # --- tyre (rear, driven) ---
        ref = max(abs(self.v), LOW_SPEED_REF)
        steady = max(-4.0, min(4.0, (self.ww*WHEEL_R - self.v)/ref))
        relax = min(1.0, ref*dt/RELAX_LEN)
        self.slip += (steady - self.slip)*relax
        load_factor = max(0.35, min(1.35, 1 - LOAD_SENS*(fz_rear/NOMINAL_LOAD - 1)))
        mu_eff = MU*load_factor
        fx = magic(self.slip)*mu_eff*fz_rear      # per rear wheel
        fx_total = 2*fx

        # --- traction control (cuts engine throttle only) ---
        throttle = throttle_pedal
        if tc_on:
            excess = self.slip - mp['tc_allow']
            if excess > 0:
                throttle *= max(0.0, 1 - excess*3.5)

        # --- gearbox ---
        if self.cooldown > 0: self.cooldown -= dt
        if self.shifting:
            self.shift_t += dt
            if self.shift_t >= SHIFT_TIME*mp['shift_mult']:
                self.shifting = False
                self.gear = self.target
                self.cooldown = SHIFT_COOLDOWN
        elif self.cooldown <= 0:
            up_rpm = min(BASE_UP + mp['up_off'] + throttle_pedal*THROTTLE_UP_GAIN, REDLINE*0.985)
            if self.gear < len(GEARS) and self.we*RAD_TO_RPM >= up_rpm:
                self.shifting = True; self.shift_t = 0.0; self.target = self.gear+1

        ratio = GEARS[self.gear-1]*FINAL
        in_gear = not self.shifting

        # --- clutch ---
        engagement = 0.0
        if in_gear:
            driveline_rpm = abs(self.ww*ratio)*RAD_TO_RPM
            eng = min(1.0, max(0.0, (driveline_rpm - IDLE_RPM*0.45)/(IDLE_RPM*1.15 - IDLE_RPM*0.45)))
            if throttle_pedal > 0.05:
                eng = max(eng, min(0.55, throttle_pedal*0.7))
            engagement = eng
        self.clutch_lock = damp(self.clutch_lock, engagement, 0.03, dt)

        clutch_torque = 0.0
        if in_gear and self.clutch_lock > 0.01:
            slip_rad = self.we - self.ww*ratio
            capacity = CLUTCH_MAX*self.clutch_lock
            slipping_limit = max(0.0, self.last_engine_torque)*1.05 + 30.0
            eff_cap = min(capacity, slipping_limit + (capacity-slipping_limit)*self.clutch_lock)
            sync = ENGINE_I/dt*0.8
            required = self.last_engine_torque + slip_rad*sync
            clutch_torque = max(-eff_cap, min(eff_cap, required))

        # --- engine ---
        mapped = throttle if self.mode != "sport" else min(1.0, throttle*1.4)
        self.eff_throttle = damp(self.eff_throttle, min(1.0, mapped), mp['hl'], dt)
        rpm = self.we*RAD_TO_RPM
        fuel_cut = 0.0 if rpm >= LIMITER else 1.0
        spool = 0.0 if BOOST_FULL <= BOOST_ONSET else min(1.0, max(0.0, (rpm-BOOST_ONSET)/(BOOST_FULL-BOOST_ONSET)))
        target_boost = MAX_BOOST*spool*self.eff_throttle
        hl = BOOST_SPOOL_HL if target_boost > self.boost else BOOST_DECAY_HL
        self.boost = damp(self.boost, target_boost, hl, dt)
        boost_mult = (1 + BOOST_GAIN*(self.boost/MAX_BOOST))/(1 + BOOST_GAIN)
        combustion = torque_curve(rpm)*PEAK_TORQUE*self.eff_throttle*boost_mult*fuel_cut
        friction = FRICTION + FRICTION_PER_RAD*abs(self.we)
        idle_assist = 0.0
        if rpm < IDLE_RPM:
            deficit = min(1.0, max(0.0, (IDLE_RPM-rpm)/(IDLE_RPM*0.5)))
            idle_assist = deficit*(friction+25)*1.6
        engine_out = combustion + idle_assist - friction
        self.we += (engine_out - clutch_torque)/ENGINE_I*dt
        self.we = max(0.0, self.we)
        self.last_engine_torque = engine_out

        # --- wheels ---
        drive_torque = clutch_torque*ratio*EFF/2.0    # per rear wheel
        rr_rear = -math.copysign(1.0, self.ww if abs(self.ww) > 1e-6 else 1.0)*CRR*fz_rear*WHEEL_R
        self.ww += (drive_torque - fx*WHEEL_R + rr_rear)/WHEEL_I*dt

        # front wheels roll freely; they only contribute rolling resistance
        self.wf = self.v/WHEEL_R
        rr_front_force = CRR*front_axle

        # --- body ---
        drag = 0.5*RHO*CD*AREA*self.v*self.v
        net = fx_total - drag - rr_front_force
        self.accel = net/MASS
        self.v = max(0.0, self.v + self.accel*dt)
        return rpm


def run(mode="sport", tc_on=True, seconds=90.0, dt=0.005):
    s = Sim(mode)
    t = 0.0
    t100 = None; t200 = None; t60mph = None
    top = 0.0
    trace = []
    while t < seconds:
        s.step(dt, 1.0, tc_on)
        t += dt
        kmh = s.v*3.6
        if t100 is None and kmh >= 100.0: t100 = t
        if t200 is None and kmh >= 200.0: t200 = t
        if t60mph is None and s.v*2.236936 >= 60.0: t60mph = t
        top = max(top, kmh)
        if abs(t - round(t,1)) < dt/2 and round(t,1) in (1,2,3,4,5,6,8,10,15,20,30,45,60,89):
            trace.append((round(t,1), kmh, s.we*RAD_TO_RPM, s.gear, s.slip, s.boost))
    return t100, t200, t60mph, top, trace



def load(name):
    """Rebind the module-level constants to the named vehicle."""
    spec = VEHICLES[name]
    g = globals()
    for key, value in spec.items():
        g[key] = value
    g["TORQUE_CURVE"] = spec["CURVE"]
    g["NOMINAL_LOAD"] = spec["MASS"] * 9.81 / 4.0
    g["RPM_TO_RAD"] = 1.0 / RAD_TO_RPM
    return spec


def report(name):
    spec = load(name)
    print("=" * 74)
    print("%s  -  longitudinal validation (fictional vehicle)" % name)
    print("=" * 74)

    peak_hp = peak_hp_rpm = peak_nm = peak_nm_rpm = 0
    r = IDLE_RPM
    while r <= REDLINE:
        spool = min(1.0, max(0.0, (r - BOOST_ONSET) / (BOOST_FULL - BOOST_ONSET)))
        tq = torque_curve(r) * PEAK_TORQUE * (1 + BOOST_GAIN * spool) / (1 + BOOST_GAIN)
        hp = tq * r / 7127.0
        if tq > peak_nm: peak_nm, peak_nm_rpm = tq, r
        if hp > peak_hp: peak_hp, peak_hp_rpm = hp, r
        r += 10
    print("  Peak torque : %6.0f Nm @ %5.0f rpm" % (peak_nm, peak_nm_rpm))
    print("  Peak power  : %6.0f hp @ %5.0f rpm  (%.0f kW)" % (peak_hp, peak_hp_rpm, peak_hp * 0.7457))
    print("  Power/weight: %.0f hp per tonne" % (peak_hp / MASS * 1000))

    # Traction limit at the driven axle, including longitudinal load transfer.
    share = FRONT_DIST if DRIVEN_FRONT else (1 - FRONT_DIST)
    sign = -1.0 if DRIVEN_FRONT else 1.0
    for label, accel_g in (("static", 0.0), ("under 0.5 g", 0.5)):
        axle = MASS * G * share + sign * MASS * (accel_g * G) * COM_HEIGHT / WHEELBASE
        fz = max(1.0, axle / 2)
        lf = max(0.35, min(1.35, 1 - LOAD_SENS * (fz / NOMINAL_LOAD - 1)))
        fmax = 2 * MU * lf * fz
        print("  Driven axle %-12s: %6.0f N  ->  max traction %6.0f N = %.2f g"
              % (label, axle, fmax, fmax / MASS / G))

    for mode in ("comfort", "sport"):
        for tc in (True, False):
            t100, t200, t60, top, _ = run(mode, tc)
            print("  %-8s TC %-3s : 0-100 %s   0-200 %s   top %5.1f km/h"
                  % (mode.upper(), "on" if tc else "off",
                     ("%.2f s" % t100) if t100 else "  n/a ",
                     ("%.2f s" % t200) if t200 else "  n/a ",
                     top))
    print()


if __name__ == "__main__":
    import sys
    names = sys.argv[1:] or list(VEHICLES.keys())
    for n in names:
        report(n)
