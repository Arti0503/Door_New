using System.Linq;
using CCEPL_CAD.Models;
using UnityEngine;

namespace CCEPL_CAD
{
    public class ToolObject : MonoBehaviour
    {
        public Vector3 Offset = Vector3.zero;
        internal bool IsEngage = false;
        ToolType _tool;
        private Camera _mainCamera;

        /// <summary>
        /// Initializes the tool with the specified tool type, caches the main camera, and positions the tool offscreen until moved.
        /// </summary>
        internal void Init(ToolType tool)
        {
            _tool = tool;
            _mainCamera = Camera.main;
            transform.position = Vector3.one * 100000;
        }

        /// <summary>
        /// Updates the tool orientation each frame so it continuously faces the main camera.
        /// </summary>
        private void Update()
        {
            FaceCamera();
        }

        /// <summary>
        /// Orients the tool transform to look at the main camera position.
        /// </summary>
        private void FaceCamera()
        {
            return;
            if (_mainCamera == null) return;
            
            transform.LookAt(_mainCamera.transform.position);
        }

        /// <summary>
        /// Unity physics trigger callback that detects collision with interactable component parts and triggers the tool interaction.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            //if(!_coolDown) return;
            var componentPart = other.GetComponent<IntrectableComponentPart>();
            if (componentPart == null) return;
            OnTrigger(componentPart);
        }

        /// <summary>
        /// Validates tool compatibility with the targeted component part, verifies step progress, and plays the step animation upon completion.
        /// </summary>
        internal void OnTrigger(IntrectableComponentPart componentPart)
        {
            // 1. Guard clauses for null checks
            if (!componentPart) return;
    
            var step = RefManager.Instance.UIManager.GetCurrentFaultStep();
            if (!step || !step.Step.Targets.Contains(componentPart)) return;

            var toolMatch = step.IsToolMatch(_tool, componentPart.InteractableComponentData.ComponentCode);

            // 2. Handle failure/wrong tool condition first and exit early
            if (IsEngage || !toolMatch)
            {
                IsEngage = false;
                EventManager.OnWrongToolSelected?.Invoke();
                Destroy(gameObject);
                return;
            }

            // 3. Mark the current component as triggered if it is part of the required targets
            componentPart.OnObjectSelected();

            // 4. Check if ALL required targets for this step are now triggered
            var areAllPartsTriggered = step.Step.Targets.All(target => target.IsTriggered);

            // 5. Only complete the step and trigger animation if everything is triggered
            if (!areAllPartsTriggered) return;
            IsEngage = true;
            
            // Trigger Animation here
            RefManager.Instance.FaultManager.PlayStepAnimation(step.Step);
            
            //Reset all trigger
            step.Step.Targets.ForEach(target => target.OnObjectReset());
            Destroy(gameObject);
        }

    }
}