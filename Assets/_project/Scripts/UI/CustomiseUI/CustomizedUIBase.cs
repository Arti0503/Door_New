using System.Collections.Generic;
using CCEPL_CAD.Models;
using UnityEngine;

namespace CCEPL_CAD
{
    public abstract class CustomizedUIBase : MonoBehaviour
    {
        public static CustomizedUIBase Instance;
        
        [Header("Base Animator Settings")]
        public Animator Animator;
        public RuntimeAnimatorController AnimatorController;
        public RuntimeAnimatorController FaultAnimatorController;

        [Header("Base UI Canvas Group")]
        public CanvasGroup CustomUICanvas;
        public bool IsCustomUIVisible = true;

        [Header("Idle State Objects")]
        public List<GameObject> IdleObjects = new List<GameObject>();

        /// <summary>
        /// Initializes the singleton instance reference and sets up the base animator.
        /// </summary>
        protected virtual void Awake()
        {
            Instance = this;
            InitializeBaseAnimator();
        }

        /// <summary>
        /// Configures initial canvas group visibility on startup.
        /// </summary>
        protected virtual void Start()
        {
            UpdateCanvasVisibility(IsCustomUIVisible);
        }

        /// <summary>
        /// Subscribes to UDP network events, fault triggers, fault completions, and animation reset events.
        /// </summary>
        protected virtual void OnEnable()
        {
            EventManager.ReceivedUDPData += OnReceiveUDPData;
            EventManager.ReceivedGenericParameters += ReceivedGenericParameters;
            EventManager.OnFaultsReceived += OnReceiveRawUDPString;
            EventManager.OnFaultClick += TriggerFault;
            EventManager.OnFaultCompleted += OnFaultCompleted;
            EventManager.ResetAnimation += ResetAnimation;
        }

        /// <summary>
        /// Unsubscribes from UDP and fault lifecycle events when disabled to prevent memory leaks.
        /// </summary>
        protected virtual void OnDisable()
        {
            EventManager.ReceivedUDPData -= OnReceiveUDPData;
            EventManager.ReceivedGenericParameters -= ReceivedGenericParameters;
            EventManager.OnFaultsReceived -= OnReceiveRawUDPString;
            EventManager.OnFaultClick -= TriggerFault;
            EventManager.OnFaultCompleted -= OnFaultCompleted;
            EventManager.ResetAnimation -= ResetAnimation;
        }

        /// <summary>
        /// Resolves the child Animator component and assigns the configured base runtime controller.
        /// </summary>
        protected virtual void InitializeBaseAnimator()
        {
            if (Animator == null)
            {
                Animator = GetComponentInChildren<Animator>();
            }

            if (Animator != null && AnimatorController != null)
            {
                Animator.runtimeAnimatorController = AnimatorController;
            }
        }

        /// <summary>
        /// Updates the alpha transparency and raycast interaction state of the custom UI canvas group.
        /// </summary>
        public virtual void UpdateCanvasVisibility(bool isVisible)
        {
            if (CustomUICanvas != null)
            {
                CustomUICanvas.alpha = isVisible ? 1f : 0f;
                CustomUICanvas.blocksRaycasts = isVisible;
            }
        }

        /// <summary>
        /// Responds to fault triggering by hiding the customization canvas and switching to the fault animator controller when appropriate.
        /// </summary>
        public virtual void TriggerFault(int faultIndex)
        {
            if(RefManager.Instance.FaultManager.IsFaultStart) return;

            var playWithIdle = RefManager.Instance != null &&
                               RefManager.Instance.FaultManager != null &&
                               RefManager.Instance.FaultManager.playWithIdle;

            //UpdateCanvasVisibility(false);

            if (!playWithIdle)
            {
                if (Animator == null || FaultAnimatorController == null) return;

                foreach (var idleObject in IdleObjects)
                {
                    if (idleObject != null) idleObject.SetActive(false);
                }

                Animator.runtimeAnimatorController = FaultAnimatorController;
            }
        }

        /// <summary>
        /// Restores canvas visibility, reenables idle objects, restores base animator controller, and resets animation upon fault completion.
        /// </summary>
        public virtual void OnFaultCompleted()
        {
            UpdateCanvasVisibility(IsCustomUIVisible);

            var playWithIdle = RefManager.Instance != null &&
                               RefManager.Instance.FaultManager != null &&
                               RefManager.Instance.FaultManager.playWithIdle;

            if (!playWithIdle)
            {
                foreach (var idleObject in IdleObjects)
                {
                    if (idleObject != null) idleObject.SetActive(true);
                }

                if (Animator != null && AnimatorController != null)
                {
                    Animator.enabled = false;
                    Animator.runtimeAnimatorController = AnimatorController;
                    Animator.enabled = true;
                }
            }

            ResetAnimation();
        }

        /// <summary>
        /// Resets the component's active animator state and reapplies slider configurations.
        /// </summary>
        public abstract void ResetAnimation();
        /// <summary>
        /// Processes incoming numeric UDP telemetry data specific to the derived component.
        /// </summary>
        protected abstract void OnReceiveUDPData(List<float> values);
        /// <summary>
        /// Processes incoming raw string commands or text packets received via UDP.
        /// </summary>
        protected virtual void OnReceiveRawUDPString(string raw) { }
        
        protected abstract void ReceivedGenericParameters(GenericParameterGroup genericParameterGroup);
    }
}