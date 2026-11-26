// Ce que LMU Drive lit de la voiture et de la séance (plugin 2.5), publié en propriétés SimHub pour les dashs : les
// températures des pneus comme le jeu (bande et couche intérieure, côté intérieur, milieu, extérieur), la température
// idéale de la gomme, la gomme de chaque roue, les aides au travail, le limiteur, les essuie-glaces, le lift and coast,
// l'hybride, les écarts du jeu, les drapeaux par secteur, l'adhérence, l'heure, le vent et les limites de piste.
// Des chiffres bruts lus dans la mémoire du jeu (LmuMemory.Read), aucun calcul ni conseil.
using System;
using System.Collections.Generic;

namespace EnduranceManager.SimHub
{
    public static class Telemetry
    {
        static readonly string[] Corners = { "AVG", "AVD", "ARG", "ARD" };

        static double? R(double value, int digits) => double.IsNaN(value) || double.IsInfinity(value) ? (double?)null : Math.Round(value, digits, MidpointRounding.AwayFromZero);

        // The selected electronics levels (not the intervention flags), for Stream Deck and other displays.
        // A missing setting is not zero; zero is a valid level.
        static string Level(int value, int max) => max > 0 && value >= 0 && value <= max ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "—";

        // The hybrid deployment map on LMU uses 0..10 (20 kW per step).
        // The thermal map uses 0=Safety Car and 1=Race (LMU June/September 2024 release notes).
        // Do not assign a label to an unrecognised map range.
        internal static string Cartography(Sample s)
        {
            if (s.MotorMapMax <= 0 || s.MotorMap < 0 || s.MotorMap > s.MotorMapMax) return "—";
            if (s.MotorMapMax >= 10 && s.MotorMap <= 10) return (s.MotorMap * 20).ToString(System.Globalization.CultureInfo.InvariantCulture) + " kW";
            if (s.MotorMapMax <= 2 && s.MotorMap <= 1) return s.MotorMap == 0 ? "SAFETY CAR" : "COURSE";
            return Level(s.MotorMap, s.MotorMapMax);
        }

        static string Arb(int value, int max) => max > 0 && value >= 0 && value <= max ? (value == 0 ? "OFF" : "P" + value.ToString(System.Globalization.CultureInfo.InvariantCulture)) : "—";

        // Every property under « Voiture. » and « Seance. », and how it reads the last sample (null when nobody drives).
        public static readonly (string Name, Func<Sample, object> Get)[] Properties = Build();

        static (string, Func<Sample, object>)[] Build()
        {
            var list = new List<(string, Func<Sample, object>)>();
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                string at = "Voiture.Pneus." + Corners[i] + ".";
                Func<Sample, Wheel?> w = s => s.Wheels != null ? s.Wheels[k] : (Wheel?)null;
                list.Add((at + "Bande.Interieur", s => w(s) is Wheel x ? R(x.TreadIn, 0) : null));
                list.Add((at + "Bande.Milieu", s => w(s) is Wheel x ? R(x.TreadMid, 0) : null));
                list.Add((at + "Bande.Exterieur", s => w(s) is Wheel x ? R(x.TreadOut, 0) : null));
                list.Add((at + "Couche.Interieur", s => w(s) is Wheel x ? R(x.InnerIn, 0) : null));
                list.Add((at + "Couche.Milieu", s => w(s) is Wheel x ? R(x.InnerMid, 0) : null));
                list.Add((at + "Couche.Exterieur", s => w(s) is Wheel x ? R(x.InnerOut, 0) : null));
                list.Add((at + "Carcasse", s => w(s) is Wheel x ? R(x.Temp, 0) : null));
                list.Add((at + "Ideale", s => w(s) is Wheel x && x.Optimal > 0 ? R(x.Optimal, 0) : null));
                list.Add((at + "Restant_%", s => w(s) is Wheel x ? R(x.Wear * 100, 1) : null));
                list.Add((at + "Pression_kPa", s => w(s) is Wheel x ? R(x.Pressure, 1) : null));
                list.Add((at + "Frein", s => w(s) is Wheel x ? R(x.Brake, 0) : null));
                list.Add((at + "Gomme", s => w(s) is Wheel x ? x.CompoundIndex + "." + x.CompoundType : null));
                list.Add((at + "Crevaison", s => w(s) is Wheel x && x.Flat));
                list.Add((at + "Perdue", s => w(s) is Wheel x && x.Off));
                list.Add((at + "Carrossage", s => w(s) is Wheel x ? R(x.Camber, 2) : null));
                list.Add((at + "Charge_N", s => w(s) is Wheel x ? R(x.Load, 0) : null));
                list.Add((at + "Adherence", s => w(s) is Wheel x ? R(x.Grip, 2) : null));
                list.Add((at + "Suspension_mm", s => w(s) is Wheel x ? R(x.Suspension * 1000, 1) : null));
                list.Add((at + "Hauteur_mm", s => w(s) is Wheel x ? R(x.Ride * 1000, 1) : null));
            }
            list.AddRange(new (string, Func<Sample, object>)[]
            {
                ("Voiture.Reglages.TC", s => Level(s.TC, s.TCMax)),
                ("Voiture.Reglages.CoupureTC", s => Level(s.TCCut, s.TCCutMax)),
                ("Voiture.Reglages.GlisseTC", s => Level(s.TCSlip, s.TCSlipMax)),
                ("Voiture.Reglages.ABS", s => Level(s.ABS, s.ABSMax)),
                ("Voiture.Reglages.Carto", s => Cartography(s)),
                ("Voiture.Reglages.Migration", s => Level(s.Migration, s.MigrationMax)),
                ("Voiture.Reglages.AntiroulisAV", s => Arb(s.AntiSwayFront, s.AntiSwayFrontMax)),
                ("Voiture.Reglages.AntiroulisAR", s => Arb(s.AntiSwayRear, s.AntiSwayRearMax)),
                ("Voiture.Reglages.Repartition", s => s.RearBrakeBias > 0 && s.RearBrakeBias < 1 ? ((1 - s.RearBrakeBias) * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%" : "—"),
                ("Voiture.Reglages.Regen", s => s.MotorState != 0 && s.Regen > 0 && !double.IsInfinity(s.Regen) ? Math.Round(s.Regen).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " kW" : "—"),
                ("Voiture.ABSActif", s => s.AbsOn),
                ("Voiture.TCActif", s => s.TcOn),
                ("Voiture.LimiteurStands", s => s.PitLimiter),
                ("Voiture.Limiteur", s => s.SpeedLimiter),
                ("Voiture.EssuieGlaces", s => s.Wipers),
                ("Voiture.Phares", s => s.Headlights),
                ("Voiture.LiftAndCoast", s => s.LiftCoast),
                ("Voiture.Turbo_kPa", s => R(s.Boost, 1)),
                ("Voiture.Hybride.Etat", s => s.MotorState),
                ("Voiture.Hybride.Charge_%", s => s.MotorState == 0 || s.Battery <= 0 ? null : R(s.Battery * 100, 1)),
                ("Voiture.Hybride.Moteur_C", s => s.MotorState == 0 ? null : R(s.MotorTemp, 1)),
                ("Voiture.Hybride.Eau_C", s => s.MotorState == 0 ? null : R(s.MotorWater, 1)),
                ("Voiture.Hybride.Regime", s => s.MotorState == 0 ? null : R(s.MotorRpm, 0)),
                ("Voiture.Hybride.Couple_Nm", s => s.MotorState == 0 ? null : R(s.MotorTorque, 1)),
                // LMU counts the gap to the car ahead negative (checked on track): shown as seconds behind it.
                ("Voiture.Ecart.Devant", s => R(Math.Abs(s.GapAhead), 3)),
                ("Voiture.Ecart.Derriere", s => R(s.GapBehind, 3)),
                ("Voiture.DeltaMeilleur", s => R(s.DeltaBest, 3)),
                ("Voiture.TourInvalide", s => s.Invalid),
                ("Voiture.TourCompte", s => s.CountLap),
                ("Voiture.Drapeau", s => s.Flag),
                ("Voiture.SousJaune", s => s.UnderYellow != 0),
                ("Voiture.EtatStands", s => s.PitStateScoring),
                ("Voiture.DernierChoc.Temps", s => s.ImpactET > 0 ? R(s.ImpactET, 2) : null),
                ("Voiture.DernierChoc.Force", s => s.ImpactET > 0 ? R(s.ImpactForce, 0) : null),
                ("Voiture.LimitesPiste.Pas", s => s.TrackLimitsSteps),
                ("Seance.LimitesPiste.PasParPenalite", s => s.LimitsPerPenalty),
                ("Seance.LimitesPiste.PasParPoint", s => s.LimitsPerPoint),
                ("Seance.DrapeauSecteur1", s => s.SectorFlags?[0]),
                ("Seance.DrapeauSecteur2", s => s.SectorFlags?[1]),
                ("Seance.DrapeauSecteur3", s => s.SectorFlags?[2]),
                ("Seance.FeuxDepart", s => s.StartLight),
                ("Seance.Adherence", s => s.TrackGrip),
                ("Seance.Nuages", s => s.Clouds),
                ("Seance.Heure_s", s => s.TimeOfDay >= 0 && s.TimeOfDay <= 86400 ? R(s.TimeOfDay, 0) : null),
                ("Seance.HumiditeMoyenne", s => s.WetAverage >= 0 && s.WetAverage <= 1 ? R(s.WetAverage * 100, 0) : null),
                ("Seance.Vent_ms", s => R(s.Wind, 1)),
                ("Seance.TempsRestant", s => R(s.SessionLeft, 0)),
            });
            return list.ToArray();
        }
    }
}
