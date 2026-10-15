using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GossipAnalytics.Editor
{
    public static class GossipUninstaller
    {
        // ---- MenuItems ----

        [MenuItem("Window/Gossip Analytics/Uninstall SDK", false, 100)]
        public static void RunUninstall()
        {
            // Antes que nada: los scripts del usuario que usan el SDK no compilan
            // cuando el paquete se va, y con errores de compilacion Unity NO puede
            // recargar el dominio: el menu y los componentes siguen a la vista como si
            // no se hubiera desinstalado nada, hasta cerrar el Editor. Medido el
            // 13-sep-2026 en un proyecto real: un unico fichero suelto en Assets/ con
            // using GossipSDK bastaba para que tres desinstalaciones seguidas
            // pareciesen no hacer nada (Editor.log: CS0246 entre intento y intento).
            var referencias = FindUserScriptsReferencingSdk();
            string aviso = string.Empty;
            if (referencias.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("\n\nWARNING: these scripts of yours use the SDK and will not compile once it is gone:\n");
                for (int i = 0; i < referencias.Count && i < 10; i++) sb.Append("\n- " + referencias[i]);
                if (referencias.Count > 10) sb.Append("\n- ...and " + (referencias.Count - 10) + " more");
                sb.Append("\n\nWhile there are compile errors Unity cannot reload the domain, so the Editor will keep showing the SDK even though it is already uninstalled. Delete them or move them out of Assets/ before continuing.");
                aviso = sb.ToString();
            }

            // El detalle completo va a la Consola ANTES de preguntar, tambien si se cancela:
            // DisplayDialog no tiene scroll ni se puede copiar, asi que un proyecto con 80
            // escenas no cabe dentro. En el modal van dos numeros; los nombres, aqui.
            var conteo = ContarEscenas();
            LogearConteo(conteo);

            int n = conteo.aLimpiar.Count;
            string frase = n == 0
                ? "The SDK is not in any of your scenes."
                : ("The SDK comes out of " + n + (n == 1 ? " scene." : " scenes."));

            int m = conteo.intactas.Count;
            string intactas = m == 0
                ? string.Empty
                : (m == 1
                    ? "\n\n1 other scene is not touched and will show errors when you open it. Details in the Console."
                    : "\n\n" + m + " other scenes are not touched and will show errors when you open them. Details in the Console.");

            // Boton por defecto = Keep SDK. En DisplayDialog el primer boton es el que
            // responde a Enter, y quien viene de darle Enter a tres dialogos seguidos de
            // Unity no deberia desinstalar por inercia.
            bool keep = EditorUtility.DisplayDialog(
                "Uninstall Gossip Analytics SDK",
                frase + " Your scenes and your own objects stay."
                + "\n\nYour API keys and tracking choices go too. Everything is backed up first, so you can put it back."
                + intactas + aviso,
                "Keep SDK",
                "Uninstall");

            if (keep) return;

            // El respaldo va antes del paso 1, que es el que guarda las escenas.
            string respaldo = RespaldarAntesDeBorrar(conteo.aLimpiar);
            if (respaldo != null)
                Debug.Log("[GossipUninstaller] Backup written to: " + respaldo);

            try
            {
                RunStep1_RemoveSceneComponents();
                RunStep2_DeleteAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // El aviso de "listo" ya no sale aqui. El paso 3 es asincrono y este dialogo
            // se mostraba ANTES de pedirle nada a UPM: decias Close y el borrado seguia
            // corriendo por detras. Ahora lo muestra RunStep3_RemovePackage cuando UPM
            // confirma que el paquete se fue, que es cuando termina lo nuestro.
            // Tampoco se limpia aqui la barra de progreso: la apaga el paso 3 al acabar.
            RunStep3_RemovePackage();
        }

        [MenuItem("Window/Gossip Analytics/Uninstall SDK", true, 100)]
        public static bool RunUninstall_Validate()
        {
            return true;
        }

        // ---- Step 1: Remove SDK components from all build scenes ----

        // ---- Pasada en seco: cuantas escenas se tocan y cuantas se quedan con el SDK ----

        private sealed class ConteoEscenas
        {
            public List<string> aLimpiar = new List<string>();   // se tocan y se guardan
            public List<string> intactas = new List<string>();   // llevan SDK y NO se tocan
        }

        private static ConteoEscenas ContarEscenas()
        {
            var r = new ConteoEscenas();
            var destino = new HashSet<string>(StringComparer.Ordinal);

            // 1) Escenas abiertas: se mira en memoria, que es exacto aunque no esten guardadas.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (!sc.isLoaded || string.IsNullOrEmpty(sc.path)) continue;
                destino.Add(sc.path);
                if (EscenaAbiertaTieneSdk(sc)) r.aLimpiar.Add(sc.path);
            }

            // 2) Escenas de Build Settings activadas que no estaban abiertas: por texto.
            foreach (var sceneRef in EditorBuildSettings.scenes)
            {
                if (!sceneRef.enabled || destino.Contains(sceneRef.path)) continue;
                destino.Add(sceneRef.path);
                if (FicheroDeEscenaTieneSdk(sceneRef.path)) r.aLimpiar.Add(sceneRef.path);
            }

            // 3) El resto de escenas del proyecto no se tocan, pero si llevan SDK quedaran con
            //    scripts perdidos cuando el paquete se vaya. Eso se dice ANTES de pulsar.
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(ruta) || destino.Contains(ruta)) continue;
                if (FicheroDeEscenaTieneSdk(ruta)) r.intactas.Add(ruta);
            }

            return r;
        }

        private static void LogearConteo(ConteoEscenas c)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[GossipUninstaller] Dry run - ").Append(c.aLimpiar.Count)
              .Append(" scene(s) the SDK comes out of:");
            if (c.aLimpiar.Count == 0) sb.Append(" none");
            foreach (var p in c.aLimpiar) sb.Append("\n  ").Append(p);
            sb.Append("\n[GossipUninstaller] ").Append(c.intactas.Count)
              .Append(" scene(s) NOT touched (not open and not enabled in Build Settings) that still contain SDK objects:");
            if (c.intactas.Count == 0) sb.Append(" none");
            foreach (var p in c.intactas) sb.Append("\n  ").Append(p);
            Debug.Log(sb.ToString());
        }

        private static bool EscenaAbiertaTieneSdk(Scene scene)
        {
            var tipos = GetGossipComponentTypes();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                foreach (var comp in root.GetComponentsInChildren<Component>(true))
                    if (comp != null && IsGossipComponent(comp.GetType(), tipos)) return true;
            }
            return false;
        }

        // No se abre la escena para contar: con 80 escenas, abrirlas una por una costaria mas
        // que la propia desinstalacion, y abrir descarta lo que no este guardado. Los dos
        // marcadores que el SDK deja en el .unity son el guid del prefab del manager y su
        // nombre. Puede sobrar -nunca faltar- y para un aviso ese es el lado seguro.
        private static bool FicheroDeEscenaTieneSdk(string rutaRelativa)
        {
            try
            {
                if (string.IsNullOrEmpty(rutaRelativa)) return false;
                string absoluta = System.IO.Path.Combine(RaizDelProyecto(), rutaRelativa);
                if (!System.IO.File.Exists(absoluta)) return false;
                string texto = System.IO.File.ReadAllText(absoluta);
                if (texto.IndexOf(ResolveManagerPrefabGuid(), StringComparison.Ordinal) >= 0) return true;
                return texto.IndexOf("Gossip", StringComparison.Ordinal) >= 0;
            }
            catch { return false; }
        }

        // ---- Respaldo, antes de tocar nada ----

        private static string RaizDelProyecto()
        {
            return System.IO.Directory.GetParent(Application.dataPath).FullName;
        }

        // El respaldo va FUERA de Assets/. Dentro, Unity lo importaria y el paso 2, que busca
        // los assets por tipo con FindAssets, encontraria tambien las copias y borraria el
        // propio respaldo.
        private static string RespaldarAntesDeBorrar(List<string> escenas)
        {
            string raiz = RaizDelProyecto();
            string carpeta = System.IO.Path.Combine(raiz, "GossipUninstallBackup",
                DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            try
            {
                System.IO.Directory.CreateDirectory(carpeta);
                foreach (var ruta in escenas) CopiarAlRespaldo(raiz, ruta, carpeta);
                foreach (var g in AssetDatabase.FindAssets("t:GossipAnalyticsSettings"))
                    CopiarAlRespaldo(raiz, AssetDatabase.GUIDToAssetPath(g), carpeta);
                foreach (var g in AssetDatabase.FindAssets("t:GossipInstrumentationData"))
                    CopiarAlRespaldo(raiz, AssetDatabase.GUIDToAssetPath(g), carpeta);
                return carpeta;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GossipUninstaller] Backup failed: " + ex.Message);
                return null;
            }
        }

        private static void CopiarAlRespaldo(string raiz, string rutaRelativa, string carpeta)
        {
            if (string.IsNullOrEmpty(rutaRelativa)) return;
            string origen = System.IO.Path.Combine(raiz, rutaRelativa);
            if (!System.IO.File.Exists(origen)) return;
            string destino = System.IO.Path.Combine(carpeta, rutaRelativa);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destino));
            System.IO.File.Copy(origen, destino, true);
            if (System.IO.File.Exists(origen + ".meta"))
                System.IO.File.Copy(origen + ".meta", destino + ".meta", true);
        }

private static void RunStep1_RemoveSceneComponents()
        {
            string originalScenePath = SceneManager.GetActiveScene().path;
            var processed = new HashSet<string>();

            // 1) Clean ALL open scenes in-place (covers active scene even if NOT in Build Settings).
            var open = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (sc.isLoaded) open.Add(sc);
            }
            foreach (var sc in open)
            {
                try
                {
                    RemoveGossipComponentsFromScene(sc);
                    if (!string.IsNullOrEmpty(sc.path)) { EditorSceneManager.SaveScene(sc); processed.Add(sc.path); }
                }
                catch (Exception ex) { Debug.LogWarning("[GossipUninstaller] Open scene " + sc.path + ": " + ex.Message); }
            }

            // 2) Build Settings scenes not yet processed.
            var buildScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                var sceneRef = buildScenes[i];
                if (!sceneRef.enabled || processed.Contains(sceneRef.path)) continue;

                EditorUtility.DisplayProgressBar("Gossip Analytics - Uninstalling...",
                    string.Format("Removing Gossip components... ({0} of {1})", i + 1, buildScenes.Length),
                    (float)(i + 1) / (buildScenes.Length + 2));

                try
                {
                    var scene = EditorSceneManager.OpenScene(sceneRef.path, OpenSceneMode.Single);
                    RemoveGossipComponentsFromScene(scene);
                    EditorSceneManager.SaveScene(scene);
                    processed.Add(sceneRef.path);
                }
                catch (Exception ex) { Debug.LogWarning("[GossipUninstaller] Scene " + sceneRef.path + ": " + ex.Message); }
            }

            if (!string.IsNullOrEmpty(originalScenePath) && SceneManager.GetActiveScene().path != originalScenePath)
            {
                try { EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single); } catch { }
            }
        }

        private static void RemoveGossipComponentsFromScene(Scene scene)
        {
            var gossipTypes = GetGossipComponentTypes();
            string managerPrefabGuid = ResolveManagerPrefabGuid();

            // Step A — destroy SDK-owned GameObjects entirely (guards verified before destruction).
            var toDestroy = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = tr.gameObject;
                    if (go.transform.childCount != 0) continue;             // never destroy a GO with children
                    if (IsSdkOwnedGameObject(go, managerPrefabGuid, gossipTypes)) toDestroy.Add(go);
                }

            foreach (var go in toDestroy)
                if (go != null) { try { Undo.DestroyObjectImmediate(go); } catch (Exception ex) { Debug.LogWarning("[GossipUninstaller] GO: " + ex.Message); } }

            // Step B — remove SDK components remaining on USER objects (component only).
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                foreach (var comp in root.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;
                    if (IsGossipComponent(comp.GetType(), gossipTypes))
                    {
                        try { Undo.DestroyObjectImmediate(comp); }
                        catch (Exception ex) { Debug.LogWarning("[GossipUninstaller] Comp " + comp.GetType().Name + ": " + ex.Message); }
                    }
                }
            }
        }

        private static string ResolveManagerPrefabGuid()
        {
            var pg = AssetDatabase.FindAssets("GossipAnalyticsManager t:Prefab", new[] { "Assets/Samples" });
            if (pg.Length > 0) return pg[0];
            return "29e83dfafdc2bf442a91a4129b076e3e"; // fallback to known prefab GUID
        }

        private static bool IsSdkOwnedGameObject(GameObject go, string managerPrefabGuid, List<Type> gossipTypes)
        {
            // Manager (healthy or with core removed): match by source prefab GUID — most robust.
            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (src != null)
                {
                    var g = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(src));
                    if (!string.IsNullOrEmpty(managerPrefabGuid) && g == managerPrefabGuid) return true;
                }
            }

            // Standalone SDK hosts: only if ALL non-Transform components are SDK-owned.
            if (!AllComponentsAreSdk(go, gossipTypes)) return false;
            if (GoHasComponentNamed(go, "GossipManager")) return true;
            if (go.name == "VRPermissionsHandler" && GoHasComponentNamed(go, "VRPermissionsHandler")) return true;
            if (go.name == "Gossip Device Trackers") return true;
            return false;
        }

        private static bool AllComponentsAreSdk(GameObject go, List<Type> gossipTypes)
        {
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;      // missing script (core removed) -> ignore
                if (comp is Transform) continue;
                if (!IsGossipComponent(comp.GetType(), gossipTypes)) return false; // user component present
            }
            return true;
        }

        private static bool GoHasComponentNamed(GameObject go, string typeName)
        {
            foreach (var comp in go.GetComponents<Component>())
                if (comp != null && comp.GetType().Name == typeName) return true;
            return false;
        }

        private static bool IsGossipComponent(Type compType, List<Type> explicitTypes)
        {
            if (explicitTypes.Contains(compType)) return true;
            if (!typeof(MonoBehaviour).IsAssignableFrom(compType)) return false;
            var asmName = compType.Assembly.GetName().Name;
            if (asmName == "GossipSDK.Runtime" || asmName == "GossipSDK.Editor") return true; // robust catch-all
            var ns = compType.Namespace ?? string.Empty;
            if (ns.StartsWith("GossipSDK")) return true;
            return false;
        }

                private static List<Type> GetGossipComponentTypes()
        {
            var result = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Explicit type names to always remove
            var explicitNames = new HashSet<string>
            {
                "GossipAnalyticsManager",
                "InteractableComponent",
                "VRPermissionsHandler",
                "GossipPositionTracker",
                "GossipRotationTracker",
                "GossipBalanceTracker",
                "GossipPostureTracker",
                "GossipHeadsetTracker",
                "GossipControllerTracker",
                "GossipEyeTracker",
                "PauseComponent",
                "PassthroughComponent",
            };

            foreach (var asm in assemblies)
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (explicitNames.Contains(t.Name) && typeof(Component).IsAssignableFrom(t))
                            result.Add(t);
                    }
                }
                catch { /* skip assemblies that throw on GetTypes() */ }
            }

            return result;
        }

        // ---- Step 2: Delete known Gossip assets ----

        private static void RunStep2_DeleteAssets()
        {
            EditorUtility.DisplayProgressBar(
                "Gossip Analytics â Uninstalling...",
                "Deleting Gossip assets...",
                0.75f);

            // Step 2 — delete settings asset (search by type AND by filename as fallback)
            EditorUtility.DisplayProgressBar("Gossip Uninstaller", "Removing settings asset...", 0.4f);

            var deletedPaths = new System.Collections.Generic.List<string>();

            // Primary: find by ScriptableObject type
            string[] settingsGuids = AssetDatabase.FindAssets("t:GossipAnalyticsSettings");
            foreach (var guid in settingsGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.DeleteAsset(path))
                {
                    deletedPaths.Add(path);
                    Debug.Log($"GossipSDK Uninstaller: deleted {path}");
                }
            }

            // Fallback: find by filename in case type search missed it
            string[] nameGuids = AssetDatabase.FindAssets("GossipAnalyticsSettings");
            foreach (var guid in nameGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!deletedPaths.Contains(path))
                {
                    if (AssetDatabase.DeleteAsset(path))
                    {
                        Debug.Log($"GossipSDK Uninstaller (fallback): deleted {path}");
                    }
                    else
                    {
                        Debug.LogWarning($"GossipSDK Uninstaller: could not delete {path} — delete it manually.");
                    }
                }
            }
            // Delete GossipInstrumentationData asset
            string[] dataGuids = AssetDatabase.FindAssets("t:GossipInstrumentationData");
            foreach (var guid in dataGuids)
            {
                try
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AssetDatabase.DeleteAsset(path);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GossipUninstaller] Error deleting data: " + ex.Message);
                }
            }

            // FIX 2: Delete Assets/Gossip Analytics/ folder if empty
            string gossipDir = "Assets/Gossip Analytics";
            if (AssetDatabase.IsValidFolder(gossipDir))
            {
                var remaining = AssetDatabase.FindAssets("", new[] { gossipDir });
                if (remaining.Length == 0)
                    AssetDatabase.DeleteAsset(gossipDir);
            }

            // Delete Samples folder
            string samplesPath = "Assets/Samples/Gossip Analytics SDK";
            try
            {
                if (AssetDatabase.IsValidFolder(samplesPath) ||
                    System.IO.Directory.Exists(samplesPath))
                AssetDatabase.DeleteAsset(samplesPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GossipUninstaller] Error deleting samples: " + ex.Message);
            }

            AssetDatabase.Refresh();
        }

        // ---- Pre-flight: scripts del usuario que referencian el SDK ----

        private static List<string> FindUserScriptsReferencingSdk()
        {
            var encontrados = new List<string>();
            try
            {
                string assets  = Application.dataPath;
                string samples = System.IO.Path.Combine(assets, "Samples", "Gossip Analytics SDK");
                var ficheros   = System.IO.Directory.GetFiles(assets, "*.cs", System.IO.SearchOption.AllDirectories);
                foreach (var f in ficheros)
                {
                    // El sample lo borra el paso 2, asi que no cuenta como codigo del usuario.
                    if (f.StartsWith(samples, StringComparison.Ordinal)) continue;
                    string texto;
                    try { texto = System.IO.File.ReadAllText(f); }
                    catch { continue; }
                    if (texto.IndexOf("GossipSDK", StringComparison.Ordinal) < 0) continue;
                    encontrados.Add("Assets" + f.Substring(assets.Length).Replace('\\', '/'));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GossipUninstaller] Could not scan for SDK references: " + ex.Message);
            }
            return encontrados;
        }

        // ---- Step 3: Remove package ----

        // Client.Remove es ASINCRONO. La version anterior lanzaba la peticion y, sin
        // esperarla, borraba a mano Library/PackageCache/com.gossip.core@*: le quitaba a
        // UPM el directorio del que el propio Editor esta ejecutando sus ensamblados
        // mientras la operacion seguia en vuelo. Ahora se espera a que termine y solo
        // entonces se limpia la cache.
        private static void RunStep3_RemovePackage()
        {
            EditorUtility.DisplayProgressBar(
                "Gossip Analytics - Uninstalling...",
                "Removing com.gossip.core package...",
                0.9f);

            UnityEditor.PackageManager.Requests.RemoveRequest request = null;

            try
            {
                request = UnityEditor.PackageManager.Client.Remove("com.gossip.core");
                Debug.Log("[GossipUninstaller] Package removal requested: com.gossip.core");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GossipUninstaller] Could not remove package com.gossip.core: " + ex.Message);
            }

            if (request == null)
            {
                EditorUtility.ClearProgressBar();
                CleanPackageCache();
                EditorUtility.DisplayDialog(
                    "Gossip Analytics - uninstall incomplete",
                    "Components and assets were removed, but the removal of the com.gossip.core package could not even be requested. Check the console and remove it by hand from the Package Manager.",
                    "Close");
                return;
            }

            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (!request.IsCompleted) return;

                EditorApplication.update -= poll;
                EditorUtility.ClearProgressBar();

                if (request.Status == UnityEditor.PackageManager.StatusCode.Failure)
                {
                    string motivo = request.Error != null ? request.Error.message : "unknown error";
                    Debug.LogWarning("[GossipUninstaller] UPM could not remove com.gossip.core: " + motivo);
                }

                CleanPackageCache();

                bool ok = request.Status != UnityEditor.PackageManager.StatusCode.Failure;
                EditorUtility.DisplayDialog(
                    ok ? "Gossip Analytics removed" : "Gossip Analytics - uninstall incomplete",
                    ok
                        ? "The SDK is uninstalled: components out of your scenes, assets deleted and the com.gossip.core package removed from the project. Your scenes and your scripts are untouched.\n\nUnity will recompile now; that takes a few seconds and is no longer up to us.\n\nIf any script of yours used the SDK, you will see compile errors until you remove it.\n\nWe hope to see you again soon."
                        : "Components and assets were removed, but UPM could not remove the com.gossip.core package. Check the console for the reason and remove it by hand from the Package Manager.",
                    "Close");
            };
            EditorApplication.update += poll;
        }

        // Con una dependencia git Unity cachea por hash, y una reinstalacion puede
        // reutilizar la copia vieja: por eso se limpia. Si el dominio se recarga antes de
        // que esto corra no pasa nada, Unity poda las entradas sin referenciar en el
        // siguiente resolve.
        private static void CleanPackageCache()
        {
            try
            {
                string projectRoot  = System.IO.Path.GetDirectoryName(Application.dataPath);
                string packageCache = System.IO.Path.Combine(projectRoot, "Library", "PackageCache");
                if (System.IO.Directory.Exists(packageCache))
                {
                    var gossipDirs = System.IO.Directory.GetDirectories(packageCache, "com.gossip.core@*");
                    foreach (var dir in gossipDirs)
                    {
                        System.IO.Directory.Delete(dir, true);
                        Debug.Log("[GossipUninstaller] Deleted PackageCache entry: " + dir);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GossipUninstaller] Could not clean PackageCache: " + ex.Message);
            }
        }
    }
}
