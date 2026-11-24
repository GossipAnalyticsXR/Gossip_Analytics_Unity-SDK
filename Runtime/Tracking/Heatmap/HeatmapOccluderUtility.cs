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
        public bool isEnvironment; // el SDK dice que es sala o entorno, no un objeto
    }

    public static class HeatmapOccluderUtility
    {
        // Cajas AABB (mundo) de occluders significativos (edificio, pedestales, props).
        // Excluye piso/ground y objetos diminutos. Se limita a las N mas grandes.
        public static HeatmapOccluder[] Collect(int maxCount = 32, float minSize = 0.4f)
        {
            var renderers = Object.FindObjectsOfType<Renderer>(); // mismo estilo que HeatmapSceneBoundsUtility
            var list = new System.Collections.Generic.List<HeatmapOccluder>();
            // Medido el 24-nov-2026 en Hospital, en Production y en Development: la
            // caja del propio edificio llegaba aqui como un occluder mas, el dashboard
            // no podia distinguirla porque solo recibia centro y tamano, y tapaba 33
            // de 171 pares camara-marca.
            //
            // La unica senal que separa una sala de un objeto sin depender de umbrales
            // de tamano es que el jugador este DENTRO de ella: una sala te contiene, un
            // pulmon no. Se mide en la captura, al arrancar la escena, cuando todavia
            // no ha agarrado nada.
            //
            // El guardia de instrumentado va por si se captura mas tarde: medido, la
            // regla de camara-dentro a secas perdia 24 de 171 ocultaciones legitimas,
            // todas de organos que el usuario se habia acercado a la cara.
            var cabeza = Camera.main;
            bool hayCabeza = (UnityEngine.Object)cabeza != null;
            Vector3 posCabeza = hayCabeza ? cabeza.transform.position : Vector3.zero;
            foreach (var r in renderers)
            {
                if ((UnityEngine.Object)r == null) continue;
                var b = r.bounds;
                var s = b.size;
                float maxDim = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                if (maxDim < minSize) continue;          // salta diminutos
                if (IsGroundLike(r, s)) continue;        // salta piso/ground/plane
                bool instrumentado = r.GetComponentInParent<InteractableComponent>() != null;
                bool entorno = !instrumentado && hayCabeza && b.Contains(posCabeza);
                list.Add(new HeatmapOccluder {
                    cx = b.center.x, cy = b.center.y, cz = b.center.z,
                    sx = s.x, sy = s.y, sz = s.z,
                    name = r.gameObject.name,
                    isEnvironment = entorno
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
