using UnityEngine.SceneManagement;

namespace GossipSDK.Core.Configuration
{
    /// <summary>
    /// Identidad estable de la escena activa.
    ///
    /// El nombre de una escena en Unity es el nombre de su fichero .unity: al
    /// renombrarlo cambian el nombre Y la ruta, asi que ninguno de los dos
    /// identifica la escena. El GUID del asset si: el .meta viaja con el fichero.
    ///
    /// El GUID solo se lee desde el editor (AssetDatabase). Para un build lo hornea
    /// GossipBuildPreprocessor dentro de GossipSettings, y aqui se busca por ruta.
    /// En el editor se resuelve en vivo, porque el play mode no dispara ningun build.
    /// </summary>
    public static class SceneIdentity
    {
        /// <summary>
        /// GUID del fichero .unity de la escena activa, o cadena vacia si no se pudo
        /// resolver (escena creada en runtime, cargada de un bundle, o ruta que el
        /// build no horneo). Vacio significa: agrupa por el nombre, como hasta ahora.
        ///
        /// SOLO desde el hilo principal: ni SceneManager ni AssetDatabase son validos
        /// en un hilo de fondo. Un sobre que se serialice dentro de
        /// UniTask.RunOnThreadPool tiene que capturar el valor ANTES de entrar.
        /// </summary>
        public static string CurrentSceneGuid(GossipSettings settings)
        {
            string path = SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(path)) return string.Empty;

#if UNITY_EDITOR
            string vivo = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(vivo)) return vivo;
#endif

            if (settings == null) return string.Empty;
            return settings.SceneGuidForPath(path);
        }
    }
}
