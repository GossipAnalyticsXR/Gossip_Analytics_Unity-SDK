#if !SOCKET_IO_UNITY || !UNITASK
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;

namespace Editor.Dependencies
{
    [InitializeOnLoad]
    public class PackageChecker
    {
        static PackageChecker()
        {
            const string dialogTitle = "Missing Package Dependencies";
            const string dialogMessage = "GossipSDK is missing one or more dependencies required for proper functionality.";

#if !SOCKET_IO_UNITY
            const string okButton = "Install SocketIOUnity";
#elif !UNITASK
            const string okButton = "Install UniTask";
#endif

            const string cancelButton = "Ignore";
            
            bool installPackage = EditorUtility.DisplayDialog(dialogTitle, dialogMessage, okButton, cancelButton);
            
            if (installPackage)
            {
#if !SOCKET_IO_UNITY
                Client.Add(Constants.SocketIOUnity);
#elif !UNITASK
                Client.Add(Constants.UniTask);
#endif
            }
            else
            {
                string dependencies = "Please install the required dependencies via the Package Manager in order to properly use GossipSDK:";
                
#if !SOCKET_IO_UNITY
                dependencies += $"\n\t- {Constants.SocketIOUnity}";
#endif
#if !UNITASK
                dependencies += $"\n\t- {Constants.UniTask}";
#endif
                
                Debug.LogWarning($"{dialogMessage}\n{dependencies}");
            }
        }
    }
}
#endif