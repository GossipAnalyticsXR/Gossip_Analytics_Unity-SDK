using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;
using System.Linq;

namespace GossipAnalytics.Editor.Dependencies
{
    /// <summary>
    /// On editor load, checks whether SocketIOUnity and UniTask are present.
    /// If both scripting-define symbols are already set, exits immediately.
    /// Otherwise falls back to a Type.GetType check.  If packages are missing,
    /// shows a dialog offering to install them via the Package Manager.
    /// After each package installs successfully its scripting-define symbol is
    /// added so that subsequent editor loads skip the dialog entirely.
    /// </summary>
    [InitializeOnLoad]
    public class PackageChecker
    {
        const string DefineSocketIO = "SOCKET_IO_UNITY";
        const string DefineUniTask  = "UNITASK";

        const string PkgSocketIO = "https://github.com/itisnajim/SocketIOUnity.git#v1.1.4";
        const string PkgUniTask  = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10";
        const string PkgXRManagement = "com.unity.xr.management";
        const string PkgInputSystem  = "com.unity.inputsystem";

        static AddRequest _socketIORequest;
        static AddRequest _uniTaskRequest;
        static AddRequest _xrMgmtRequest;
        static AddRequest _inputSysRequest;

        private static void AutoFixActiveInputHandling()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var ps   = new SerializedObject(assets[0]);
            var prop = ps.FindProperty("activeInputHandler");
            // 0 = InputManager (Old), 1 = Input System (New), 2 = Both
            if (prop != null && prop.intValue == 0)
            {
                prop.intValue = 2;
                ps.ApplyModifiedProperties();
                Debug.Log("GossipSDK: Active Input Handling set to 'Both' automatically.");
            }
        }

        static PackageChecker()
        {
            // Always auto-fix Active Input Handling — no dialog needed
            AutoFixActiveInputHandling();

            // Detect missing packages via reflection
            bool missingSocketIO = System.Type.GetType("SocketIOUnity, SocketIOUnityAssembly") == null;
            bool missingUniTask  = System.Type.GetType("Cysharp.Threading.Tasks.UniTask, UniTask") == null;
            bool missingXRMgmt   = System.Type.GetType("UnityEngine.XR.Management.XRGeneralSettings, Unity.XR.Management") == null;
            bool missingInputSys = System.Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem") == null;

            // XR Provider: user must have either OpenXR OR Oculus XR (not auto-installed — user must choose)
            bool hasOpenXR   = System.Type.GetType("UnityEngine.XR.OpenXR.OpenXRRuntime, Unity.XR.OpenXR") != null;
            bool hasOculusXR = System.Type.GetType("Unity.XR.Oculus.OculusLoader, Unity.XR.Oculus") != null;
            bool missingXRProvider = !hasOpenXR && !hasOculusXR;

            // Set defines for packages that are already present
            if (!missingSocketIO) EnsureDefine(DefineSocketIO);
            if (!missingUniTask)  EnsureDefine(DefineUniTask);

            // If nothing is missing, return early
            if (!missingSocketIO && !missingUniTask && !missingXRMgmt && !missingInputSys && !missingXRProvider)
                return;

            // Build dialog message listing only what is missing
            var missingList = new System.Collections.Generic.List<string>();
            if (missingSocketIO)   missingList.Add("• SocketIOUnity (auto-install available)");
            if (missingUniTask)    missingList.Add("• UniTask (auto-install available)");
            if (missingXRMgmt)     missingList.Add("• XR Plugin Management (auto-install available)");
            if (missingInputSys)   missingList.Add("• Input System (auto-install available)");
            if (missingXRProvider) missingList.Add("• XR Provider: install OpenXR OR Oculus XR manually in Package Manager → choose based on your target device");

            string dialogMessage =
                "Gossip Analytics SDK is missing required dependencies:\n\n" +
                string.Join("\n", missingList) +
                "\n\nClick Install to automatically install what can be installed." +
                (missingXRProvider ? "\n\n⚠ XR Provider requires manual installation." : "");

            bool install = EditorUtility.DisplayDialog(
                "Gossip Analytics — Missing Dependencies",
                dialogMessage,
                "Install", "Ignore");

            if (install)
            {
                if (missingSocketIO) { _socketIORequest  = Client.Add(PkgSocketIO);      EditorApplication.update += TrackSocketIOInstall; }
                if (missingUniTask)  { _uniTaskRequest   = Client.Add(PkgUniTask);       EditorApplication.update += TrackUniTaskInstall; }
                if (missingXRMgmt)   { _xrMgmtRequest    = Client.Add(PkgXRManagement);  EditorApplication.update += TrackXRMgmtInstall; }
                if (missingInputSys) { _inputSysRequest   = Client.Add(PkgInputSystem);   EditorApplication.update += TrackInputSysInstall; }
                if (missingXRProvider)
                    Debug.LogWarning("GossipSDK: Install an XR Provider via Package Manager → + → Add package from registry → search for 'OpenXR' or 'Oculus XR Plugin'.");
            }
            else
            {
                var sb = new System.Text.StringBuilder("GossipSDK: Install these dependencies manually:\n");
                if (missingSocketIO)   sb.AppendLine($"\t- {PkgSocketIO}");
                if (missingUniTask)    sb.AppendLine($"\t- {PkgUniTask}");
                if (missingXRMgmt)     sb.AppendLine("\t- com.unity.xr.management");
                if (missingInputSys)   sb.AppendLine("\t- com.unity.inputsystem");
                if (missingXRProvider) sb.AppendLine("\t- com.unity.xr.openxr OR com.unity.xr.oculus");
                Debug.LogWarning(sb.ToString());
            }
        }
        static void TrackSocketIOInstall()
        {
            if (_socketIORequest == null || !_socketIORequest.IsCompleted) return;
            EditorApplication.update -= TrackSocketIOInstall;

            if (_socketIORequest.Status == StatusCode.Success)
            {
                Debug.Log("GossipSDK: SocketIOUnity installed successfully.");
                EnsureDefine(DefineSocketIO);
            }
            else
            {
                Debug.LogError($"GossipSDK: Failed to install SocketIOUnity: {_socketIORequest.Error?.message}");
            }
            _socketIORequest = null;
        }

        static void TrackUniTaskInstall()
        {
            if (_uniTaskRequest == null || !_uniTaskRequest.IsCompleted) return;
            EditorApplication.update -= TrackUniTaskInstall;

            if (_uniTaskRequest.Status == StatusCode.Success)
            {
                Debug.Log("GossipSDK: UniTask installed successfully.");
                EnsureDefine(DefineUniTask);
            }
            else
            {
                Debug.LogError($"GossipSDK: Failed to install UniTask: {_uniTaskRequest.Error?.message}");
            }
            _uniTaskRequest = null;
        }

        static void TrackXRMgmtInstall()
        {
            if (_xrMgmtRequest == null || !_xrMgmtRequest.IsCompleted) return;
            EditorApplication.update -= TrackXRMgmtInstall;
            if (_xrMgmtRequest.Status == StatusCode.Success)
                Debug.Log("GossipSDK: XR Plugin Management installed successfully.");
            else
                Debug.LogError($"GossipSDK: Failed to install XR Plugin Management: {_xrMgmtRequest.Error?.message}");
            _xrMgmtRequest = null;
        }

        static void TrackInputSysInstall()
        {
            if (_inputSysRequest == null || !_inputSysRequest.IsCompleted) return;
            EditorApplication.update -= TrackInputSysInstall;
            if (_inputSysRequest.Status == StatusCode.Success)
                Debug.Log("GossipSDK: Input System installed successfully.");
            else
                Debug.LogError($"GossipSDK: Failed to install Input System: {_inputSysRequest.Error?.message}");
            _inputSysRequest = null;
        }

        /// <summary>
        /// Adds <paramref name="define"/> to every BuildTargetGroup that currently
        /// lacks it, then triggers a script recompilation.
        /// </summary>
        static void EnsureDefine(string define)
        {
            var groups = new[]
            {
                BuildTargetGroup.Standalone,
                BuildTargetGroup.Android,
                BuildTargetGroup.iOS,
            };

            bool changed = false;
            foreach (var group in groups)
            {
                var raw     = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                var defines = new List<string>(raw.Split(';'));
                if (!defines.Contains(define))
                {
                    defines.Add(define);
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defines));
                    changed = true;
                }
            }

            if (changed)
                Debug.Log($"GossipSDK: Scripting define '{define}' added. Recompiling...");
        }
    }
}
