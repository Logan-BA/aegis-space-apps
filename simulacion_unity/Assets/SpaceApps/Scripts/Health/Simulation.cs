using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>
    /// Lógica de la misión, sin nada visual: tripulación, ambiente, fases, eventos y bitácora.
    /// Paso = 1 hora de misión.
    /// </summary>
    public class Simulation
    {
        public readonly List<AstronautHealth> Crew = new List<AstronautHealth>();
        public readonly List<EarlyWarning> Warnings = new List<EarlyWarning>();
        public readonly CabinEnvironment Env = new CabinEnvironment();
        public readonly Countermeasures Cm = new Countermeasures();
        public readonly List<string> Log = new List<string>();

        public float SolarActivity = 0.5f;
        public int ConditioningDays = 14;
        public int MissionIndex = 4;
        public string FreeScenario = "leo";

        public readonly List<(string key, int days)> Legs = new List<(string, int)>();
        public int LegIndex, HourInLeg, MissionHours;
        public bool Finished;
        public int SpeCount;

        readonly System.Random _rng = new System.Random();
        readonly List<string> _alerts = new List<string>();
        bool _evaToday;
        int _speHour = -1;
        float _speRaw;

        public int Hour => MissionHours % 24;
        public int Day => MissionHours / 24;

        public Simulation()
        {
            for (int i = 0; i < MissionData.DefaultCrew.Length; i++)
            {
                var c = MissionData.DefaultCrew[i];
                Crew.Add(new AstronautHealth(c.name, c.role, c.fitness, c.massKg, c.heightM, c.age, ConditioningDays, i));
                Warnings.Add(new EarlyWarning());
            }
        }

        public Scenario Current => MissionData.Scenarios[CurrentKey];
        public string CurrentKey => Legs.Count == 0 ? FreeScenario : Legs[Mathf.Min(LegIndex, Legs.Count - 1)].key;

        public void ApplyMission(int index)
        {
            MissionIndex = index;
            foreach (var h in Crew) { h.ConditioningDays = ConditioningDays; h.Reset(); }
            foreach (var w in Warnings) w.Reset();
            Legs.Clear();
            var m = MissionData.Missions[index];
            if (m.Legs.Length > 0)
            {
                if (ConditioningDays > 0) Legs.Add(("conditioning", ConditioningDays));
                Legs.AddRange(m.Legs);
            }
            LegIndex = HourInLeg = MissionHours = SpeCount = 0;
            Finished = false;
            Log.Clear();
            Env.Reset(Current);
            AddLog($"<b>Misión: {m.Label}</b>");
            if (ConditioningDays < 14) AddLog("<color=orange>Advertencia: acondicionamiento menor a 14 días</color>");
        }

        public float TotalDays
        {
            get
            {
                if (Legs.Count == 0) return Mathf.Max(365f, Day * 1.1f);
                int t = 0; foreach (var l in Legs) t += l.days;
                return t;
            }
        }

        /// <summary>Avanza una hora. Devuelve true si cambió la fase (para cambiar el escenario 3D).</summary>
        public bool StepHour()
        {
            if (Finished) return false;
            var s = Current;
            int hour = Hour;

            if (hour == 0)
            {
                _evaToday = s.EvaChance > 0f && _rng.NextDouble() < s.EvaChance;
                _speHour = -1;
                if (_rng.NextDouble() < 0.002 + 0.006 * SolarActivity)
                {
                    _speHour = _rng.Next(0, 24);
                    _speRaw = 30f + (float)_rng.NextDouble() * 370f;
                    if (_rng.NextDouble() < 0.1) _speRaw = 400f + (float)_rng.NextDouble() * 800f;
                }
            }
            float spe = hour == _speHour ? _speRaw : 0f;
            if (spe > 0f && s.SpeExposure > 0f)
            {
                SpeCount++;
                AddLog($"<color=#ff9a4a>Tormenta solar ({spe:0} mSv sin blindaje){(Cm.Shelter ? " - tripulación al refugio" : "")}</color>");
            }

            Schedule(hour, s);
            float dust = (_evaToday && hour == 15) ? s.DustPerEva : 0f;
            if (dust > 0f) AddLog($"Fin de EVA: entra polvo por la esclusa (+{dust:0} µg/m³)");
            int[] cells = new int[Crew.Count];
            float[] act = new float[Crew.Count];
            for (int i = 0; i < Crew.Count; i++) { cells[i] = Crew[i].Cell; act[i] = Crew[i].Activity; }
            Env.StepHour(s, Cm, hour, cells, act, dust, _rng);

            _alerts.Clear();
            for (int i = 0; i < Crew.Count; i++)
            {
                Crew[i].StepHour(s, Cm, Env, hour, spe, _alerts);
                Warnings[i].StepHour(Crew[i], Env, _alerts);
            }
            foreach (var a in _alerts) AddLog(a);

            MissionHours++;
            if (Legs.Count == 0) return false;
            HourInLeg++;
            if (HourInLeg >= Legs[LegIndex].days * 24)
            {
                LegIndex++; HourInLeg = 0;
                if (LegIndex >= Legs.Count)
                {
                    LegIndex = Legs.Count - 1; Finished = true;
                    AddLog($"<b>Misión completada en {Day} días</b>");
                    return false;
                }
                AddLog($"<b>Nueva fase: {Current.Label}</b>");
                return true;
            }
            return false;
        }

        /// <summary>Horario diario: dónde está cada astronauta y qué hace (afecta CO2 local y signos vitales).</summary>
        void Schedule(int hour, Scenario s)
        {
            int exHours = Mathf.RoundToInt(2.5f * Cm.Exercise);
            for (int i = 0; i < Crew.Count; i++)
            {
                var h = Crew[i];
                h.OnEva = false;
                int exStart = 14 + (i % 2) * 2;
                if (hour >= 22 || hour < 6) { h.Cell = 1 + i % 3; h.Activity = 0f; }
                else if (hour == 7 || hour == 13 || hour == 19) { h.Cell = CabinEnvironment.KitchenCell; h.Activity = 1f; }
                else if (_evaToday && i >= 2 && hour >= 9 && hour < 15) { h.Cell = -1; h.Activity = 2f; h.OnEva = true; }
                else if (hour >= exStart && hour < exStart + exHours) { h.Cell = CabinEnvironment.GymCell; h.Activity = 3f; }
                else if (hour >= 20) { h.Cell = 6; h.Activity = 1f; }
                else { h.Cell = 4 + i; h.Activity = 1f; }
            }
        }

        public void AddLog(string text)
        {
            Log.Add($"<color=#8aa0c8>D{Day} {Hour:00}h</color> {text}");
            if (Log.Count > 300) Log.RemoveAt(0);
        }

        public string ExportCsv()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Resultados"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"simulacion_salud_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
            var sb = new StringBuilder();
            sb.AppendLine("astronauta,dia,densidad_osea,masa_muscular,vo2max,deficit_plasma,sans,dosis_msv,estres,salud_global," +
                          "fc_reposo,hrv_ms,pas,pad,spo2,temp_central,cortisol,glucosa,calcio_urinario,horas_sueno,co2_mmhg,pm25");
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var h in Crew)
                foreach (var r in h.History)
                    sb.AppendLine(string.Format(ci,
                        "{0},{1},{2:0.###},{3:0.###},{4:0.###},{5:0.###},{6:0.####},{7:0.##},{8:0.##},{9:0.##},{10:0.#},{11:0.#},{12:0.#},{13:0.#},{14:0.#},{15:0.##},{16:0.#},{17:0.#},{18:0},{19:0.#},{20:0.##},{21:0.#}",
                        h.Name, r.Day, r.Bone, r.Muscle, r.Vo2, r.Plasma, r.Sans, r.Dose, r.Stress, r.Health,
                        r.HrRest, r.Hrv, r.Sbp, r.Dbp, r.SpO2, r.CoreTemp, r.Cortisol, r.Glucose, r.UrinaryCa, r.SleepH,
                        r.Co2Exposure, r.Pm25Exposure));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            AddLog($"CSV guardado: {path}");
            return path;
        }
    }
}
