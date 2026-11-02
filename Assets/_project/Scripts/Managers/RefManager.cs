using UnityEngine;

namespace CCEPL_CAD
{
    public class RefManager : MonoBehaviour
    {
        public static RefManager Instance;

        [Header("Manager References")]
        public FaultManager FaultManager;
        public UIManager UIManager;
        public UDPManager UDPManager;

        [Space(5), Header("UI References")] public UIReference UIReference;


        [Space(5), Header("Scriptable Objects References")]
        public ToolsDataSO ToolsDataSo;

        [Space(5), Header("Tool Objects References")]
        public ToolObject ToolObject;

        /// <summary>
        /// Implements singleton pattern for RefManager, persisting the instance across scene loads and destroying duplicates.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}