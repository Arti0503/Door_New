#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CCEPL_CAD.Editor
{
    /// <summary>
    /// Ensures that the Windows Standalone scripting backend is set to Mono2x.
    /// This fixes the "Could not set up a toolchain for Architecture x64. Make sure you have the right build tools installed for il2cpp builds" error.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildSettingsConfigurator
    {
        static BuildSettingsConfigurator()
        {
            EditorApplication.delayCall += ApplyMonoBackend;
        }

        [MenuItem("Tools/Build/Set Standalone Scripting Backend to Mono")]
        public static void ApplyMonoBackend()
        {
            var namedTarget = NamedBuildTarget.Standalone;
            var currentBackend = PlayerSettings.GetScriptingBackend(namedTarget);

            if (currentBackend != ScriptingImplementation.Mono2x)
            {
                PlayerSettings.SetScriptingBackend(namedTarget, ScriptingImplementation.Mono2x);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=green>[BuildSettingsConfigurator]</color> Successfully switched Standalone Scripting Backend from {currentBackend} to Mono2x. Windows Standalone builds will now compile cleanly without requiring C++ IL2CPP build tools.");
            }
            else
            {
                Debug.Log("<color=green>[BuildSettingsConfigurator]</color> Standalone Scripting Backend is already set to Mono2x.");
            }
        }
    }
}
#endif
