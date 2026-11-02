using CCEPL_CAD.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIOutline = UnityEngine.UI.Outline;

namespace CCEPL_CAD
{
    public enum StepVisualState
    {
        NotStarted,
        InProgress,
        Completed,
        Skipped,
        Rejected
    }

    public class FaultStep : MonoBehaviour
    {
        public TMP_Text FaultText;
        public Image FaultStepBG;
        public UIOutline FaultStepBGOutline;
        public Color FaultStepCompColor;
        public Color FaultStepCompOutlineColor;
        public Color FaultStepRejectColor;
        public Color FaultStepRejectOutlineColor;
        public Color FaultStepSkipColor;
        public Color FaultStepSkipOutlineColor;
        public Color FaultStepStartColor;
        public Color DefaultColor;
        private string _componentCode;
        private ToolType _requireTool;
        internal Step Step;
        public StepVisualState CurrentVisualState { get; internal set; } = StepVisualState.NotStarted;

        /// <summary>
        /// Initializes the fault step UI with data from the specified step, configuring required tools and status text.
        /// </summary>
        internal void SetFault(Step step)
        {
            _componentCode = step.ComponentCode;
            _requireTool = step.RequireToolType;
            FaultText.text =
                $"{step.Message}{(RefManager.Instance.FaultManager.Modes == Modes.Easy ? $" ({ByteConverter.SplitWords(_requireTool.ToString())})" : string.Empty)}";
            FaultStepBG.color = DefaultColor;
            CurrentVisualState = StepVisualState.NotStarted;
            Step = step;
        }

        /// <summary>
        /// Updates the step text to reflect whether tool name hints should be shown based on the active mode (Easy vs Normal).
        /// </summary>
        internal void UpdateModeText()
        {
            if (Step == null) return;
            FaultText.text =
                $"{Step.Message}{(RefManager.Instance.FaultManager.Modes == Modes.Easy ? $" ({ByteConverter.SplitWords(_requireTool.ToString())})" : string.Empty)}";
        }

        /// <summary>
        /// Updates visuals and triggers animations or target highlights when this step becomes active.
        /// </summary>
        internal void OnStepStart()
        {
            CurrentVisualState = StepVisualState.InProgress;
            FaultStepBG.color = FaultStepStartColor;
            if (FaultStepBGOutline != null) FaultStepBGOutline.effectColor = Color.clear;
            if (FaultManager.IsVisualInspectionStep(Step)) RefManager.Instance.FaultManager.PlayStepAnimation(Step);
            HighLightAllTargets();
        }

        /// <summary>
        /// Updates the step's background and outline visuals to indicate successful completion.
        /// </summary>
        internal void OnStepComplete()
        {
            CurrentVisualState = StepVisualState.Completed;
            ApplyStateVisuals(FaultStepCompColor, FaultStepCompOutlineColor);
        }

        /// <summary>
        /// Updates the step visuals to indicate rejection or failure and resets any target highlights.
        /// </summary>
        internal void OnStepReject()
        {
            CurrentVisualState = StepVisualState.Rejected;
            ApplyStateVisuals(FaultStepRejectColor, FaultStepRejectOutlineColor);
            ResetAllTargets();
        }

        /// <summary>
        /// Updates the step visuals to indicate the step was skipped and resets any target highlights.
        /// </summary>
        internal void OnStepSkip()
        {
            CurrentVisualState = StepVisualState.Skipped;
            ApplyStateVisuals(FaultStepSkipColor, FaultStepSkipOutlineColor);
            ResetAllTargets();
        }
        
        internal void OnStepReset()
        {
            CurrentVisualState = StepVisualState.NotStarted;
            ApplyStateVisuals(DefaultColor, Color.clear);
            ResetAllTargets();
        }

        /// <summary>
        /// Restores visual styling corresponding to a previously saved step state without triggering step execution events.
        /// </summary>
        internal void RestoreVisualState(StepVisualState state)
        {
            CurrentVisualState = state;
            switch (state)
            {
                case StepVisualState.Completed:
                    ApplyStateVisuals(FaultStepCompColor, FaultStepCompOutlineColor);
                    ResetAllTargets();
                    break;
                case StepVisualState.Skipped:
                    ApplyStateVisuals(FaultStepSkipColor, FaultStepSkipOutlineColor);
                    ResetAllTargets();
                    break;
                case StepVisualState.Rejected:
                    ApplyStateVisuals(FaultStepRejectColor, FaultStepRejectOutlineColor);
                    ResetAllTargets();
                    break;
                case StepVisualState.InProgress:
                    // ApplyStateVisuals(FaultStepStartColor, Color.clear);
                    // break;
                case StepVisualState.NotStarted:
                default:
                    ApplyStateVisuals(DefaultColor, Color.clear);
                    break;
            }
        }

        /// <summary>
        /// Determines whether the provided tool type matches the tool required by this step.
        /// </summary>
        internal bool IsToolMatch(ToolType tool, string code)
        {
            return _requireTool == tool;
        }

        /// <summary>
        /// Checks if this step can be skipped, which is permitted for steps that are not visual inspections.
        /// </summary>
        internal bool IsSkippable()
        {
            return !FaultManager.IsVisualInspectionStep(Step);
        }

        #region Helper Methods

        /// <summary>
        /// Applies the specified background color and outline color to the step UI elements.
        /// </summary>
        private void ApplyStateVisuals(Color bgColor, Color outlineColor)
        {
            if (FaultStepBG != null)
            {
                FaultStepBG.color = bgColor;
            }

            if (FaultStepBGOutline != null)
            {
                // Direct assignment avoids allocating a zero-second DOTween instance
                FaultStepBGOutline.effectColor = outlineColor;
            }
        }

        /// <summary>
        /// Resets the interaction state and visual highlights on all target objects associated with this step.
        /// </summary>
        internal void ResetAllTargets()
        {
            if (Step?.Targets == null) return;
            
            foreach (var target in Step.Targets)
            {
                if (target) target.OnObjectReset();
            }
        }

        /// <summary>
        /// Highlights all target objects associated with this step by setting their selection state.
        /// </summary>
        internal void HighLightAllTargets()
        {
            if (Step?.Targets == null) return;
            
            foreach (var target in Step.Targets)
            {
                if (target) target.OnObjectDeselected();
            }
        }

        #endregion
    }
}
