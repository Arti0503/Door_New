using System;
using System.Collections;
using System.Collections.Generic;
using CCEPL_CAD.Models;
using DG.Tweening;
using Newtonsoft.Json;
using RuntimeSceneGizmo;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CCEPL_CAD
{
    public class FaultManager : MonoBehaviour
    {
        public Modes Modes = Modes.Easy;
        public Faults faults;
        internal bool IsFaultStart = false;
        public bool playWithIdle = false;
        
        // Playables Graph for standalone animation handling (Forward & Reverse)
        private PlayableGraph customGraph;
        private AnimationMixerPlayable customMixer;
        private bool isCustomGraphCreated = false;
        private Coroutine animationCoroutine;
        private Animator targetAnimator;

        /// <summary>
        /// Sets the application target frame rate to 120 frames per second on component initialization.
        /// </summary>
        private void Awake()
        {
            Application.targetFrameRate = 120;
        }

        /// <summary>
        /// Subscribes to fault and animation lifecycle events, and schedules initial fault detection notification after a delay.
        /// </summary>
        private void OnEnable()
        {
            EventManager.OnFaultsReceived += FaultsReceived;
            EventManager.OnFaultCompleted += CleanupCustomGraph;
            EventManager.ResetAnimation += CleanupCustomGraph;
            //DOVirtual.DelayedCall(3f, () => { EventManager.OnFaultDetect?.Invoke(faults); });
        }

        /// <summary>
        /// Unsubscribes from fault and animation events and cleans up custom playable animation graph resources.
        /// </summary>
        private void OnDisable()
        {
            EventManager.OnFaultsReceived -= FaultsReceived;
            EventManager.OnFaultCompleted -= CleanupCustomGraph;
            EventManager.ResetAnimation -= CleanupCustomGraph;
            CleanupCustomGraph();
        }

        /// <summary>
        /// Invokes the application start event to initiate application workflows.
        /// </summary>
        private void Start()
        {
            EventManager.StartApplication?.Invoke();
        }

        /// <summary>
        /// Handles incoming fault data string from external events and logs it.
        /// </summary>
        void FaultsReceived(string data)
        {
            Debug.Log($"[FaultManager] \n{data}");
        }

        [Header("Visual Inspection Settings")]
        [SerializeField] private float visualInspectionDuration = 5.5f;

        /// <summary>
        /// Determines whether a given step requires a visual inspection tool or check.
        /// </summary>
        public static bool IsVisualInspectionStep(Step step)
        {
            if (step == null) return false;
            ToolType t = step.RequireToolType;
            return t == ToolType.Visual ||
                   t == ToolType.VisualCheck ||
                   t == ToolType.VisualCheckInThroghtSightGlass ||
                   t == ToolType.VisualInspection ||
                   t == ToolType.VisualInspectionLamp ||
                   t == ToolType.None;
        }

        #region Fault steps

        /// <summary>
        /// Starts the animation routine for a specific step and triggers associated display messages.
        /// </summary>
        internal void PlayStepAnimation(Step step, bool state = true, bool isReverse = false)
        {
            ResetStepHighlights();
            if (animationCoroutine != null) StopCoroutine(animationCoroutine);
            animationCoroutine = StartCoroutine(ExecuteAnimationRoutine(step, state, isReverse));
            if(step.IsDisplayeText) EventManager.TriggerDisplayText(step.DisaplayMessage);
        }

        /// <summary>
        /// Resets visual highlights applied to interactable objects during step execution.
        /// </summary>
        private void ResetStepHighlights()
        {
        }

        /// <summary>
        /// Coroutine that locks camera movement, positions camera if configured, plays the step animation or delays for inspection, and triggers step completion.
        /// </summary>
        private IEnumerator ExecuteAnimationRoutine(Step step, bool state = true, bool isReverse = false)
        {
            // Lock camera movement during visual inspection / step execution
            EventManager.OnCameraLockStateChanged?.Invoke(true);
            CameraMovement.OnCameraLockStateChanged?.Invoke(true);

            // Move camera to specific position and rotation if configured for this step
            if (step != null && step.UseCustomCameraTransform)
            {
                EventManager.MoveCameraToTransform?.Invoke(step.TargetCameraPosition, step.TargetCameraRotation, 1.0f);
            }

            float duration = 1.0f;
            bool isVisualStep = IsVisualInspectionStep(step);

            if (step != null && step.AnimationClip != null)
            {
                float clipLength = step.AnimationClip.length;
                float absSpeed = Mathf.Max(0.001f, Mathf.Abs(step.AnimationSpeed));
                duration = clipLength / absSpeed;

                if (isVisualStep && !isReverse)
                {
                    yield return new WaitForSeconds(3);
                    duration = Mathf.Max(duration, visualInspectionDuration);
                }

                var playSpeed = isReverse ? step.AnimationSpeed * -1 : step.AnimationSpeed;
                Debug.Log(
                    $"[FaultManager] Playing clip '{step.AnimationClip.name}' | Reverse: {isReverse} | Speed: {playSpeed} | Duration: {duration:F2}s");
                PlayClipWithPlayable(step.AnimationClip, playSpeed);
            }
            else
            {
                duration = isVisualStep ? visualInspectionDuration : 2f;
                Debug.Log($"[FaultManager] Step '{(step != null ? step.Message : "null")}' | Visual: {isVisualStep} | Duration: {duration:F2}s");
            }

            yield return new WaitForSeconds(duration);

            // Unlock camera movement
            EventManager.OnCameraLockStateChanged?.Invoke(false);
            CameraMovement.OnCameraLockStateChanged?.Invoke(false);

            if (state) EventManager.OnStepComplete?.Invoke();
        }

        /// <summary>
        /// Finalizes the current fault repair procedure, resets highlights, and invokes completion events.
        /// </summary>
        private void CompleteProcedure()
        {
            ResetStepHighlights();
            EventManager.OnFaultCompleted?.Invoke();
        }

        /// <summary>
        /// Coroutine that evaluates an animation clip either forward or in reverse over a specified duration using the Playables API.
        /// </summary>
        private void PlayClipWithPlayable(AnimationClip clip, float speed)
        {
            targetAnimator = CustomizedUIBase.Instance?.Animator;

            if (!targetAnimator) return;
            
            EnsureCustomGraphCreated();

            var clipPlayable = AnimationClipPlayable.Create(customGraph, clip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetApplyPlayableIK(false);

            if (customMixer.GetInputCount() == 0)
            {
                customGraph.Connect(clipPlayable, 0, customMixer, 0);
            }
            else
            {
                customGraph.Disconnect(customMixer, 0);
                customGraph.Connect(clipPlayable, 0, customMixer, 0);
            }

            customMixer.SetInputWeight(0, 1f);

            if (speed < 0f)
            {
                clipPlayable.SetTime(clip.length);
                clipPlayable.SetSpeed(speed);
            }
            else
            {
                clipPlayable.SetTime(0);
                clipPlayable.SetSpeed(speed);
            }

            customGraph.Play();
        }

        private void EnsureCustomGraphCreated()
        {
            if (isCustomGraphCreated && customGraph.IsValid()) return;

            customGraph = PlayableGraph.Create("RepairWorkflowGraph");
            customGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            customMixer = AnimationMixerPlayable.Create(customGraph, 1);

            if (targetAnimator != null)
            {
                var output = AnimationPlayableOutput.Create(customGraph, "AnimationOutput", targetAnimator);
                output.SetSourcePlayable(customMixer);
            }

            isCustomGraphCreated = true;
        }

        /// <summary>
        /// Stops running animation coroutines and destroys the custom PlayableGraph and playable nodes.
        /// </summary>
        public void CleanupCustomGraph()
        {

            if (isCustomGraphCreated && customGraph.IsValid())
            {
                customGraph.Destroy();
                isCustomGraphCreated = false;
            }
        }

        /// <summary>
        /// Cancels any currently executing step animation coroutine, cleans up the playable graph, and unlocks camera controls.
        /// </summary>
        public void StopActiveStepAnimation()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }
            CleanupCustomGraph();
            EventManager.OnCameraLockStateChanged?.Invoke(false);
            CameraMovement.OnCameraLockStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Restores cumulative 3D model states by sequentially sampling animation clips from step 0 up to targetStepIndex.
        /// </summary>
        public void RestoreCumulativeAnimation(List<Step> steps, int targetStepIndex)
        {
            targetAnimator = CustomizedUIBase.Instance?.Animator;
            if (targetAnimator == null || steps == null) return;

            StopActiveStepAnimation();

            int limit = Mathf.Min(targetStepIndex, steps.Count);
            for (int i = 0; i < limit; i++)
            {
                var step = steps[i];
                if (step != null && step.AnimationClip != null)
                {
                    float sampleTime = step.AnimationSpeed < 0f ? 0f : step.AnimationClip.length;
                    step.AnimationClip.SampleAnimation(targetAnimator.gameObject, sampleTime);
                }
            }
        }

        #endregion

        #region Step Copy & Append Utilities

        /// <summary>
        /// Copies all steps from source Fault and appends them to target Fault's existing Steps list.
        /// </summary>
        public int AppendStepsFromFault(int sourceFaultIndex, int targetFaultIndex)
        {
            if (faults == null || faults.FaultList == null) return 0;
            if (sourceFaultIndex < 0 || sourceFaultIndex >= faults.FaultList.Count) return 0;
            if (targetFaultIndex < 0 || targetFaultIndex >= faults.FaultList.Count) return 0;

            return AppendStepsFromFault(faults.FaultList[sourceFaultIndex], faults.FaultList[targetFaultIndex]);
        }

        /// <summary>
        /// Copies all steps from source Fault and appends them to target Fault's existing Steps list.
        /// </summary>
        public int AppendStepsFromFault(Fault sourceFault, Fault targetFault)
        {
            if (sourceFault == null || targetFault == null) return 0;
            if (sourceFault.Steps == null || sourceFault.Steps.Count == 0) return 0;

            if (targetFault.Steps == null)
            {
                targetFault.Steps = new List<Step>();
            }

            int count = 0;
            foreach (var step in sourceFault.Steps)
            {
                if (step != null)
                {
                    targetFault.Steps.Add(step.Clone());
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Appends a collection of steps (cloned) to a target Fault's existing Steps list.
        /// </summary>
        public int AppendSteps(Fault targetFault, IEnumerable<Step> stepsToAppend)
        {
            if (targetFault == null || stepsToAppend == null) return 0;
            if (targetFault.Steps == null)
            {
                targetFault.Steps = new List<Step>();
            }

            int count = 0;
            foreach (var step in stepsToAppend)
            {
                if (step != null)
                {
                    targetFault.Steps.Add(step.Clone());
                    count++;
                }
            }

            return count;
        }

        #endregion
    }
}
