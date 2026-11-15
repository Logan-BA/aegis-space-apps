using System.Collections.Generic;
using SpaceApps.Bus;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>
    /// Punto de entrada. Agregar a un GameObject vacío (lo hace el menú Space Apps → Crear escena).
    /// Dos modos:
    ///  - Local: la simulación propia de Unity avanza la misión (demostración sin red).
    ///  - Bus AEGIS: Unity es la PC 3 del equipo; recibe del motor (PC 1) el mismo día, escenario y
    ///    tripulación que ve el dashboard, y solo los representa en 3D. No calcula diagnósticos.
    /// Línea de comandos de una build: -aegis ws://IP_DE_PC1:8001/ws  (conecta al iniciar)
    /// Controles: clic derecho + arrastrar = girar · rueda = zoom · Espacio = iniciar/pausar (modo local).
    /// </summary>
    public class SpaceAppsSimulation : MonoBehaviour
    {
        public enum Mode { Local, Bus }

        public Simulation Sim { get; private set; }
        public AegisLink Link { get; private set; }
        public Mode CurrentMode = Mode.Local;
        public int Selected;      // astronauta seleccionado (modo local)
        public int BusSelected;   // astronauta seleccionado (modo bus)
        public bool Running;
        public bool InteriorView;
        [Tooltip("Horas de misión simuladas por segundo")] public float HoursPerSecond = 192f;

        public OrbitCamera Orbit { get; private set; }
        public BodyMap BodyMap => _bodyMap;
        public IReadOnlyList<CrewAvatar> Avatars => _avatars;
        public string SceneKey { get; private set; } = "";
        public float SpeFlash;
        public bool Dirty = true;

        Camera _cam;
        readonly List<CrewAvatar> _avatars = new List<CrewAvatar>();
        BodyMap _bodyMap;
        GameObject _scenario;
        CabinBeacons _beacons;
        float _accum;
        int _lastSolar, _busHaptic, _lastRedCount;

        static readonly string[] InteriorCapable = { "leo", "moon_transit", "mars_transit" };
        static readonly string[] InteriorScenes = { "leo", "deep_space_moon", "deep_space_mars" };

        void Start()
        {
            Application.targetFrameRate = 60;
            HoursPerSecond = 192f;   // velocidad 2x por defecto (8 días por segundo)
            Application.runInBackground = true;   // seguir recibiendo el bus aunque la ventana pierda el foco
            Sim = new Simulation();
            Link = new AegisLink();

            _cam = Camera.main;
            if (_cam == null)
            {
                var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = cgo.AddComponent<Camera>();
                cgo.AddComponent<AudioListener>();
            }
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 30000f;
            _cam.fieldOfView = 55f;
            Orbit = _cam.GetComponent<OrbitCamera>();
            if (Orbit == null) Orbit = _cam.gameObject.AddComponent<OrbitCamera>();

            _cam.cullingMask = ~((1 << BodyMap.Layer) | (1 << SpaceApps.Assistant.HoloFace.Layer));

            // Un tripulante 3D por astronauta (modelo del equipo), en fila frente a la cámara
            var crewRoot = new GameObject("Tripulacion").transform;
            for (int i = 0; i < Sim.Crew.Count; i++)
            {
                var a = CrewAvatar.Create(i, crewRoot);
                a.transform.position = new Vector3((i - (Sim.Crew.Count - 1) / 2f) * 1.25f, 0f, 0f);
                _avatars.Add(a);
            }
            _bodyMap = BodyMap.Create();
            if (GetComponent<SpaceApps.Assistant.MedAssistant>() == null) gameObject.AddComponent<SpaceApps.Assistant.MedAssistant>();   // ASTRA, asistente médica
            if (GetComponent<SpaceApps.Enlace.EnlaceTelemetria>() == null) gameObject.AddComponent<SpaceApps.Enlace.EnlaceTelemetria>();   // telemetría cifrada a la Tierra

            if (GetComponent<SimulationUI>() == null) gameObject.AddComponent<SimulationUI>();
            ApplyMission(Sim.MissionIndex);

            var args = System.Environment.GetCommandLineArgs();
            int ai = System.Array.IndexOf(args, "-aegis");
            if (ai >= 0)
            {
                if (ai + 1 < args.Length && args[ai + 1].StartsWith("ws")) Link.Url = args[ai + 1];
                SetMode(Mode.Bus);
            }
            ApplyLaunchOptions(args);
        }

        /// <summary>
        /// Opciones de arranque (lanzador y pruebas):
        ///   -mision N      misión 0..4 (4 = Marte)        -dia N        adelanta la misión N días
        ///   -interior      vista interior                  -play         empieza corriendo
        ///   -astronauta N  astronauta seleccionado          -pestana N    pestaña del panel derecho
        ///   -capturar ruta.png [-esperar s]   guarda una captura y cierra (para revisar la app)
        ///   -preguntar "texto"   le hace esa pregunta a ASTRA al arrancar (prueba de la IA local)
        ///   -oir audio.wav       le "habla" a ASTRA con ese audio (prueba del micrófono/Whisper)
        ///   -conversar           arranca con el micrófono en manos libres
        ///   -estacion ws://IP:8765   envía la telemetría cifrada a esa estación terrena (se puede repetir)
        ///   -sinenlace           no transmite telemetría
        /// </summary>
        void ApplyLaunchOptions(string[] args)
        {
            string Arg(string name) { int i = System.Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            bool Has(string name) => System.Array.IndexOf(args, name) >= 0;

            if (CurrentMode == Mode.Local)
            {
                if (int.TryParse(Arg("-mision"), out int m) && m >= 0 && m < MissionData.Missions.Length) ApplyMission(m);
                if (int.TryParse(Arg("-dia"), out int d))
                {
                    for (int h = 0; h < d * 24 && !Sim.Finished; h++) { Sim.StepHour(); if (Sim.Hour == 0) Advisor.AutoApply(Sim); }
                    SceneKey = "";
                    UpdateScene();
                }
                if (int.TryParse(Arg("-astronauta"), out int a)) Selected = Mathf.Clamp(a, 0, Sim.Crew.Count - 1);
                if (Has("-play")) Running = true;
            }
            if (Has("-interior")) { InteriorView = true; SceneKey = ""; UpdateScene(); }
            if (int.TryParse(Arg("-pestana"), out int tab)) GetComponent<SimulationUI>().SetTab(tab);
            if (Has("-sinayuda") || Has("-play")) GetComponent<SimulationUI>().ShowHelp = false;
            Dirty = true;
            string q = Arg("-preguntar");
            if (q != null) StartCoroutine(AskAstra(q));
            string heard = Arg("-oir");
            if (heard != null) StartCoroutine(HearAstra(heard));
            if (Has("-conversar")) StartCoroutine(ConverseOnStart());
            string cap = Arg("-capturar");
            if (cap != null)
            {
                float wait = float.TryParse(Arg("-esperar"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : 4f;
                StartCoroutine(CaptureAndQuit(cap, wait));
            }
        }

        System.Collections.IEnumerator AskAstra(string question)
        {
            var astra = GetComponent<SpaceApps.Assistant.MedAssistant>();
            float t = 0f;
            while (t < 10f && (astra == null || astra.Model == null || astra.Status.StartsWith("Cargando"))) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(1f);
            if (astra != null) astra.Ask(question);
        }

        System.Collections.IEnumerator HearAstra(string wav)
        {
            var astra = GetComponent<SpaceApps.Assistant.MedAssistant>();
            float t = 0f;
            while (t < 30f && (astra == null || astra.Ear == null || !astra.Ear.Ready || astra.Model == null)) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(1f);
            if (astra != null && astra.Ear != null && System.IO.File.Exists(wav)) astra.Ear.Transcribe(SpaceApps.Assistant.VoiceListener.LoadWav16k(wav));
        }

        System.Collections.IEnumerator ConverseOnStart()
        {
            yield return null;
            var astra = GetComponent<SpaceApps.Assistant.MedAssistant>();
            if (astra != null && astra.Ear != null) astra.Ear.HandsFree = true;
        }

        System.Collections.IEnumerator CaptureAndQuit(string path, float wait)
        {
            yield return new WaitForSeconds(wait);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSeconds(1.5f);
            Application.Quit();
        }

        void OnDestroy() => Link?.Disconnect();

        public void SetMode(Mode m)
        {
            CurrentMode = m;
            Running = false;
            if (m == Mode.Bus) Link.Connect(); else Link.Disconnect();
            SceneKey = "";
            UpdateScene();
            Dirty = true;
        }

        public void ApplyMission(int index)
        {
            Sim.ApplyMission(index);
            Advisor.AutoApply(Sim);
            Running = false;
            _accum = 0f;
            UpdateScene();
            Dirty = true;
        }

        public void SetFreeScenario(string key)
        {
            Sim.FreeScenario = key;
            Sim.Env.Reset(Sim.Current);
            UpdateScene();
            Dirty = true;
        }

        public void ToggleInterior()
        {
            InteriorView = !InteriorView;
            UpdateScene();
        }

        public void UpdateScene()
        {
            string key;
            if (CurrentMode == Mode.Bus)
            {
                key = Link.SceneKey();
                if (InteriorView && System.Array.IndexOf(InteriorScenes, key) >= 0) key = "ship_interior";
            }
            else
            {
                key = Sim.Current.Scene;
                if (InteriorView && System.Array.IndexOf(InteriorCapable, Sim.CurrentKey) >= 0) key = "ship_interior";
            }
            if (key == SceneKey && _scenario != null) return;
            SceneKey = key;
            if (_scenario) Destroy(_scenario);
            _scenario = ScenarioBuilder.Build(key, _cam);
            _beacons = CurrentMode == Mode.Local ? _scenario.GetComponentInChildren<CabinBeacons>() : null;
            if (_beacons) _beacons.Show(Sim.Env);
        }

        void Update()
        {
            if (CurrentMode == Mode.Bus) UpdateBus(); else UpdateLocal();
            SpeFlash = Mathf.Max(0f, SpeFlash - Time.deltaTime * 0.8f);
        }

        void UpdateLocal()
        {
            if (OrbitCamera.SpacePressed() && !SimulationUI.TextFocus) TogglePlay();
            if (Running && !Sim.Finished)
            {
                _accum += Time.deltaTime * HoursPerSecond;
                int steps = 0;
                while (_accum >= 1f && steps < 2000 && !Sim.Finished)
                {
                    _accum -= 1f; steps++;
                    int spe = Sim.SpeCount;
                    if (Sim.StepHour()) UpdateScene();
                    if (Sim.Hour == 0) Advisor.AutoApply(Sim);   // el asesor recalcula las contramedidas cada día
                    if (Sim.SpeCount > spe && Sim.Current.SpeExposure > 0f) SpeFlash = 1f;
                    Dirty = true;
                }
                if (Sim.Finished) Running = false;
            }
            var s = Sim.Current;
            float g = s.Gravity + (s.Transit ? Sim.Cm.ArtificialG : 0f);
            for (int i = 0; i < _avatars.Count; i++)
            {
                _avatars[i].gameObject.SetActive(true);
                _avatars[i].SetHealth(Sim.Crew[i], Sim.Warnings[i], g < 0.05f, i == Selected);
            }
            _bodyMap.SetSeverity(BodyMap.FromHealth(Sim.Crew[Selected]));
            if (_beacons && Dirty) _beacons.Show(Sim.Env);
            FollowAndPick(Selected, i => { Selected = i; Dirty = true; });
        }

        void UpdateBus()
        {
            Link.Poll();
            if (Link.Changed)
            {
                Link.Changed = false;
                UpdateScene();
                Dirty = true;
                if (Link.SolarEventCounter != _lastSolar) { _lastSolar = Link.SolarEventCounter; SpeFlash = 1f; }
                int reds = 0; foreach (var a in Link.Alerts) if (a.IsRed) reds++;
                if (reds > _lastRedCount) _busHaptic++;
                _lastRedCount = reds;
            }
            BusSelected = Mathf.Clamp(BusSelected, 0, Mathf.Max(0, Link.Crew.Count - 1));
            for (int i = 0; i < _avatars.Count; i++)
            {
                bool has = i < Link.Crew.Count;
                _avatars[i].gameObject.SetActive(has || (Link.Crew.Count == 0 && i == 0));
                if (has) _avatars[i].SetExternal(BusSeverity(Link.Crew[i]), Floating(SceneKey), BusLed(Link.Crew[i]), i == BusSelected);
                else if (i == 0) _avatars[i].SetExternal(new Dictionary<string, float>(), Floating(SceneKey), Color.gray, true);
            }
            _bodyMap.SetSeverity(Link.Crew.Count > 0 ? BodyMap.FromBus(BusSeverity(Link.Crew[BusSelected])) : new Dictionary<string, float>());
            FollowAndPick(BusSelected, i => { if (i < Link.Crew.Count) { BusSelected = i; Dirty = true; } });
        }

        /// <summary>La cámara sigue al tripulante elegido; clic sobre un tripulante en 3D lo selecciona.</summary>
        void FollowAndPick(int selected, System.Action<int> select)
        {
            if (selected >= 0 && selected < _avatars.Count)
            {
                var target = _avatars[selected].transform.position + Vector3.up * 1.1f;
                Orbit.Target = Vector3.Lerp(Orbit.Target, target, Mathf.Clamp01(Time.deltaTime * 3f));
            }
            if (!Orbit.BlockInput && OrbitCamera.LeftClicked(out var mouse))
            {
                var ray = _cam.ScreenPointToRay(mouse);
                if (Physics.Raycast(ray, out var hit, 200f) && hit.collider.TryGetComponent<CrewAvatar>(out var av)) select(av.Index);
            }
        }

        /// <summary>Posición en pantalla (coordenadas GUI) de la etiqueta del tripulante i; false si no se ve.</summary>
        public bool TagPosition(int i, float guiScale, out Vector2 pos)
        {
            pos = default;
            if (i < 0 || i >= _avatars.Count || !_avatars[i].gameObject.activeInHierarchy) return false;
            var sp = _cam.WorldToScreenPoint(_avatars[i].transform.position + Vector3.up * 2.15f);
            if (sp.z <= 0f) return false;
            pos = new Vector2(sp.x / guiScale, (Screen.height - sp.y) / guiScale);
            return true;
        }

        static bool Floating(string scene) => scene == "leo" || scene.StartsWith("deep_space") || scene == "ship_interior";

        /// <summary>
        /// Intensidad visual 0..1 por zona a partir de los indicadores del motor. Es solo una escala de
        /// color para la escena (no un umbral clínico): 0 = igual que el inicio de la misión.
        /// </summary>
        public static Dictionary<string, float> BusSeverity(AegisAstronaut a)
        {
            var d = new Dictionary<string, float>();
            if (a.BonePct is float b) { float s = Mathf.Clamp01((100f - b) / 20f); d["legs"] = s; d["spine"] = s; d["pelvis"] = s; }
            if (a.AerobicPct is float v) d["chest"] = Mathf.Clamp01((100f - v) / 30f);
            if (a.SleepH is double sl) d["head"] = Mathf.Clamp01((8f - (float)sl) / 4f);
            if (a.DoseMsv is double dose) d["radiation"] = Mathf.Clamp01((float)dose / 1000f);
            return d;
        }

        /// <summary>LED del astronauta según las alertas que envía el motor (rojo si alguna es de nivel alto).</summary>
        public Color BusLed(AegisAstronaut a)
        {
            bool any = false, red = false;
            foreach (var al in Link.Alerts)
            {
                if (al.AstronautId != null && al.AstronautId != a.Id && al.AstronautId != a.Name) continue;
                any = true; red |= al.IsRed;
            }
            return red ? new Color(1f, 0.15f, 0.1f) : any ? new Color(1f, 0.8f, 0.1f) : new Color(0.2f, 1f, 0.35f);
        }

        public void TogglePlay()
        {
            if (CurrentMode == Mode.Bus) return;
            if (Sim.Finished) ApplyMission(Sim.MissionIndex);
            Running = !Running;
        }
    }
}
