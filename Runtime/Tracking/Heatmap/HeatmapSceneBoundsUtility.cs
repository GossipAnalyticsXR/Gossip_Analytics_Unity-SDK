using UnityEngine;
using GossipSDK.Components;

namespace GossipSDK.Heatmaps
{
    public static class HeatmapSceneBoundsUtility
    {
        /// <summary>
        /// Margen sobre la union instrumentada. 1.5 deja sitio para estar de pie
        /// alrededor de lo que se mide sin volver a encuadrar media escena.
        /// </summary>
        public const float FrameMargin = 1.5f;

        /// <summary>Lado minimo del marco, en metros, para la escena que instrumenta dos botones.</summary>
        public const float MinFrameSide = 10f;

        public const string RuleInstrumented = "instrumented-union-x1.5-min10";
        public const string RuleRenderers = "renderers-union-x1.5-min10";

        /// <summary>
        /// El marco que encuadra la escena: cuadrado, con margen y lado minimo.
        /// Lo usan LA CAMARA y el spec, asi que la imagen y la caja declarada son
        /// el mismo rectangulo. Hasta la 2.0.34 no lo eran: la camara sumaba su
        /// propio padding de 2 m y el spec escribia los bounds crudos.
        ///
        /// Por que no vale la union de TODOS los renderers, que es lo que se hacia:
        /// el marco lo fijaba el renderer mas grande, y ese es el suelo. Medido el
        /// 04-10-2026 sobre las 60 imagenes de escena guardadas, las tres escenas
        /// declaraban 100.0 x 100.0 m identicos al decimal, que es el plano Ground
        /// a escala 10; el contenido real de Hospital Zone cabe en 6.5 x 7.8 m.
        /// </summary>
        public static Bounds CalculateFrame(out string rule)
        {
            Bounds baseBounds;

            if (TryInstrumentedBounds(out baseBounds))
            {
                rule = RuleInstrumented;
            }
            else
            {
                baseBounds = CalculateSceneBounds();
                rule = RuleRenderers;
            }

            float side = Mathf.Max(baseBounds.size.x, baseBounds.size.z) * FrameMargin;
            if (side < MinFrameSide) side = MinFrameSide;

            float alto = Mathf.Max(baseBounds.size.y, 0.01f);
            return new Bounds(baseBounds.center, new Vector3(side, alto, side));
        }

        /// <summary>
        /// Union de las cajas de los objetos instrumentados, con la MISMA regla que
        /// SceneInventoryComponent: los Renderer de sus hijos, incluidos los
        /// desactivados, y si no tiene ninguno sus Collider. Devuelve false cuando
        /// la escena no instrumenta nada.
        /// </summary>
        private static bool TryInstrumentedBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            var hay = false;

            foreach (var instrumentado in Object.FindObjectsByType<InteractableComponent>(FindObjectsSortMode.None))
            {
                if ((Object)instrumentado == null) continue;

                Bounds caja;
                if (!TryCajaDe(instrumentado.gameObject, out caja)) continue;

                if (!hay) { bounds = caja; hay = true; }
                else bounds.Encapsulate(caja);
            }

            return hay;
        }

        private static bool TryCajaDe(GameObject go, out Bounds caja)
        {
            caja = new Bounds();
            if ((Object)go == null) return false;

            var hay = false;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if ((Object)r == null) continue;
                if (!hay) { caja = r.bounds; hay = true; }
                else caja.Encapsulate(r.bounds);
            }

            if (hay) return true;

            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                if ((Object)c == null) continue;
                if (!hay) { caja = c.bounds; hay = true; }
                else caja.Encapsulate(c.bounds);
            }

            return hay;
        }

        /// <summary>
        /// Union de TODOS los renderers. Queda como RESPALDO de CalculateFrame para
        /// la escena que no instrumenta nada; ya no se usa directamente como marco.
        /// </summary>
        public static Bounds CalculateSceneBounds()
        {
            var renderers = Object.FindObjectsOfType<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one * 10f);

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }
    }
}
