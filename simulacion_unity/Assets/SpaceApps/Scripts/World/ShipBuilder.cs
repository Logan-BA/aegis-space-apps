using UnityEngine;

namespace SpaceApps
{
    /// <summary>
    /// Geometría de respaldo (metros, eje largo +Y, base en y = 0) con las MISMAS medidas que la
    /// macro freecad/nave_spaceapps.py. Si existe el modelo de FreeCAD/Blender, se usa ese.
    /// </summary>
    public static class ShipBuilder
    {
        static Material White => Mats.Lit(new Color(0.88f, 0.88f, 0.86f), 0.1f, 0.45f);
        static Material Grey => Mats.Lit(new Color(0.55f, 0.56f, 0.6f), 0.7f, 0.6f);
        static Material Dark => Mats.Lit(new Color(0.15f, 0.15f, 0.17f), 0.8f, 0.55f);
        static Material Gold => Mats.Lit(new Color(0.85f, 0.65f, 0.25f), 0.9f, 0.7f);
        static Material Solar => Mats.Lit(new Color(0.10f, 0.14f, 0.38f), 0.6f, 0.8f);
        static Material Orange => Mats.Lit(new Color(0.85f, 0.45f, 0.15f), 0f, 0.25f);
        static Material Glass => Mats.Lit(new Color(0.05f, 0.12f, 0.2f), 0.9f, 0.95f);

        /// <summary>Nave de tránsito: 39,3 m de alto, 52,4 m de envergadura.</summary>
        public static GameObject TransitShip()
        {
            var root = new GameObject("NaveTransito").transform;
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.PI * 2f * i / 3f;
                MeshGen.Cyl(root, 0.4f, 1.2f, 3f, Dark, new Vector3(Mathf.Cos(a) * 2.5f, 1.5f, Mathf.Sin(a) * 2.5f), default, $"Tobera{i + 1}");
            }
            MeshGen.Cyl(root, 4.2f, 4.2f, 15f, Gold, new Vector3(0, 10.5f, 0), default, "EtapaPropulsion");
            MeshGen.Box(root, new Vector3(0.08f, 10f, 5f), White, new Vector3(0, 10.5f, 6.7f), default, "Radiador1");
            MeshGen.Box(root, new Vector3(0.08f, 10f, 5f), White, new Vector3(0, 10.5f, -6.7f), default, "Radiador2");
            MeshGen.Cyl(root, 4.2f, 4.2f, 12f, White, new Vector3(0, 24f, 0), default, "Habitat");
            MeshGen.Cyl(root, 4.22f, 4.22f, 0.6f, Glass, new Vector3(0, 26f, 0), default, "Ventanas");
            foreach (float s in new[] { -1f, 1f })
            {
                MeshGen.Cyl(root, 0.15f, 0.15f, 2f, Grey, new Vector3(s * 5.2f, 24f, 0), new Vector3(0, 0, 90), "Brazo", 12);
                MeshGen.Box(root, new Vector3(20f, 0.06f, 6f), Solar, new Vector3(s * 16.2f, 24f, 0), default, "PanelSolar");
            }
            MeshGen.Cyl(root, 1f, 1f, 2f, Grey, new Vector3(0, 31f, 0), default, "Adaptador");
            MeshGen.Cyl(root, 2.5f, 2.5f, 4f, White, new Vector3(0, 34f, 0), default, "ModuloServicio");
            MeshGen.Cyl(root, 0.75f, 2.5f, 3.3f, Grey, new Vector3(0, 37.65f, 0), default, "Capsula");
            return root.gameObject;
        }

        /// <summary>Cohete lanzador de carga pesada: ~107 m.</summary>
        public static GameObject Launcher()
        {
            var root = new GameObject("CoheteLanzador").transform;
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * 2f * i / 4f + Mathf.PI / 4f;
                MeshGen.Cyl(root, 0.35f, 0.7f, 4f, Dark, new Vector3(Mathf.Cos(a) * 1.8f, 2f, Mathf.Sin(a) * 1.8f), default, $"Motor{i + 1}");
            }
            MeshGen.Cyl(root, 4.2f, 4.2f, 65f, Orange, new Vector3(0, 36.5f, 0), default, "EtapaCentral");
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (4.2f + 1.85f + 0.3f);
                MeshGen.Cyl(root, 0.65f, 1.3f, 3f, Dark, new Vector3(x, 2.5f, 0), default, "ToberaPropulsor");
                MeshGen.Cyl(root, 1.85f, 1.85f, 54f, White, new Vector3(x, 31f, 0), default, "Propulsor");
                MeshGen.Cyl(root, 0f, 1.85f, 5f, White, new Vector3(x, 60.5f, 0), default, "OjivaPropulsor");
            }
            MeshGen.Cyl(root, 2.5f, 4.2f, 4f, Orange, new Vector3(0, 71f, 0), default, "Interetapa");
            MeshGen.Cyl(root, 2.5f, 2.5f, 14f, White, new Vector3(0, 80f, 0), default, "EtapaSuperior");
            MeshGen.Cyl(root, 2.5f, 2.5f, 2f, Grey, new Vector3(0, 88f, 0), default, "AdaptadorCarga");
            MeshGen.Cyl(root, 2.5f, 2.5f, 4f, White, new Vector3(0, 91f, 0), default, "ModuloServicio");
            MeshGen.Cyl(root, 0.75f, 2.5f, 3.3f, Grey, new Vector3(0, 94.65f, 0), default, "Capsula");
            MeshGen.Cyl(root, 0.3f, 0.75f, 3f, White, new Vector3(0, 97.8f, 0), default, "CubiertaEscape");
            MeshGen.Cyl(root, 0.2f, 0.2f, 6f, Grey, new Vector3(0, 102.3f, 0), default, "TorreEscape", 12);
            MeshGen.Cyl(root, 0f, 0.25f, 1.5f, Dark, new Vector3(0, 106.05f, 0), default, "PuntaEscape", 12);
            return root.gameObject;
        }

        /// <summary>Estación modular genérica en órbita baja (~100 m de armadura).</summary>
        public static GameObject Station()
        {
            var root = new GameObject("Estacion").transform;
            var truss = Mats.Lit(new Color(0.6f, 0.6f, 0.62f), 0.7f, 0.5f);
            MeshGen.Box(root, new Vector3(100, 2.5f, 2.5f), truss, Vector3.zero, default, "Armadura");
            MeshGen.Cyl(root, 2.1f, 2.1f, 44f, White, new Vector3(0, -4.5f, 0), new Vector3(90, 0, 0), "ModulosPresurizados");
            MeshGen.Cyl(root, 2.1f, 2.1f, 16f, White, new Vector3(0, -4.5f, 9f), new Vector3(0, 0, 90), "Nodo");
            MeshGen.Cyl(root, 1.6f, 1.6f, 6f, Gold, new Vector3(0, -4.5f, -24f), new Vector3(90, 0, 0), "ModuloServicio");
            foreach (float x in new[] { -45f, -33f, 33f, 45f })
                foreach (float z in new[] { -19f, 19f })
                    MeshGen.Box(root, new Vector3(11f, 0.08f, 34f), Solar, new Vector3(x, 0, z), default, "PanelSolar");
            foreach (float x in new[] { -16f, 16f })
                MeshGen.Box(root, new Vector3(3f, 0.1f, 14f), White, new Vector3(x, -1.5f, 9f), new Vector3(0, 0, 30), "Radiador");
            return root.gameObject;
        }

        public static GameObject Lander()
        {
            var root = new GameObject("ModuloLunar").transform;
            MeshGen.Box(root, new Vector3(4.2f, 2f, 4.2f), Gold, new Vector3(0, 2.6f, 0), default, "Descenso");
            MeshGen.Cyl(root, 1.6f, 2f, 3f, White, new Vector3(0, 5.1f, 0), default, "Ascenso");
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * 2f * i / 4f + Mathf.PI / 4f;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 foot = d * 3.6f, attach = d * 1.6f + Vector3.up * 2.6f, v = attach - foot;
                var leg = MeshGen.Cyl(root, 0.08f, 0.08f, v.magnitude, Grey, (foot + attach) * 0.5f, default, "Pata", 8);
                leg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, v);
                MeshGen.Cyl(root, 0.45f, 0.45f, 0.1f, Grey, foot + Vector3.up * 0.05f, default, "Pie", 16);
            }
            return root.gameObject;
        }

        public static GameObject MarsHabitat()
        {
            var root = new GameObject("HabitatMarte").transform;
            var dome = MeshGen.Ball(root, 5f, White, Vector3.zero, "Domo");
            dome.transform.localScale = new Vector3(10f, 8f, 10f);
            MeshGen.Cyl(root, 1.2f, 1.2f, 6f, Grey, new Vector3(7.5f, 1.2f, 0), new Vector3(0, 0, 90), "Tunel");
            MeshGen.Cyl(root, 2.4f, 2.4f, 9f, White, new Vector3(13f, 2.4f, 0), new Vector3(90, 0, 0), "Modulo");
            return root.gameObject;
        }
    }
}
