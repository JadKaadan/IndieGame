using System;
using IndieGame.Persistence;
using UnityEngine;

namespace IndieGame.Vehicles.Tuning
{
    public enum TuningCategory
    {
        Ecu,
        Turbo,
        Exhaust,
        GearRatios,
        FinalDrive,
        Brakes,
        Tyres,
        Springs,
        RideHeight,
        WeightReduction,
        Aero
    }

    /// <summary>One selectable level within a category.</summary>
    public class TuningLevel
    {
        public string Name;
        public string Effect;
        public int Price;

        /// <summary>Writes this level's changes into the save data's physics multipliers.</summary>
        public Action<VehicleSaveData> Apply;

        public TuningLevel(string name, string effect, int price, Action<VehicleSaveData> apply)
        {
            Name = name;
            Effect = effect;
            Price = price;
            Apply = apply;
        }
    }

    /// <summary>
    /// The parts catalogue.
    ///
    /// Every level writes a multiplier that a real subsystem reads: the ECU stages
    /// scale the torque curve, the turbo raises the boost ceiling and shortens
    /// spool, the tyres scale the peak friction coefficient, the springs scale the
    /// suspension rate, weight reduction changes the rigidbody's mass. There is no
    /// "horsepower number" anywhere that is not derived from those.
    /// </summary>
    public static class TuningCatalogue
    {
        public static readonly TuningCategory[] Categories =
            (TuningCategory[])Enum.GetValues(typeof(TuningCategory));

        public static TuningLevel[] Levels(TuningCategory category)
        {
            switch (category)
            {
                case TuningCategory.Ecu:
                    return new[]
                    {
                        new TuningLevel("Stock", "Factory map", 0, d => d.EngineTorqueMultiplier = 1.00f),
                        new TuningLevel("Stage 1", "+8% torque across the range", 1200,
                                        d => d.EngineTorqueMultiplier = 1.08f),
                        new TuningLevel("Stage 2", "+16% torque, requires the sports exhaust", 3400,
                                        d => d.EngineTorqueMultiplier = 1.16f),
                        new TuningLevel("Stage 3", "+26% torque, aggressive ignition timing", 7800,
                                        d => d.EngineTorqueMultiplier = 1.26f),
                    };

                case TuningCategory.Turbo:
                    return new[]
                    {
                        new TuningLevel("Stock", "Factory turbocharger", 0,
                                        d => { d.BoostBarOffset = 0f; d.SpoolSpeedMultiplier = 1f; }),
                        new TuningLevel("Hybrid", "+0.25 bar, slightly slower spool", 2600,
                                        d => { d.BoostBarOffset = 0.25f; d.SpoolSpeedMultiplier = 1.15f; }),
                        new TuningLevel("Big single", "+0.60 bar, noticeably more lag", 6900,
                                        d => { d.BoostBarOffset = 0.60f; d.SpoolSpeedMultiplier = 1.65f; }),
                        new TuningLevel("Twin scroll", "+0.45 bar and faster spool", 9400,
                                        d => { d.BoostBarOffset = 0.45f; d.SpoolSpeedMultiplier = 0.72f; }),
                    };

                case TuningCategory.Exhaust:
                    return new[]
                    {
                        new TuningLevel("Stock", "Silenced, valve shut", 0, d => d.ExhaustAggression = 0.15f),
                        new TuningLevel("Sports", "Louder, occasional overrun crackle", 900,
                                        d => d.ExhaustAggression = 0.45f),
                        new TuningLevel("Decat", "Loud, frequent pops and bangs", 2200,
                                        d => d.ExhaustAggression = 0.75f),
                        new TuningLevel("Race", "Very loud, flames on hard lifts", 4100,
                                        d => d.ExhaustAggression = 1.00f),
                    };

                case TuningCategory.GearRatios:
                    return new[]
                    {
                        new TuningLevel("Stock", "Factory ratios", 0, d => d.GearRatioMultiplier = 1f),
                        new TuningLevel("Short", "8% shorter: quicker, lower top speed", 2400,
                                        d => d.GearRatioMultiplier = 1.08f),
                        new TuningLevel("Very short", "16% shorter, drag setup", 4200,
                                        d => d.GearRatioMultiplier = 1.16f),
                        new TuningLevel("Long", "8% longer: relaxed cruising, higher top speed", 2400,
                                        d => d.GearRatioMultiplier = 0.92f),
                    };

                case TuningCategory.FinalDrive:
                    return new[]
                    {
                        new TuningLevel("Stock", "Factory final drive", 0, d => d.FinalDriveMultiplier = 1f),
                        new TuningLevel("Short", "+7% final drive", 1500, d => d.FinalDriveMultiplier = 1.07f),
                        new TuningLevel("Long", "-7% final drive", 1500, d => d.FinalDriveMultiplier = 0.93f),
                    };

                case TuningCategory.Brakes:
                    return new[]
                    {
                        new TuningLevel("Stock", "Factory brakes", 0,
                                        d => { d.BrakeTorqueMultiplier = 1f; d.BrakeBias = 0.5f; }),
                        new TuningLevel("Performance", "+25% torque", 1800,
                                        d => { d.BrakeTorqueMultiplier = 1.25f; d.BrakeBias = 0.5f; }),
                        new TuningLevel("Big brake kit", "+55% torque, more front bias", 5200,
                                        d => { d.BrakeTorqueMultiplier = 1.55f; d.BrakeBias = 0.42f; }),
                    };

                case TuningCategory.Tyres:
                    return new[]
                    {
                        new TuningLevel("Touring", "Long life, least grip", 0, d => d.TyreGripMultiplier = 0.88f),
                        new TuningLevel("Sport", "Factory performance summer", 700,
                                        d => d.TyreGripMultiplier = 1.00f),
                        new TuningLevel("Semi-slick", "Track compound", 2900,
                                        d => d.TyreGripMultiplier = 1.18f),
                        new TuningLevel("Slick", "Race slick, poor when cold or wet", 6400,
                                        d => d.TyreGripMultiplier = 1.32f),
                    };

                case TuningCategory.Springs:
                    return new[]
                    {
                        new TuningLevel("Stock", "Factory rates", 0,
                                        d => { d.SpringStiffnessMultiplier = 1f; d.DamperMultiplier = 1f; }),
                        new TuningLevel("Sport", "+20% rate, +15% damping", 1600,
                                        d => { d.SpringStiffnessMultiplier = 1.20f; d.DamperMultiplier = 1.15f; }),
                        new TuningLevel("Coilover", "+45% rate, +35% damping", 4300,
                                        d => { d.SpringStiffnessMultiplier = 1.45f; d.DamperMultiplier = 1.35f; }),
                        new TuningLevel("Race", "+80% rate, +60% damping", 8200,
                                        d => { d.SpringStiffnessMultiplier = 1.80f; d.DamperMultiplier = 1.60f; }),
                    };

                case TuningCategory.RideHeight:
                    return new[]
                    {
                        new TuningLevel("Stock", "Factory height", 0, d => d.RideHeightOffsetM = 0f),
                        new TuningLevel("-20 mm", "Lower centre of mass", 300, d => d.RideHeightOffsetM = -0.020f),
                        new TuningLevel("-40 mm", "Less body roll, less travel", 300, d => d.RideHeightOffsetM = -0.040f),
                        new TuningLevel("-60 mm", "Aggressive, bottoms out on kerbs", 300, d => d.RideHeightOffsetM = -0.060f),
                    };

                case TuningCategory.WeightReduction:
                    return new[]
                    {
                        new TuningLevel("Stock", "Full interior", 0, d => d.MassOffsetKg = 0f),
                        new TuningLevel("Stage 1", "-60 kg", 2100, d => d.MassOffsetKg = -60f),
                        new TuningLevel("Stage 2", "-130 kg, stripped interior", 5600, d => d.MassOffsetKg = -130f),
                        new TuningLevel("Stage 3", "-210 kg, carbon panels and cage", 12800, d => d.MassOffsetKg = -210f),
                    };

                case TuningCategory.Aero:
                    return new[]
                    {
                        new TuningLevel("Stock", "Factory bodywork", 0,
                                        d => { d.DownforceMultiplier = 1f; d.DragMultiplier = 1f; }),
                        new TuningLevel("Lip and splitter", "+60% downforce, +4% drag", 1400,
                                        d => { d.DownforceMultiplier = 1.6f; d.DragMultiplier = 1.04f; }),
                        new TuningLevel("GT wing", "+220% downforce, +14% drag", 3800,
                                        d => { d.DownforceMultiplier = 3.2f; d.DragMultiplier = 1.14f; }),
                    };

                default:
                    return new[] { new TuningLevel("Stock", "", 0, d => { }) };
            }
        }

        public static string DisplayName(TuningCategory category)
        {
            switch (category)
            {
                case TuningCategory.Ecu: return "ECU";
                case TuningCategory.GearRatios: return "Gear ratios";
                case TuningCategory.FinalDrive: return "Final drive";
                case TuningCategory.RideHeight: return "Ride height";
                case TuningCategory.WeightReduction: return "Weight";
                default: return category.ToString();
            }
        }

        /// <summary>Ensures the level array matches the catalogue size.</summary>
        public static int[] NormaliseLevels(int[] levels)
        {
            int count = Categories.Length;
            var result = new int[count];
            if (levels != null)
            {
                for (int i = 0; i < count && i < levels.Length; i++)
                {
                    int max = Levels(Categories[i]).Length - 1;
                    result[i] = Mathf.Clamp(levels[i], 0, max);
                }
            }
            // Tyres default to the factory fitment rather than the cheapest option.
            if (levels == null || levels.Length == 0)
                result[(int)TuningCategory.Tyres] = 1;
            return result;
        }

        /// <summary>
        /// Rewrites every physics multiplier in <paramref name="data"/> from the
        /// selected levels. Always applied from stock so removing a part actually
        /// removes its effect.
        /// </summary>
        public static void Apply(VehicleSaveData data, int[] levels)
        {
            data.TuningLevels = NormaliseLevels(levels);

            for (int i = 0; i < Categories.Length; i++)
            {
                TuningLevel[] options = Levels(Categories[i]);
                int index = Mathf.Clamp(data.TuningLevels[i], 0, options.Length - 1);
                options[index].Apply(data);
            }
        }

        public static int TotalSpend(int[] levels)
        {
            int[] normalised = NormaliseLevels(levels);
            int total = 0;
            for (int i = 0; i < Categories.Length; i++)
                total += Levels(Categories[i])[normalised[i]].Price;
            return total;
        }
    }
}
