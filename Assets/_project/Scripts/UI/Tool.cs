using System;
using System.Collections;
using CCEPL_CAD.Models;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIOutline = UnityEngine.UI.Outline;

namespace CCEPL_CAD
{
    public class Tool : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Image _toolIcon;
        [SerializeField] private UIOutline _outline;
        [SerializeField] private TMP_Text _toolName;
        public Color SelectedColor;
        public Color UnselectedColor;
        [SerializeField] private float PulseDuration = 1f;

        [Header("Hold Settings")]
        [Tooltip("Duration in seconds to hold before spawning the 3D tool object.")]
        [SerializeField] private float _holdDuration = 2.5f;

        private Coroutine _pulseCoroutine;
        private Coroutine _holdCoroutine;
        private bool _isHeldTriggered;

        internal ToolType _toolType;
        ToolObject _toolObject;

        /// <summary>
        /// Populates the tool UI elements with data including icon, formatted name, tool type, and associated 3D model reference.
        /// </summary>
        internal void SetTool(ToolData data)
        {
            if (data.ToolIcon != null) _toolIcon.sprite = data.ToolIcon;
            _toolName.text = ByteConverter.SplitWords(data.ToolName);
            _toolType = data.ToolType;
            _toolObject = data.ToolObject;
        }

        /// <summary>
        /// Handles pointer down events by starting the hold timer routine for spawning the 3D tool model.
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            _isHeldTriggered = false;
            CancelHold();
            _holdCoroutine = StartCoroutine(HoldTimerRoutine());
        }

        /// <summary>
        /// Handles pointer release events by cancelling any ongoing hold timer.
        /// </summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            CancelHold();
        }

        /// <summary>
        /// Handles pointer exit events by cancelling any ongoing hold timer when the pointer moves off the element.
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            CancelHold();
        }

        /// <summary>
        /// Coroutine that waits for the configured hold duration before triggering the 3D tool object spawn event.
        /// </summary>
        private IEnumerator HoldTimerRoutine()
        {
            yield return new WaitForSeconds(_holdDuration);

            _isHeldTriggered = true;
            _holdCoroutine = null;

            // Trigger the 3D spawn event
            EventManager.Spawn3DToolObject?.Invoke(_toolObject);
        }

        /// <summary>
        /// Stops the active hold timer coroutine and clears its reference.
        /// </summary>
        private void CancelHold()
        {
            if (_holdCoroutine != null)
            {
                StopCoroutine(_holdCoroutine);
                _holdCoroutine = null;
            }
        }

        /// <summary>
        /// Handles click events on the tool UI, validating prerequisites, triggering tool selection, and updating visual selection state.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {

            if (DirectModelRotatorAndScaler.IsLocked)
            {
                EventManager.OnLogMessage?.Invoke($"Please select after current step is completed.", true);
                return;
            }
            // Ignore normal click action if it was triggered by a long-press hold
            if (_isHeldTriggered)
            {
                _isHeldTriggered = false;
                return;
            }

            if (!RefManager.Instance.FaultManager.IsFaultStart)
            {
                EventManager.OnLogMessage?.Invoke($"Please start the fault before selecting a tool.", true);
                return;
            }
            EventManager.OnToolSelected?.Invoke(_toolType, _toolObject);
            
            // Stop pulsing and set to selected color on click
            StopPulse();
            _outline.effectColor = SelectedColor;
        }

        /// <summary>
        /// Resets the outline color to the unselected state and optionally stops outline pulsing.
        /// </summary>
        internal void ResetSelection(bool state = false)
        {
            _outline.effectColor = UnselectedColor;
            if (state) StopPulse();
        }

        /// <summary>
        /// Initiates outline pulsing between unselected and selected colors when playing in Easy mode to guide the user.
        /// </summary>
        internal void HighLightTool()
        {
            if (RefManager.Instance.FaultManager.Modes != Modes.Easy) return;

            // Start continuous pulsing between UnselectedColor and SelectedColor until clicked
            if (_pulseCoroutine != null) return;
            _pulseCoroutine = StartCoroutine(PulseOutline());
        }

        /// <summary>
        /// Coroutine that continuously lerps the outline color between unselected and selected states over time.
        /// </summary>
        private IEnumerator PulseOutline()
        {
            if (PulseDuration <= 0.0001f) PulseDuration = 1f;
            float t = 0f;
            bool forward = true;
            while (true)
            {
                float delta = Time.deltaTime / PulseDuration;
                t += forward ? delta : -delta;
                if (t >= 1f)
                {
                    t = 1f;
                    forward = false;
                }
                else if (t <= 0f)
                {
                    t = 0f;
                    forward = true;
                }

                _outline.effectColor = Color.Lerp(UnselectedColor, SelectedColor, t);
                yield return null;
            }
        }

        /// <summary>
        /// Stops the active outline pulse coroutine and clears its reference.
        /// </summary>
        private void StopPulse()
        {
            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }
        }

        /// <summary>
        /// Cleans up coroutines and hold state when the component is disabled.
        /// </summary>
        private void OnDisable()
        {
            StopPulse();
            CancelHold();
            _isHeldTriggered = false;
        }
    }
}