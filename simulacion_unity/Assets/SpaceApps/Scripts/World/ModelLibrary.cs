using UnityEngine;

namespace SpaceApps
{
    /// <summary>
    /// Carga modelos importados desde Assets/SpaceApps/Resources/Models/ en este orden:
    ///   Models/&lt;nombre&gt;          -> FBX detallado en Blender (blender/*.py)
    ///   Models/freecad/&lt;nombre&gt;  -> OBJ por piezas exportado por FreeCAD
    /// Si no existe, devuelve null y se usa la geometría procedural.
    ///
    /// Nombres: nave_transito, cohete_lanzador, estacion, astronauta, modulo_lunar,
    ///          modulo_marte, interior_nave
    /// </summary>
    public static class ModelLibrary
    {
        /// <param name="groundAtZero">false para interiores: solo se centra en X/Z y se respeta la altura del modelo</param>
        public static GameObject TryLoad(string name, float targetSize = 0f, bool groundAtZero = true)
        {
            var prefab = Resources.Load<GameObject>("Models/" + name);
            if (prefab == null) prefab = Resources.Load<GameObject>("Models/freecad/" + name);
            if (prefab == null) return null;
            var holder = new GameObject(name);
            var model = Object.Instantiate(prefab, holder.transform);
            model.name = prefab.name;
            Normalize(holder.transform, model.transform, targetSize, groundAtZero);
            TryBounds(holder.transform, out var b);
            Debug.Log($"[SpaceApps][ModelLibrary] usando modelo importado: {name} · tamaño {b.size.x:0.0} x {b.size.y:0.0} x {b.size.z:0.0} m");
            return holder;
        }

        /// <summary>Escala mm→m si hace falta (o a targetSize) y apoya la base en y = 0, centrado en X/Z.</summary>
        static void Normalize(Transform holder, Transform model, float targetSize, bool groundAtZero)
        {
            if (!TryBounds(holder, out var b)) return;
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (targetSize > 0f) model.localScale *= targetSize / longest;
            else if (longest > 500f) model.localScale *= 0.001f;
            if (!TryBounds(holder, out b)) return;
            float dy = groundAtZero ? b.min.y - holder.position.y : 0f;
            model.position -= new Vector3(b.center.x - holder.position.x, dy, b.center.z - holder.position.z);
        }

        static bool TryBounds(Transform root, out Bounds b)
        {
            b = default;
            bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
