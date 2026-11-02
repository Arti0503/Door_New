using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIOutline = UnityEngine.UI.Outline;

namespace CCEPL_CAD
{
    public class FaultTab : MonoBehaviour
    {
        public Button FaultBtn;
        public TMP_Text FaultName;
        public UIOutline Outline;
        public Color DefaultOutlineColor;
        public Color SelectedOutlineColor;
        internal int _faultIndex = -1;
        public bool IsFaultCompleted;

        /// <summary>
        /// Subscribes the button click event listener when the component is enabled.
        /// </summary>
        private void OnEnable()
        {
            FaultBtn.onClick.AddListener(OnFaultClick);
        }

        /// <summary>
        /// Removes all event listeners from the button when the component is disabled to prevent memory leaks.
        /// </summary>
        private void OnDisable()
        {
            FaultBtn.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// Initializes the tab with the specified fault index and updates the display text.
        /// </summary>
        public void Init(int faultIndex)
        {
            _faultIndex = faultIndex;
            FaultName.text = $"Fault {faultIndex}";
        }

        /// <summary>
        /// Handles the button click by triggering the global fault selection event with this tab's fault index.
        /// </summary>
        void OnFaultClick()
        {
#if !UNITY_EDITOR
            return;
#endif
            EventManager.OnFaultClick?.Invoke(_faultIndex);
        }

        /// <summary>
        /// Resets the tab's interactable state and restores its outline color to the default unselected style.
        /// </summary>
        internal void ResetGraphics()
        {
            FaultBtn.interactable = true;
            Outline.effectColor = DefaultOutlineColor;
        }

        /// <summary>
        /// Marks this tab as actively selected by disabling its button interaction and applying the selected outline color.
        /// </summary>
        internal void SelectTab()
        {
            FaultBtn.interactable = false;
            Outline.effectColor = SelectedOutlineColor;
        }
    }
}