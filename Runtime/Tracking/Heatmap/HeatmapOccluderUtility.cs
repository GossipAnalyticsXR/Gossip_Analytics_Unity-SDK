using UnityEngine;
using GossipSDK.Components;

namespace GossipSDK.Heatmaps
{
    [System.Serializable]
    public class HeatmapOccluder
    {
        public float cx, cy, cz;   // world-space center
        public float sx, sy, sz;   // full size (world AABB)
        public string name;        // nombre del Renderer, para poder diagnosticar
        public bool instrumented; // pertenece a un objeto con InteractableComponent
    }

    public static class HeatmapOccluderUtility
    {
        // Cajas AABB (mundo) de occluders significativos (edificio, pedestales, props).
        // Excluye piso/ground y objetos diminutos. Se limita a las N mas grandes.
        public static HeatmapOccluder[] Collect(int maxCount = 32, float minSize = 0.4f)
        {
            var renderers = Object.FindObjectsOfType<Renderer>(); // mismo estilo que HeatmapSceneBoundsUtility
            var list = new System.Collections.Generic.List<HeatmapOccluder>();
            // El occluder viaja SIN veredicto: solo con hechos, su nombre y si
            // pertenece a un objeto instrumentado. Quien decide si una caja es la
            // sala es el dashboard, y no por gusto.
            //
            // Medido el 25-nov-2026 en Hospital: la caja del edificio contiene 20
            // de las 27 posiciones de captura de la sesion, y las cajas de objetos
            // contienen 0. Separacion perfecta. Pero en el instante en que se arma
            // esta lista -medio segundo tras cargar la escena- la unica posicion
            // que existe es el spawn, y en esta escena cae en z 2,004 con el
            // edificio empezando en z 2,5: fuera. Decidirlo aqui daba falso en las
            // seis cajas. El dato que falta es temporal y aqui todavia no existe.
            foreach (var r in renderers)
            {
                if ((UnityEngine.Object)r == null) continue;
                var b = r.bounds;
                var s = b.size;
                float maxDim = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                if (maxDim < minSize) continue;          // salta diminutos
                if (IsGroundLike(r, s)) continue;        // salta piso/ground/plane
                bool instrumentado = r.GetComponentInParent<InteractableComponent>() != null;
                list.Add(new HeatmapOccluder {
                    cx = b.center.x, cy = b.center.y, cz = b.center.z,
                    sx = s.x, sy = s.y, sz = s.z,
                    name = r.gameObject.name,
                    instrumented = instrumentado
                });
            }
            list.Sort((a, bb) => (bb.sx * bb.sy * bb.sz).CompareTo(a.sx * a.sy * a.sz));
            if (list.Count > maxCount) list.RemoveRange(maxCount, list.Count - maxCount);
            return list.ToArray();
        }

        private static bool IsGroundLike(Renderer r, Vector3 size)
        {
            string n = r.gameObject.name.ToLowerInvariant();
            if (n.Contains("ground") || n.Contains("floor") || n.Contains("plane") || n.Contains("terrain"))
                return true;
            if (size.y < 0.5f && (size.x > 20f || size.z > 20f)) // slab plano y enorme = piso
                return true;
            return false;
        }
    }
}
