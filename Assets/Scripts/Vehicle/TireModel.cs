using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// Result of evaluating a tyre contact patch for one physics step.
    /// Forces are in newtons, in the wheel's ground-plane frame.
    /// </summary>
    public struct TireForces
    {
        /// <summary>Force along the wheel's forward direction. Positive drives the car forward.</summary>
        public float Longitudinal;

        /// <summary>Force along the wheel's right direction.</summary>
        public float Lateral;

        /// <summary>Friction coefficient actually available after load sensitivity and surface.</summary>
        public float AvailableFriction;

        /// <summary>Combined slip utilisation, 0..1+. Above 1 the tyre is saturated and sliding.</summary>
        public float Saturation;
    }

    /// <summary>
    /// A tyre force model. Swapping in a different implementation (a full
    /// Pacejka 2002 set, a brush model, a lookup table generated from real tyre
    /// data) must not require touching the wheel or the vehicle controller.
    /// </summary>
    public interface ITireModel
    {
        TireForces Evaluate(float slipRatio, float slipAngleRad, float normalLoadN,
                            float surfaceFrictionScale, VehicleDefinition.TyreConfig config);
    }

    /// <summary>
    /// Simplified Pacejka "magic formula" with load sensitivity and a friction
    /// circle for combined slip.
    ///
    ///   F(s) = D * sin( C * atan( B*s - E * (B*s - atan(B*s)) ) )
    ///
    /// B controls stiffness (how quickly grip builds), C the shape, D the peak,
    /// E the falloff past the peak. Longitudinal slip is the dimensionless slip
    /// ratio; lateral slip is the slip angle in radians.
    ///
    /// This is deliberately a single-friction-ellipse model rather than full
    /// combined-slip Pacejka: it captures everything a driver can actually feel
    /// (grip build-up, a peak, a slide past the peak, and the fact that braking
    /// and cornering compete for the same friction) at a fraction of the cost
    /// and with far fewer coefficients to author per car.
    /// </summary>
    public sealed class PacejkaTireModel : ITireModel
    {
        public static readonly PacejkaTireModel Shared = new PacejkaTireModel();

        public TireForces Evaluate(float slipRatio, float slipAngleRad, float normalLoadN,
                                   float surfaceFrictionScale, VehicleDefinition.TyreConfig config)
        {
            TireForces result = default;

            if (normalLoadN <= 0.01f)
                return result;

            // --- Load sensitivity ------------------------------------------
            // Real tyres lose friction coefficient as vertical load rises. This
            // is the mechanism behind load transfer mattering: the outside tyre
            // in a corner gains less than the inside one loses, so total grip
            // falls. Without this, weight transfer would be cosmetic.
            float loadRatio = normalLoadN / Mathf.Max(1f, config.NominalLoadN);
            float loadFactor = 1f - config.LoadSensitivity * (loadRatio - 1f);
            loadFactor = Mathf.Clamp(loadFactor, 0.35f, 1.35f);

            float mu = config.PeakFrictionCoefficient * loadFactor * surfaceFrictionScale;
            result.AvailableFriction = mu;

            // --- Pure slip coefficients ------------------------------------
            float fx0 = MagicFormula(slipRatio, config.LongStiffnessB, config.LongShapeC,
                                     config.LongPeakD, config.LongCurvatureE);
            float fy0 = -MagicFormula(slipAngleRad, config.LatStiffnessB, config.LatShapeC,
                                      config.LatPeakD, config.LatCurvatureE);

            // --- Combined slip: friction circle ----------------------------
            // Neither axis may exceed the available friction, and together they
            // may not exceed it either. Scaling both by the same factor keeps
            // the force direction correct while respecting the limit.
            float magnitude = Mathf.Sqrt(fx0 * fx0 + fy0 * fy0);
            result.Saturation = magnitude;
            if (magnitude > 1f)
            {
                float scale = 1f / magnitude;
                fx0 *= scale;
                fy0 *= scale;
            }

            float maxForce = mu * normalLoadN;
            result.Longitudinal = fx0 * maxForce;
            result.Lateral = fy0 * maxForce;
            return result;
        }

        /// <summary>The Pacejka magic formula, returning a normalised force coefficient.</summary>
        public static float MagicFormula(float slip, float b, float c, float d, float e)
        {
            float bs = b * slip;
            float inner = bs - e * (bs - Mathf.Atan(bs));
            return d * Mathf.Sin(c * Mathf.Atan(inner));
        }

        /// <summary>
        /// Slip value at which the tyre reaches peak grip. Useful for tuning,
        /// for the traction control target, and for the debug overlay.
        /// </summary>
        public static float PeakSlip(float b, float c)
        {
            // The magic formula peaks where C * atan(B*s) = pi/2.
            if (b <= 0.0001f || c <= 0.0001f) return 0.1f;
            return Mathf.Tan(Mathf.PI / (2f * c)) / b;
        }
    }
}
