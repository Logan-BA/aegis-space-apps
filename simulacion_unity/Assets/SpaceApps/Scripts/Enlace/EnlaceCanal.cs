using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps.Enlace
{
    /// <summary>
    /// Modelo simplificado del canal de radio nave → Tierra (Red de Espacio Profundo). Calcula:
    ///  - Distancia según la fase (órbita baja vía satélite de relevo, Luna, tránsito y superficie de Marte con
    ///    la geometría Tierra-Marte-Sol que cambia cada 780 días) y el retraso de la luz.
    ///  - Relación señal/ruido Eb/N0: cae 20·log10(d) por la pérdida en el espacio libre.
    ///  - Penalizaciones: centelleo del plasma solar cuando el Sol queda cerca de la línea de visión
    ///    (conjunción solar → apagón), tormentas solares, ionosfera y cinturones de Van Allen (ráfagas).
    ///  - Tasa de error de bit (BER) de una modulación BPSK: 0,5·erfc(√(Eb/N0)).
    /// Luego aplica ese ruido a cada transmisión (bits invertidos al azar + ráfagas + tramas perdidas).
    /// Es educativo: los valores están ajustados para que en la demo se vean errores corregidos y apagones.
    /// </summary>
    public class EnlaceCanal
    {
        public const double AuKm = 149_597_870.7, LightKmS = 299_792.458;
        const double MarsOrbitAu = 1.524, SynodicDays = 779.9, Theta0Deg = 70.0, EbN0At1AuDb = 14.5;   // margen típico de un enlace en banda X con antenas de 34–70 m

        public double DistanceKm, DistanceAu, LightSeconds, SepDeg = 180, EbN0Db = 99, Ber;
        public float BurstProb, LossProb;
        public string Via = "";
        public readonly List<string> Sources = new List<string>();

        public void Update(Simulation sim, bool storm)
        {
            Sources.Clear();
            string key = sim.CurrentKey;
            double f = 0.5;
            bool returning = false;
            if (sim.Legs.Count > 0)
            {
                var leg = sim.Legs[Mathf.Min(sim.LegIndex, sim.Legs.Count - 1)];
                f = Math.Min(1.0, sim.HourInLeg / Math.Max(1.0, leg.days * 24.0));
                for (int i = 0; i < sim.LegIndex && i < sim.Legs.Count; i++)
                    if (sim.Legs[i].key == "mars_surface" || sim.Legs[i].key == "moon_surface") returning = true;
            }

            SepDeg = 120;
            BurstProb = 0f; LossProb = 0f;
            Via = "DSN (Red de Espacio Profundo)";
            switch (key)
            {
                case "earth":
                case "conditioning":
                    DistanceKm = 0; Via = "Red terrestre";
                    break;
                case "leo":
                    DistanceKm = 72_000; Via = "Satélites de relevo TDRS";   // subida y bajada por órbita geoestacionaria
                    break;
                case "moon_transit":
                    DistanceKm = 6_800 + (returning ? 1 - f : f) * 377_600;
                    break;
                case "moon_surface":
                    DistanceKm = 384_400;
                    break;
                case "mars_transit":
                {
                    double dEnd = MarsGeometry(Theta0Deg, out _);
                    double dBack = MarsGeometry(Theta0Deg + 360.0 * 500 / SynodicDays, out _);
                    DistanceKm = (returning ? Lerp(dBack, 0.0001, f) : Lerp(0.0001, dEnd, Math.Pow(f, 1.3))) * AuKm;
                    SepDeg = returning ? 60 + 40 * f : 100 - 40 * f;
                    break;
                }
                case "mars_surface":
                {
                    double days = sim.HourInLeg / 24.0;
                    DistanceKm = MarsGeometry(Theta0Deg + 360.0 * days / SynodicDays, out SepDeg) * AuKm;
                    break;
                }
                default:
                    DistanceKm = 72_000;
                    break;
            }

            DistanceAu = DistanceKm / AuKm;
            LightSeconds = DistanceKm / LightKmS;
            if (DistanceKm <= 0) { EbN0Db = 99; Ber = 0; Sources.Add("enlace terrestre (sin ruido espacial)"); return; }

            EbN0Db = EbN0At1AuDb - 20.0 * Math.Log10(Math.Max(DistanceAu, 1e-7));
            Sources.Add("pérdida por distancia y ruido térmico/galáctico");
            LossProb = 0.002f;

            if (DistanceKm < 70_000)
            {
                BurstProb += 0.12f;
                Sources.Add("ionosfera y cinturones de Van Allen (ráfagas)");
            }
            if (SepDeg < 10)
            {
                EbN0Db -= (10 - SepDeg) * 0.8;
                BurstProb += 0.25f;
                Sources.Add($"centelleo por plasma solar (Sol a {SepDeg:0.0}° de la línea de visión)");
                if (SepDeg < 2) { LossProb = 0.9f; Sources.Add("conjunción solar: apagón de comunicaciones"); }
            }
            if (storm)
            {
                EbN0Db -= 3;
                BurstProb += 0.3f;
                LossProb = Mathf.Max(LossProb, 0.05f);
                Sources.Add("tormenta solar (partículas energéticas)");
            }
            double ebn0 = Math.Pow(10, EbN0Db / 10.0);
            Ber = 0.5 * Erfc(Math.Sqrt(ebn0));
            if (Ber < 1e-9) Ber = 0;
            Ber = Math.Min(Ber, 0.2);
        }

        /// <summary>Distancia Tierra-Marte (UA) y ángulo Sol-Tierra-nave (°) para una fase sinódica θ.</summary>
        static double MarsGeometry(double thetaDeg, out double sepDeg)
        {
            double t = thetaDeg * Math.PI / 180.0;
            double mx = MarsOrbitAu * Math.Cos(t) - 1.0, my = MarsOrbitAu * Math.Sin(t);   // Tierra en (1, 0)
            double d = Math.Sqrt(mx * mx + my * my);
            double cos = (-mx) / d;                                                         // vector Tierra→Sol = (-1, 0)
            sepDeg = Math.Acos(Math.Max(-1, Math.Min(1, cos))) * 180.0 / Math.PI;
            return d;
        }

        static double Lerp(double a, double b, double t) => a + (b - a) * t;

        /// <summary>erfc(x) para x ≥ 0 (Numerical Recipes, error &lt; 1,2e-7).</summary>
        static double Erfc(double x)
        {
            double t = 1.0 / (1.0 + 0.5 * x);
            return t * Math.Exp(-x * x - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 + t * (-0.18628806 +
                   t * (0.27886807 + t * (-1.13520398 + t * (1.48851587 + t * (-0.82215223 + t * 0.17087277)))))))));
        }

        /// <summary>Aplica el ruido a una transmisión. Devuelve bits invertidos y bytes afectados por ráfagas.</summary>
        public (int bits, int burst) ApplyNoise(byte[] data, System.Random rng)
        {
            int flips = 0, burst = 0;
            if (Ber > 0)
            {
                long totalBits = (long)data.Length * 8;
                double logq = Math.Log(1.0 - Ber);
                long pos = -1;
                while (true)
                {
                    pos += 1 + (long)Math.Floor(Math.Log(1.0 - rng.NextDouble()) / logq);   // salto geométrico
                    if (pos >= totalBits) break;
                    data[pos >> 3] ^= (byte)(1 << (int)(pos & 7));
                    flips++;
                }
            }
            if (rng.NextDouble() < BurstProb && data.Length > 64)
            {
                int len = rng.Next(8, 48), start = rng.Next(0, data.Length - len);
                for (int i = start; i < start + len; i++) data[i] ^= (byte)(1 << rng.Next(0, 7));
                burst = len;
                flips += len;
            }
            return (flips, burst);
        }
    }
}
