using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;

namespace GossipAnalytics.Editor.Dependencies
{
    /// <summary>
    /// Automatically prompts the developer to install missing package dependencies
    /// when the project is loaded in Unity Editor. Runs unconditionally so the dialog
    /// always appears even when none of the scripting-define symbols are set yet.
    /// </summary>
    [InitializeOnLoad]
    public class PackageChecker
    {
        static PackageChecker()
        {
            bool missingSocektIO = System.Type.GetType("SocketIOClient.SocketIO, SocketIOUnity") == null;
            bool missingUniTask = System.Type.GetType("Cysharp.Threading.Tasks.UniTask, UniTask") == null;
#if !SOCKET_IO_UNITY
            missingSocektIO = true;
#endif
#if !UNITASK
            missingUniTask = true;
#endif

            if (!missingSocektIO && !missingUniTask)
                return; // All dependencies present — nothing to do.

            const string dialogTitle  = "Missing Package Dependencies";
            const string dialogMessage = "GossipSDK is missing one or more required dependencies.\n\nClick Install to add them automatically via the Package Manager.";
            const string okButton     = "Install";
            const string cancelButton = "Ignore";

            bool install = EditorUtility.DisplayDialog(dialogTitle, dialogMessage, okButton, cancelButton);

            if (install)
            {
#if !SOCKET_IO_UNITY
                Client.Add("https://github.com/itisnajim/SocketIOUnity.git#v1.1.4");
#endif
#if !UNITASK
                Client.Add("https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10");
#endif
            }
            else
            {
                string msg = "GossipSDK: Please install the required dependencies via the Package Manager:";
#if !SOCKET_IO_UNITY
                msg += $"\n\t- https://github.com/itisnajim/SocketIOUnity.git#v1.1.4";
#endif
#if !UNITASK
                msg += $"\n\t- https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10";
#endif
                Debug.LogWarning(msg);
            }
        }
    }
}
