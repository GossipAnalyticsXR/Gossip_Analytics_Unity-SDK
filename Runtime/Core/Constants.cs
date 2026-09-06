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
        // No se lee de package.json en runtime a proposito: UnityEditor.PackageManager
        // no existe en un build, asi que no hay forma de resolverlo desde el paquete.
        public const string SdkVersion = "2.0.2";
    }
}
