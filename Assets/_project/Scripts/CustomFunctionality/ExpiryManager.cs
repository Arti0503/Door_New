using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace CCEPL_CAD
{

    public class ExpiryManager : MonoBehaviour
    {
        [SerializeField] private int expiryDays = 21;
        [SerializeField] private GameObject expiryPopup;
        public Button QuitButton;

        [Serializable]
        private class BuildInfoData
        {
            public string buildDate;
        }

        /// <summary>
        /// Subscribes the quit button click listener when this component is enabled.
        /// </summary>
        private void OnEnable()
        {
            QuitButton.onClick.AddListener(QuitApplication);
        }

        /// <summary>
        /// Unsubscribes listeners from the quit button when this component is disabled.
        /// </summary>
        private void OnDisable()
        {
            QuitButton.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// Reads build metadata at startup, checks if the build has exceeded its allowed active period, and pauses execution with an expiry dialog if expired.
        /// </summary>
        private void Start()
        {
            #if UNITY_EDITOR
            return;
            #endif
            
            string path = Path.Combine(Application.streamingAssetsPath, "BuildInfo.json");

            if (!File.Exists(path))
            {
                Debug.LogError("BuildInfo.json not found!");
                return;
            }

            string json = File.ReadAllText(path);
            BuildInfoData info = JsonUtility.FromJson<BuildInfoData>(json);

            DateTime buildDate = DateTime.Parse(
                info.buildDate,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind);

            if (DateTime.UtcNow >= buildDate.AddDays(expiryDays))
            {
                if (expiryPopup != null)
                    expiryPopup.SetActive(true);

                Time.timeScale = 0;
            }
        }

        /// <summary>
        /// Handles the quit button click event to exit the application.
        /// </summary>
        private void QuitApplication()
        {
            Application.Quit();
        }
    }
}