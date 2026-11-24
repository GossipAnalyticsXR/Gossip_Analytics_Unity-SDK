namespace GossipSDK.Core
{
    public class Constants
    {
        // public const string ServerUrl = "http://localhost:3500";

        // Motor de desarrollo de ESTE build del SDK. Cada fork por motor cambia SOLO este valor
        // ("Unreal", "Godot", "WebGL", ...). Se propaga al sobre de todos los eventos como Engine.
        public const string Engine = "Unity";

        // Version de ESTE build del SDK. Se sube A LA VEZ que package.json y el
        // CHANGELOG: si se olvida, `sdk_version` vuelve a mentir, que es justo el bug
        // que esto arregla (mandaba Application.version, o sea la version de la APP).
        //
        // Ya se olvido DOS veces: los bumps a 2.0.3 y a 2.0.4 solo tocaron
        // package.json, y esta constante se quedo en 2.0.2 mientras el paquete iba por
        // 2.0.4. Si cambias package.json, cambia esta linea en el mismo commit.
        //
        // No se lee de package.json en runtime a proposito: UnityEditor.PackageManager
        // no existe en un build, asi que no hay forma de resolverlo desde el paquete.
        public const string SdkVersion = "2.0.68";
        // Capa donde el SDK cuelga las cajas de mirada de la UI de mundo. La crea el
        // editor (GossipAnalyticsSetup.AsegurarCapaDeMirada) al hacer Quick Setup, al abrir
        // el Instrumentation Manager y en cada build, asi que al integrador no se le pide
        // nada.
        //
        // Capa propia y no la del boton: la capa es del GameObject, no del collider, asi
        // que mover el boton aqui le romperia el culling de la camara y el GraphicRaycaster,
        // y una caja de fisica en la capa del objeto entraria en los raycast del juego
        // -- XRRayInteractor, agarre, teleport -- y convertiria la analitica en un bug de
        // jugabilidad. El rayo de mirada la ve igual porque EyeTrackingComponent:27
        // raycastea con ~0.
        public const string GazeLayerName = "GossipGaze";
    }
}
