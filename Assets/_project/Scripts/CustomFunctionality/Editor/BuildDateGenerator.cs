using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CCEPL_CAD
{
    public class BuildDateGenerator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private const string OutputPath = "Assets/StreamingAssets/BuildInfo.json";

        /// <summary>
        /// Unity build pre-process callback that generates and writes the build timestamp to BuildInfo.json in StreamingAssets.
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            string directory = Path.GetDirectoryName(OutputPath);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var json = JsonUtility.ToJson(new BuildInfoData
            {
                buildDate = DateTime.UtcNow.ToString("O") // ISO 8601
            }, true);

            File.WriteAllText(OutputPath, json);

            AssetDatabase.Refresh();

            Debug.Log($"BuildInfo.json generated: {json}");
        }

        [Serializable]
        private class BuildInfoData
        {
            public string buildDate;
        }
    }
}