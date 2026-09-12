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
        public const string SdkVersion = "2.0.16";
    }
}
