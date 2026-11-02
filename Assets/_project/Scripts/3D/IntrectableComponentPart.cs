using System;
using CCEPL_CAD.Models;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CCEPL_CAD
{
    [RequireComponent(typeof(Outline))]
    public class IntrectableComponentPart : MonoBehaviour, IPointerDownHandler
    {
        public InteractableComponentData InteractableComponentData;
        internal bool IsTriggered = false;
        private Color _outlineColor = new Color(0, 0, 0, 0f);
        private Outline _outline;
        private Collider _collider;

        /// <summary>
        /// Validates and caches component references for outline and collider in the Unity Editor.
        /// </summary>
        private void OnValidate()
        {
            _outline = GetComponent<Outline>();
            _collider = GetComponent<Collider>();
        }

        /// <summary>
        /// Initializes the outline settings and ensures collider references are cached and disabled by default.
        /// </summary>
        private void Start()
        {
            if (_outline == null) _outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            _outline.OutlineMode = Outline.Mode.OutlineAll;
            _outline.OutlineWidth = 3;
            _outline.OutlineColor = _outlineColor;
            _outline.enabled = false;
            if (_collider == null) _collider = GetComponent<Collider>();
            _collider.enabled = false;
        }

        /// <summary>
        /// Determines whether the provided component code matches this interactable component part's code.
        /// </summary>
        public bool IsMatch(string code)
        {
            return InteractableComponentData != null && code != string.Empty && code == InteractableComponentData.ComponentCode;
        }
        
        /// <summary>
        /// Handles pointer click or tap events to trigger the active tool interaction on this component part.
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!HandleToolOperation.Instance._tool || IsTriggered) return;
            HandleToolOperation.Instance._tool.OnTrigger(this);
        }
        
        /// <summary>
        /// Marks the component part as triggered/selected, sets the outline to yellow in Easy mode, and disables the collider.
        /// </summary>
        public void OnObjectSelected()
        {
            IsTriggered = true;
            _collider.enabled = false;
            _outline.OutlineColor = Color.yellow;
            _outline.enabled = RefManager.Instance.FaultManager.Modes == Modes.Easy;
        }
        
        /// <summary>
        /// Updates the outline to green for Easy mode and re-enables the collider for subsequent interactions.
        /// </summary>
        public void OnObjectDeselected()
        {
            _collider.enabled = true;
            _outline.OutlineColor = Color.green;
            _outline.enabled = RefManager.Instance.FaultManager.Modes == Modes.Easy;
        }
        
        /// <summary>
        /// Resets the component part to its default unselected state, disabling the outline and collider.
        /// </summary>
        public void OnObjectReset()
        {
            IsTriggered = false;
            _collider.enabled = false;
            _outline.OutlineColor = _outlineColor;
            _outline.enabled = false;
        }
    }
}