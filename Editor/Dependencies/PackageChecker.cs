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
            bool missingSocketIO = !System.Type.GetType("SocketIOClient.SocketIO, SocketIOUnity") is object;
            bool missingUniTask  = !System.Type.GetType("Cysharp.Threading.Tasks.UniTask, UniTask") is object;

#if !SOCKET_IO_UNITY
            missingSocketIO = true;
#endif
#if !UNITASK
            missingUniTask = true;
#endif

            if (!missingSocketIO && !missingUniTask)
                return; // All dependencies present — nothing to do.

            const string dialogTitle   = "Missing Package Dependencies";
            const string dialogMessage = "GossipSDK is missing one or more required dependencies.\n\nClick Install to add them automatically via the Package Manager.";
            const string okButton      = "Install";
            const string cancelButton  = "Ignore";

            bool install = EditorUtility.DisplayDialog(dialogTitle, dialogMessage, okButton, cancelButton);

            if (install)
            {
#if !SOCKET_IO_UNITY
                Client.Add(Constants.SocketIOUnity);
#endif
#if !UNITASK
                Client.Add(Constants.UniTask);
#endif
            }
            else
            {
                string msg = "GossipSDK: Please install the required dependencies via the Package Manager:";
#if !SOCKET_IO_UNITY
                msg += $"\n\t- {Constants.SocketIOUnity}";
#endif
#if !UNITASK
                msg += $"\n\t- {Constants.UniTask}";
#endif
                Debug.LogWarning(msg);
            }
        }
    }
}
