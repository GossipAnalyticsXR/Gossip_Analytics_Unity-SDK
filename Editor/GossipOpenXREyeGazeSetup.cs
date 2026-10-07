#if OPENXR_PRESENT
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;

namespace GossipSDK.Editor
{
    // Enciende el perfil de interaccion Eye Gaze de OpenXR, que es lo que hace aparecer el
    // InputDevice de mirada. Es la otra mitad de GossipMetaEyeTrackingSetup: aquel pone
    // eyeTrackingSupport en el OVRProjectConfig, y este enciende la casilla de OpenXR.
    //
    // Sin ella, OpenXREyeGazeProvider.IsAvailable -que es eyeDevice.isValid- se queda en
    // false para siempre, TryGetEyeGaze devuelve false y TrackingSource no sale nunca de
    // "head", por mucho que el usuario conceda el permiso.
    //
    // Medido el 04-10-2026 en VR-Anatomy-Lab, con permiso concedido, eyeTrackingSupport a 1
    // y el paquete de Meta en 205.0.0: una sesion en Quest Pro con eye tracking real dio 27
    // fijaciones y las 27 con fuente "head". Lo unico apagado era esta casilla.
    //
    // Se aplica solo, igual que el de Meta: ningun cliente tiene que acordarse. Solo
    // enciende lo que falta; no apaga nunca nada.
    [InitializeOnLoad]
    internal static class GossipOpenXREyeGazeSetup
    {
        // El id lo publica el propio paquete en EyeGazeInteraction.featureId. Se escribe
        // literal para no obligar a este asmdef a referenciar el ensamblado de runtime de
        // OpenXR solo por una constante.
        private const string EyeGazeFeatureId = "com.unity.openxr.feature.input.eyetracking";

        private static readonly BuildTargetGroup[] Grupos =
        {
            BuildTargetGroup.Android,
            BuildTargetGroup.Standalone,
        };

        static GossipOpenXREyeGazeSetup()
        {
            EditorApplication.delayCall += EnsureEyeGazeInteractionEnabled;
        }

        private static void EnsureEyeGazeInteractionEnabled()
        {
            // El setter de OpenXRFeature.enabled se niega -y escribe un LogError- si OpenXR
            // ya esta corriendo. En Play no se toca nada.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            foreach (var grupo in Grupos)
            {
                var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(grupo, EyeGazeFeatureId);
                if (feature == null) continue;
                if (feature.enabled) continue;

                // El propio setter llama a EditorUtility.SetDirty, asi que no hace falta aqui.
                feature.enabled = true;
                Debug.Log($"[Gossip] Enabled OpenXR Eye Gaze Interaction Profile for {grupo} (required for eye-gaze heatmaps).");
            }
        }
    }
}
#endif
