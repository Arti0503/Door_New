using System.Collections;
using System.Collections.Generic;
using CCEPL_CAD.Models;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CCEPL_CAD
{
    public class DoorCustomizedUI : CustomizedUIBase
    {
        [Header("Animator")]
        public Animator PassengerDoorAnimator;
        public Animator EmergencyDoorAnimator;
        [Header("Door Sub-Models")]
        public GameObject PassengerDoorModel;
        public Toggle PassengerDoorToggle;
        public TMP_Text PassengerDoorToggleText;
        public GameObject EmergencyDoorModel;
        public Toggle EmergencyDoorToggle;
        public TMP_Text EmergencyDoorToggleText;
        public List<int> PassengerDoorFaultIndexes;
        public List<int> EmergencyDoorFaultIndexes;
        
        [Space(10), Header("Primary Door Buttons")] 
        public Button openLeftDoorBtn;
        public Button closeLeftDoorBtn;
        public Button openRightDoorBtn;
        public Button closeRightDoorBtn;
        public Button openBothDoorsBtn;
        public Button closeBothDoorsBtn;

        [Space(10), Header("Prototype Clip Buttons")]
        public Button obstacleReopenBtn;
        public Button obstacleRetryBtn;
        public Button persistentObstacleBtn;
        public Button obstacleDuringOpeningBtn;
        public Button antiDragActionBtn;

        [Space(10), Header("Timing & Obstacle Sliders")]
        public Slider timingSlider;
        public TMP_Text timingText;
        public Button obstacleBtn;
        public Slider obstacleRetrySlider;
        public TMP_Text obstacleRetryText;

        [Space(10), Header("Motion Values")] 
        [Range(1f, 10f)] public float doorMotionDurationSeconds = 3.0f;

        public float baseClipDurationSeconds = 3.0f;
        [Range(1, 5)] public int maxObstacleRetries = 3;
        
        [Space(10), Header("Emergency Door")]
        public int EmergencyDoorStartFaultIndex = 128;
        
        private bool isLeftDoorOpen = false;
        private bool isRightDoorOpen = false;
        private Coroutine obstacleRoutine;
        private Vector3 _hiddenPosition = Vector3.one * 10000f;

        /// <summary>
        /// Resolves door animators in hierarchy and initializes base class state.
        /// </summary>
        protected override void Awake()
        {
            ResolveAnimators();
            base.Awake();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-time validation callback ensuring animators are properly linked when inspector properties change.
        /// </summary>
        private void OnValidate()
        {
            ResolveAnimators();
        }
#endif

        /// <summary>
        /// Locates and caches child animators for passenger and emergency doors if not already assigned.
        /// </summary>
        public void ResolveAnimators()
        {
            // if (EmergencyDoorAnimator == null)
            // {
            //     var animators = GetComponentsInChildren<Animator>(true);
            //     foreach (var anim in animators)
            //     {
            //         if (anim.gameObject.name.Contains("Emergency"))
            //         {
            //             EmergencyDoorAnimator = anim;
            //             break;
            //         }
            //     }
            // }
            //
            // if (PassengerDoorAnimator == null)
            // {
            //     var animators = GetComponentsInChildren<Animator>(true);
            //     foreach (var anim in animators)
            //     {
            //         if (anim != EmergencyDoorAnimator &&
            //             (anim.gameObject.name.Contains("Door") || anim.gameObject.name.Contains("Passenger") || anim.gameObject.name.Contains("Pessanger")))
            //         {
            //             PassengerDoorAnimator = anim;
            //             break;
            //         }
            //     }
            // }

            if (Animator == null)
            {
                Animator = PassengerDoorAnimator;
            }
        }

        /// <summary>
        /// Binds UI events, updates slider readouts, and initializes door animations.
        /// </summary>
        protected override void Start()
        {
            base.Start();
            SetupUIListeners();
            UpdateTimingUI(doorMotionDurationSeconds);
            UpdateObstacleRetryUI(maxObstacleRetries);
            
            RegisterModelToggle(PassengerDoorToggle, PassengerDoorToggleText, PassengerDoorModel, PassengerDoorAnimator, PassengerDoorFaultIndexes);
            RegisterModelToggle(EmergencyDoorToggle, EmergencyDoorToggleText, EmergencyDoorModel, EmergencyDoorAnimator, EmergencyDoorFaultIndexes);

            if (PassengerDoorToggle) PassengerDoorToggle.isOn = true;
            ResetAnimation();
        }

        /// <summary>
        /// Registers event handlers for door buttons, prototype clips, and configuration sliders.
        /// </summary>
        private void SetupUIListeners()
        {
            if (openLeftDoorBtn != null) openLeftDoorBtn.onClick.AddListener(OpenLeftDoor);
            if (closeLeftDoorBtn != null) closeLeftDoorBtn.onClick.AddListener(CloseLeftDoor);
            if (openRightDoorBtn != null) openRightDoorBtn.onClick.AddListener(OpenRightDoor);
            if (closeRightDoorBtn != null) closeRightDoorBtn.onClick.AddListener(CloseRightDoor);
            if (openBothDoorsBtn != null) openBothDoorsBtn.onClick.AddListener(OpenBothDoors);
            if (closeBothDoorsBtn != null) closeBothDoorsBtn.onClick.AddListener(CloseBothDoors);

            if (obstacleReopenBtn != null) obstacleReopenBtn.onClick.AddListener(PlayObstacleReopen);
            if (obstacleRetryBtn != null) obstacleRetryBtn.onClick.AddListener(PlayObstacleRetry);
            if (persistentObstacleBtn != null) persistentObstacleBtn.onClick.AddListener(PlayPersistentObstacle);
            if (obstacleDuringOpeningBtn != null)
                obstacleDuringOpeningBtn.onClick.AddListener(PlayObstacleDuringOpening);
            if (antiDragActionBtn != null) antiDragActionBtn.onClick.AddListener(PlayAntiDragAction);
            if (obstacleBtn != null) obstacleBtn.onClick.AddListener(TriggerObstacleSequence);

            if (timingSlider != null)
            {
                timingSlider.minValue = 1.0f;
                timingSlider.maxValue = 10.0f;
                timingSlider.value = doorMotionDurationSeconds;
                timingSlider.onValueChanged.AddListener(val =>
                {
                    doorMotionDurationSeconds = val;
                    UpdateTimingUI(val);
                    SetAnimatorSpeed();
                });
            }

            if (obstacleRetrySlider != null)
            {
                obstacleRetrySlider.minValue = 1f;
                obstacleRetrySlider.maxValue = 5f;
                obstacleRetrySlider.wholeNumbers = true;
                obstacleRetrySlider.value = maxObstacleRetries;
                obstacleRetrySlider.onValueChanged.AddListener(val =>
                {
                    maxObstacleRetries = Mathf.RoundToInt(val);
                    UpdateObstacleRetryUI(maxObstacleRetries);
                });
            }
        }

        /// <summary>
        /// Switches between passenger and emergency door models based on the fault index and begins the fault sequence.
        /// </summary>
        public override void TriggerFault(int faultIndex)
        {
            if(RefManager.Instance.FaultManager.IsFaultStart) return;
            // ResolveAnimators();
            // var isEmergency = faultIndex >= EmergencyDoorStartFaultIndex;
            // Animator = isEmergency ? EmergencyDoorAnimator : PassengerDoorAnimator;

            base.TriggerFault(faultIndex);

            /*if (PassengerDoorAnimator != null)
            {
                PassengerDoorAnimator.gameObject.transform.localPosition = isEmergency ? _hiddenPosition : Vector3.zero;
            }

            if (EmergencyDoorAnimator != null && EmergencyDoorAnimator.transform.parent != null)
            {
                EmergencyDoorAnimator.transform.parent.transform.localPosition = !isEmergency ? _hiddenPosition : Vector3.zero;
            }*/
        }

        /// <summary>
        /// Stops active routines, restores passenger door visibility, resets layer weights, and triggers Idle animation.
        /// </summary>
        public override void ResetAnimation()
        {
            StopObstacleRoutine();
            ResolveAnimators();
            isLeftDoorOpen = false;
            isRightDoorOpen = false;

            // if (PassengerDoorAnimator != null)
            // {
            //     PassengerDoorAnimator.gameObject.transform.localPosition = Vector3.zero;
            // }
            //
            // if (EmergencyDoorAnimator != null && EmergencyDoorAnimator.transform.parent != null)
            // {
            //     EmergencyDoorAnimator.transform.parent.transform.localPosition = _hiddenPosition;
            // }
            
            Animator = PassengerDoorToggle.isOn ? PassengerDoorAnimator : EmergencyDoorAnimator;

            if (Animator != null)
            {
                for (var i = 0; i < Animator.layerCount; i++) Animator.SetLayerWeight(i, 1.0f);
                Animator.Play("Idle", 0, 0f);
            }
            SetAnimatorSpeed();
        }

        /// <summary>
        /// Sets layer weights on the active door animator depending on whether the secondary right layer should be active.
        /// </summary>
        private void SetLayerWeights(bool enableRightLayer)
        {
            if (Animator == null) return;
            if (Animator.layerCount > 0) Animator.SetLayerWeight(0, 1.0f);
            if (Animator.layerCount > 1) Animator.SetLayerWeight(1, enableRightLayer ? 1.0f : 0.0f);
        }

        /// <summary>
        /// Calculates and applies the speed scaling factor on the animator based on target duration.
        /// </summary>
        private void SetAnimatorSpeed()
        {
            if (Animator == null) return;
            var speedMultiplier = baseClipDurationSeconds / Mathf.Max(0.1f, doorMotionDurationSeconds);
            Animator.SetFloat("MotionSpeed", speedMultiplier);
            Animator.speed = speedMultiplier;
        }

        /// <summary>
        /// Initiates the left door leaf opening animation on layer 0.
        /// </summary>
        public void OpenLeftDoor()
        {
            StopObstacleRoutine();
            if (isLeftDoorOpen) return;
            SetAnimatorSpeed();
            SetLayerWeights(true);
            Animator.Play("Left_Door_Open", 0, 0f);
            isLeftDoorOpen = true;
        }

        /// <summary>
        /// Initiates the left door leaf closing animation on layer 0.
        /// </summary>
        public void CloseLeftDoor()
        {
            StopObstacleRoutine();
            if (!isLeftDoorOpen) return;
            SetAnimatorSpeed();
            SetLayerWeights(true);
            Animator.Play("Left_Door_Close", 0, 0f);
            isLeftDoorOpen = false;
        }

        /// <summary>
        /// Initiates the right door leaf opening animation on layer 1.
        /// </summary>
        public void OpenRightDoor()
        {
            StopObstacleRoutine();
            if (isRightDoorOpen) return;
            SetAnimatorSpeed();
            SetLayerWeights(true);
            Animator.Play("Right_Door_Open", 1, 0f);
            isRightDoorOpen = true;
        }

        /// <summary>
        /// Initiates the right door leaf closing animation on layer 1.
        /// </summary>
        public void CloseRightDoor()
        {
            StopObstacleRoutine();
            if (!isRightDoorOpen) return;
            SetAnimatorSpeed();
            SetLayerWeights(true);
            Animator.Play("Right_Door_Close", 1, 0f);
            isRightDoorOpen = false;
        }

        /// <summary>
        /// Initiates simultaneous opening animations on both door leaves.
        /// </summary>
        public void OpenBothDoors()
        {
            StopObstacleRoutine();
            SetAnimatorSpeed();
            SetLayerWeights(true);
            if (!isLeftDoorOpen)
            {
                Animator.Play("Left_Door_Open", 0, 0f);
                isLeftDoorOpen = true;
            }

            if (!isRightDoorOpen)
            {
                Animator.Play("Right_Door_Open", 1, 0f);
                isRightDoorOpen = true;
            }
        }

        /// <summary>
        /// Initiates simultaneous closing animations on both door leaves.
        /// </summary>
        public void CloseBothDoors()
        {
            StopObstacleRoutine();
            SetAnimatorSpeed();
            SetLayerWeights(true);
            if (isLeftDoorOpen)
            {
                Animator.Play("Left_Door_Close", 0, 0f);
                isLeftDoorOpen = false;
            }

            if (isRightDoorOpen)
            {
                Animator.Play("Right_Door_Close", 1, 0f);
                isRightDoorOpen = false;
            }
        }

        /// <summary>
        /// Plays the obstacle reopen animation clip on the primary layer.
        /// </summary>
        public void PlayObstacleReopen()
        {
            StopObstacleRoutine();
            SetAnimatorSpeed();
            SetLayerWeights(false);
            Animator.Play("Obstacle_Reopen", 0, 0f);
        }

        /// <summary>
        /// Plays the obstacle retry animation clip on the primary layer.
        /// </summary>
        public void PlayObstacleRetry()
        {
            StopObstacleRoutine();
            SetAnimatorSpeed();
            SetLayerWeights(false);
            Animator.Play("Obstacle_Retry", 0, 0f);
        }

        /// <summary>
        /// Plays the persistent obstacle animation clip holding doors at 50mm position.
        /// </summary>
        public void PlayPersistentObstacle()
        {
            StopObstacleRoutine();
            SetAnimatorSpeed();
            SetLayerWeights(false);
            Animator.Play("Persistent_Obstacle", 0, 0f);
        }

        /// <summary>
        /// Plays the obstacle during opening animation clip on the primary layer.
        /// </summary>
        public void PlayObstacleDuringOpening()
        {
            StopObstacleRoutine();
            SetAnimatorSpeed();
            SetLayerWeights(false);
            Animator.Play("Obstacle_During_Opening", 0, 0f);
        }

        /// <summary>
        /// Rapidly closes open doors at high speed upon train motion to prevent dragging accidents.
        /// </summary>
        public void PlayAntiDragAction()
        {
            StopObstacleRoutine();
            if (!isLeftDoorOpen && !isRightDoorOpen) return;

            if (Animator != null)
            {
                float highSpeed = (baseClipDurationSeconds / Mathf.Max(0.1f, doorMotionDurationSeconds)) * 2.0f;
                Animator.SetFloat("MotionSpeed", highSpeed);
                Animator.speed = highSpeed;
            }

            SetLayerWeights(false);
            Animator.Play("Anti_drag_Action", 0, 0f);
            isLeftDoorOpen = false;
            isRightDoorOpen = false;
        }

        /// <summary>
        /// Cancels existing obstacle actions and begins the automated obstacle reopen-retry cycle.
        /// </summary>
        public void TriggerObstacleSequence()
        {
            StopObstacleRoutine();
            obstacleRoutine = StartCoroutine(ObstacleSequenceRoutine());
        }

        /// <summary>
        /// Coroutine managing reopening and retrying close operations up to the retry limit before holding open.
        /// </summary>
        private IEnumerator ObstacleSequenceRoutine()
        {
            for (var i = 1; i <= maxObstacleRetries; i++)
            {
                SetLayerWeights(false);
                Animator.Play("Obstacle_Reopen", 0, 0f);
                yield return new WaitForSeconds(Mathf.Max(1.0f, doorMotionDurationSeconds * 0.5f));

                yield return new WaitForSeconds(1.0f);

                SetLayerWeights(false);
                Animator.Play("Obstacle_Retry", 0, 0f);
                yield return new WaitForSeconds(doorMotionDurationSeconds);
            }

            SetLayerWeights(false);
            Animator.Play("Persistent_Obstacle", 0, 0f);
        }

        /// <summary>
        /// Stops the running obstacle coroutine and resets its reference.
        /// </summary>
        private void StopObstacleRoutine()
        {
            if (obstacleRoutine == null) return;
            StopCoroutine(obstacleRoutine);
            obstacleRoutine = null;
        }

        /// <summary>
        /// Updates the text display showing the configured door motion duration in seconds.
        /// </summary>
        private void UpdateTimingUI(float sec)
        {
            if (timingText) timingText.text = $"Door Duration: {sec:F1} s";
        }

        /// <summary>
        /// Updates the text display showing the maximum number of obstacle retries.
        /// </summary>
        private void UpdateObstacleRetryUI(int retries)
        {
            if (obstacleRetryText) obstacleRetryText.text = $"Obstacle Retries: {retries}";
        }

        /// <summary>
        /// Processes incoming numeric UDP data to adjust timings, retry limits, and trigger door actions.
        /// </summary>
        protected override void OnReceiveUDPData(List<float> values)
        {
            return;
            if (values == null || values.Count == 0) return;
            var valueIndex = 0;
            
            if (timingSlider && !float.IsNaN(values[valueIndex]))
            {
                doorMotionDurationSeconds = Mathf.Clamp(values[valueIndex], 1f, 10f);
                if (timingSlider) timingSlider.value = doorMotionDurationSeconds;
                UpdateTimingUI(doorMotionDurationSeconds);
                SetAnimatorSpeed();
                valueIndex++;
            }

            if (obstacleRetrySlider && !float.IsNaN(values[valueIndex]))
            {
                maxObstacleRetries = Mathf.Clamp(Mathf.RoundToInt(values[valueIndex]), 1, 5);
                if (obstacleRetrySlider) obstacleRetrySlider.value = maxObstacleRetries;
                UpdateObstacleRetryUI(maxObstacleRetries);
                valueIndex++;
            }

            for (var i = valueIndex; i < values.Count; i++)
            {
                var value = Mathf.FloorToInt(values[i]);
                ExecuteUDPCommand(i - valueIndex, value > 0);
            }
        }

        protected override void ReceivedGenericParameters(GenericParameterGroup genericParameterGroup)
        {
            if (genericParameterGroup == null || genericParameterGroup.Parameters == null) return;
            
            var genericParameters = genericParameterGroup.Parameters;
            var speedParameters = genericParameters.FindAll(p => p.Unit == "Speed");
            var boolParameters = genericParameters.FindAll(p => p.Unit is "Toggle" or "Bool" or "Boolean");
            var quitParameter = genericParameters.Find(p => p.Unit == "DS3D OFF");
            var sliderUIParameter= genericParameters.Find(p => p.Unit == "SliderUI");

            if (sliderUIParameter != null)
            {
                IsCustomUIVisible = sliderUIParameter.AsBool;
                UpdateCanvasVisibility(PassengerDoorToggle.isOn && sliderUIParameter.AsBool);
            }

            if (timingSlider)
            {
                var value=speedParameters.Find(p => p.Name == "door_motion_time")?.Value ?? doorMotionDurationSeconds;
                doorMotionDurationSeconds = Mathf.Clamp(value, 1f, 10f);
                timingSlider.value = doorMotionDurationSeconds;
                UpdateTimingUI(doorMotionDurationSeconds);
                SetAnimatorSpeed();
            }

            if (obstacleRetrySlider)
            {
                var value=speedParameters.Find(p => p.Name == "max_obstacle_retries")?.Value ?? doorMotionDurationSeconds;
                maxObstacleRetries = Mathf.Clamp(Mathf.RoundToInt(value), 1, 5);
                if (obstacleRetrySlider) obstacleRetrySlider.value = maxObstacleRetries;
                UpdateObstacleRetryUI(maxObstacleRetries);
            }
            
            foreach (var genericParameter in boolParameters)
            {
                var visible = genericParameter.Value > 0;
                switch (genericParameter.Name)
                {
                    case "open_left":
                        if (visible) OpenLeftDoor();
                        break;
                    case "close_left":
                        if (visible) CloseLeftDoor();
                        break;
                    case "open_right":
                        if (visible) OpenRightDoor();
                        break;
                    case "close_right":
                        if (visible) CloseRightDoor();
                        break;
                    case "open_both":
                        if (visible) OpenBothDoors();
                        break;
                    case "close_both":
                        if (visible) CloseBothDoors();
                        break;
                    case "obstacle":
                        if (visible) TriggerObstacleSequence();
                        break;
                    case "obstacle_reopen":
                        if (visible) PlayObstacleReopen();
                        break;
                    case "obstacle_retry":
                        if (visible) PlayObstacleRetry();
                        break;
                    case "persistent_obstacle":
                        if (visible) PlayPersistentObstacle();
                        break;
                    case "obstacle_opening":
                        if (visible) PlayObstacleDuringOpening();
                        break;
                    case "anti_drag":
                        if (visible) PlayAntiDragAction();
                        break;
                }
            }

            if (quitParameter is { AsBool: true })
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                            Application.Quit();
#endif
            }

        }

        /// <summary>
        /// Dispatches a door action or quit command matching the received UDP command index.
        /// </summary>
        private void ExecuteUDPCommand(int code, bool state)
        {
            Debug.Log($"UDP Command: {code} state: {state}");
            switch (code)
            {
                case 0:
                    if (state) OpenLeftDoor();
                    break;
                case 1:
                    if (state) CloseLeftDoor();
                    break;
                case 2:
                    if (state) OpenRightDoor();
                    break;
                case 3:
                    if (state) CloseRightDoor();
                    break;
                case 4:
                    if (state) OpenBothDoors();
                    break;
                case 5:
                    if (state) CloseBothDoors();
                    break;
                case 6:
                    if (state) TriggerObstacleSequence();
                    break;
                case 7:
                    if (state) PlayObstacleReopen();
                    break;
                case 8:
                    if (state) PlayObstacleRetry();
                    break;
                case 9:
                    if (state) PlayPersistentObstacle();
                    break;
                case 10:
                    if (state) PlayObstacleDuringOpening();
                    break;
                case 11:
                    if (state) PlayAntiDragAction();
                    break;
                case 12:
                    if (state)
                    {
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif
                    }
                    break;
            }
        }
        
        /// <summary>
        /// Registers a toggle listener to show or hide a model, update its label color, and assign the active animator.
        /// </summary>
        private void RegisterModelToggle(Toggle toggle, TMP_Text label, GameObject model, Animator anim, List<int> faultIndexes)
        {
            if (!toggle) return;
            toggle.onValueChanged.AddListener(state =>
            {
                if (label != null) label.color = state ? Color.red : Color.black;
                if (model != null) model.transform.position = state ? Vector3.zero : _hiddenPosition;

                if (!state) return;
                UpdateCanvasVisibility(toggle == PassengerDoorToggle && IsCustomUIVisible);
                Animator = anim;
                ResetAnimation();

                var faultList = RefManager.Instance.FaultManager.faults.FaultList.FindAll(f =>
                    f.FaultIndex >= faultIndexes[0] && f.FaultIndex <= faultIndexes[^1]);
                if (faultList.Count > 0)
                {
                    var faults = new Faults() { FaultList = faultList };
                    EventManager.OnFaultDetect?.Invoke(faults);
                }
            });
        }
    }
}