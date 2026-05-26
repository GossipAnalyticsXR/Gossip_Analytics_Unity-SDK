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

        static AddRequest _socketIORequest;
        static AddRequest _uniTaskRequest;

        static PackageChecker()
        {
#if SOCKET_IO_UNITY && UNITASK
            // Both defines already set – nothing to do.
            return;
#else
            bool missingSocektIO = System.Type.GetType("SocketIOClient.SocketIO, SocketIOUnity") == null;
            bool missingUniTask  = System.Type.GetType("Cysharp.Threading.Tasks.UniTask, UniTask")  == null;

            // If a package is already present but its define is missing, set it now.
            if (!missingSocektIO) EnsureDefine(DefineSocketIO);
            if (!missingUniTask)  EnsureDefine(DefineUniTask);

            if (!missingSocektIO && !missingUniTask)
                return; // All dependencies present — nothing to do.

            const string dialogTitle   = "Missing Package Dependencies";
            const string dialogMessage = "GossipSDK is missing one or more required dependencies.\n\nClick Install to add them automatically via the Package Manager.";
            const string okButton      = "Install";
            const string cancelButton  = "Ignore";

            bool install = EditorUtility.DisplayDialog(dialogTitle, dialogMessage, okButton, cancelButton);

            if (install)
            {
                if (missingSocektIO)
                {
                    _socketIORequest = Client.Add(PkgSocketIO);
                    EditorApplication.update += TrackSocketIOInstall;
                }
                if (missingUniTask)
                {
                    _uniTaskRequest = Client.Add(PkgUniTask);
                    EditorApplication.update += TrackUniTaskInstall;
                }
            }
            else
            {
                string msg = "GossipSDK: Please install the required dependencies via the Package Manager:";
                if (missingSocektIO) msg += $"\n\t- {PkgSocketIO}";
                if (missingUniTask)  msg += $"\n\t- {PkgUniTask}";
                Debug.LogWarning(msg);
            }
#endif
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
