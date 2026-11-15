using System.Collections.Generic;

namespace SpaceApps
{
    /// <summary>Datos de un escenario. Valores de referencia explicados en el informe del proyecto.</summary>
    public class Scenario
    {
        public string Key, Label, Scene;
        public float Gravity;        // g (1 = Tierra)
        public float DoseRate;       // mSv/día por rayos cósmicos (GCR)
        public float SpeExposure;    // fracción de un evento solar que llega (0 = protegido)
        public float Isolation;      // carga psicológica por aislamiento/distancia
        public float Circadian;      // 0 = ciclo día/noche normal ... 1 = muy alterado
        public bool Transit;         // viaje (admite gravedad artificial)
        public bool Cabin;           // atmósfera artificial de cabina (si no, aire exterior de la Tierra)
        public float NoiseBaseDb;    // ruido de fondo (ventiladores, bombas)
        public float DustPerEva;     // µg/m³ de PM2.5 que entra por la esclusa en cada EVA
        public float EvaChance;      // probabilidad diaria de una caminata exterior
    }

    public static class MissionData
    {
        public static readonly Dictionary<string, Scenario> Scenarios = new Dictionary<string, Scenario>
        {
            ["earth"] = new Scenario { Key = "earth", Label = "Tierra (recuperación)", Scene = "earth", Gravity = 1f, DoseRate = 0.008f, SpeExposure = 0f, Isolation = 0f, Circadian = 0f, Cabin = false, NoiseBaseDb = 50f },
            ["conditioning"] = new Scenario { Key = "conditioning", Label = "Acondicionamiento previo", Scene = "earth", Gravity = 1f, DoseRate = 0.008f, SpeExposure = 0f, Isolation = 0.2f, Circadian = 0.1f, Cabin = false, NoiseBaseDb = 50f },
            ["leo"] = new Scenario { Key = "leo", Label = "Órbita baja / ISS", Scene = "leo", Gravity = 0f, DoseRate = 0.5f, SpeExposure = 0.05f, Isolation = 0.6f, Circadian = 0.6f, Cabin = true, NoiseBaseDb = 62f, DustPerEva = 5f, EvaChance = 0.03f },
            ["moon_transit"] = new Scenario { Key = "moon_transit", Label = "Tránsito Tierra-Luna", Scene = "deep_space_moon", Gravity = 0f, DoseRate = 1.4f, SpeExposure = 1f, Isolation = 0.8f, Circadian = 0.3f, Transit = true, Cabin = true, NoiseBaseDb = 60f },
            ["moon_surface"] = new Scenario { Key = "moon_surface", Label = "Superficie lunar", Scene = "moon_surface", Gravity = 0.165f, DoseRate = 1.4f, SpeExposure = 0.5f, Isolation = 0.9f, Circadian = 0.4f, Cabin = true, NoiseBaseDb = 55f, DustPerEva = 160f, EvaChance = 0.5f },
            ["mars_transit"] = new Scenario { Key = "mars_transit", Label = "Tránsito a Marte", Scene = "deep_space_mars", Gravity = 0f, DoseRate = 1.8f, SpeExposure = 1f, Isolation = 1.2f, Circadian = 0.3f, Transit = true, Cabin = true, NoiseBaseDb = 60f },
            ["mars_surface"] = new Scenario { Key = "mars_surface", Label = "Superficie de Marte", Scene = "mars_surface", Gravity = 0.38f, DoseRate = 0.64f, SpeExposure = 0.3f, Isolation = 1.3f, Circadian = 0.2f, Cabin = true, NoiseBaseDb = 55f, DustPerEva = 90f, EvaChance = 0.3f },
        };

        public static readonly string[] ScenarioOrder = { "earth", "leo", "moon_transit", "moon_surface", "mars_transit", "mars_surface" };

        public class Mission
        {
            public string Label;
            public (string key, int days)[] Legs;
        }

        public static readonly Mission[] Missions =
        {
            new Mission { Label = "Modo libre", Legs = new (string, int)[0] },
            new Mission { Label = "Estación ISS · 6 meses", Legs = new[] { ("leo", 180), ("earth", 90) } },
            new Mission { Label = "Estación ISS · 1 año", Legs = new[] { ("leo", 365), ("earth", 120) } },
            new Mission { Label = "Luna · 40 días", Legs = new[] { ("moon_transit", 5), ("moon_surface", 30), ("moon_transit", 5), ("earth", 60) } },
            new Mission { Label = "Marte · ida y vuelta", Legs = new[] { ("mars_transit", 210), ("mars_surface", 500), ("mars_transit", 210), ("earth", 180) } },
        };

        /// <summary>Tripulación por defecto: nombre, rol, condición física (0..1), masa (kg), talla (m), edad.</summary>
        public static readonly (string name, string role, float fitness, float massKg, float heightM, int age)[] DefaultCrew =
        {
            ("CMD Alfa", "Comandante", 0.85f, 78f, 1.80f, 45),
            ("PIL Bravo", "Piloto", 0.70f, 72f, 1.74f, 38),
            ("MED Charlie", "Oficial médico", 0.75f, 63f, 1.66f, 41),
            ("ING Delta", "Especialista", 0.55f, 84f, 1.78f, 35),
        };
    }

    /// <summary>Contramedidas que el usuario ajusta en el panel.</summary>
    public class Countermeasures
    {
        public float Exercise = 0.8f;      // cumplimiento del programa de ejercicio (0..1)
        public float Shielding = 0.5f;     // blindaje de la nave (0..1) -> hasta -35 % GCR
        public bool Shelter = true;        // refugio para tormentas solares (-90 % de un SPE)
        public float Scrubber = 0.3f;      // depurador de CO2 y filtro HEPA (0..1)
        public float Support = 0.6f;       // apoyo psicológico (0..1)
        public float ArtificialG = 0f;     // gravedad artificial en tránsito (g)
        public float Lighting = 0.5f;      // iluminación circadiana (0..1) reduce la alteración del sueño
    }
}
