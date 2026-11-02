using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace CCEPL_CAD
{
    public class ModelAnimationManager : MonoBehaviour
    {
        // --- 1. Data Structures ---

        [Serializable]
        public class AnimationEntry
        {
            public string animationId;
            public AnimationClip clip;
            [Range(0f, 1f)] public float startWeight = 1f;
            public float speed = 1f;
        }

        [Serializable]
        public class AnimationGroup
        {
            public string GroupName;
            public List<AnimationEntry> Clips = new();
            
            [Header("UI Generation")]
            public bool CreateSpeedSlider = true;
            public bool CreateAmplitudeSlider = true;
        }

        private class AnimationData
        {
            public int Index;
            public AnimationClipPlayable Playable;
            public float CurrentSliderSpeedValue = 5f; // Used to recalculate when reversing
        }

        // --- 2. Inspector References ---

        [Header("References")]
        [SerializeField] private Animator animator;

        [Header("Grouped Animations (Generates UI)")]
        public List<AnimationGroup> animationGroups = new();

        [Header("Default Animations (No UI)")] 
        [SerializeField] private AnimationEntry[] defaultAnimations;

        [Header("Settings")]
        [SerializeField] private int maxAnimations = 100;

        [Header("UI Generation References")]
        public SliderForSpeed SliderPrefab;
        public RectTransform SpeedContainer;
        public RectTransform AmplitudeContainer;
        public Toggle ReverseAnimationToggle;

        // --- 3. Internal State ---
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private readonly Dictionary<string, AnimationData> animations = new();
        private int currentInputIndex;
        private bool isGraphInitialized = false;

        // --- 4. Unity Lifecycle ---

        /// <summary>
        /// Initializes the PlayableGraph and AnimationMixerPlayable, registers animations, generates UI controls, and attaches reverse toggle listeners.
        /// </summary>
        private void Awake()
        {
            if (animator != null) animator.runtimeAnimatorController = null; // Prevent conflicts

            graph = PlayableGraph.Create("ModelAnimationGraph");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            mixer = AnimationMixerPlayable.Create(graph, maxAnimations);

            var output = AnimationPlayableOutput.Create(graph, "AnimationOutput", animator);
            output.SetSourcePlayable(mixer);

            RegisterDefaultAnimations();
            RegisterGroupAnimations();

            graph.Play();
            isGraphInitialized = true;

            GenerateUI();

            if (ReverseAnimationToggle != null)
            {
                ReverseAnimationToggle.onValueChanged.AddListener(_ => RefreshAllSpeeds());
            }
        }
        
        /// <summary>
        /// Resumes animation PlayableGraph playback when the component becomes enabled.
        /// </summary>
        private void OnEnable()
        {
            if (isGraphInitialized && graph.IsValid()) graph.Play();
        }

        /// <summary>
        /// Pauses animation PlayableGraph playback when the component becomes disabled.
        /// </summary>
        private void OnDisable()
        {
            if (isGraphInitialized && graph.IsValid()) graph.Stop();
        }

        /// <summary>
        /// Destroys the PlayableGraph to release animation resources upon component destruction.
        /// </summary>
        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        // --- 5. Registration & UI Generation ---

        /// <summary>
        /// Registers configured default animation clips with the animation playable mixer.
        /// </summary>
        private void RegisterDefaultAnimations()
        {
            if (defaultAnimations == null) return;
            foreach (var anim in defaultAnimations)
            {
                AddAnimation(anim.animationId, anim.clip, anim.startWeight, anim.speed);
            }
        }

        /// <summary>
        /// Registers all clips in user-defined animation groups into the playable mixer.
        /// </summary>
        private void RegisterGroupAnimations()
        {
            foreach (var group in animationGroups)
            {
                foreach (var anim in group.Clips)
                {
                    AddAnimation(anim.animationId, anim.clip, anim.startWeight, anim.speed);
                }
            }
        }

        /// <summary>
        /// Dynamically instantiates and configures speed and amplitude UI sliders for each animation group.
        /// </summary>
        private void GenerateUI()
        {
            foreach (var group in animationGroups)
            {
                if (group.CreateSpeedSlider && SpeedContainer != null)
                {
                    var speedGo = Instantiate(SliderPrefab, SpeedContainer);
                    speedGo.name = $"Slider_Speed_{group.GroupName}";
                    ConfigureSlider(speedGo, $"{group.GroupName} Speed", value => ApplyGroupSpeed(group, value));
                }

                if (group.CreateAmplitudeSlider && AmplitudeContainer != null)
                {
                    var ampGo = Instantiate(SliderPrefab, AmplitudeContainer);
                    ampGo.name = $"Slider_Amp_{group.GroupName}";
                    ConfigureSlider(ampGo, $"{group.GroupName} Amplitude", value => ApplyGroupAmplitude(group, value));
                }
            }
        }

        /// <summary>
        /// Configures a speed or amplitude slider UI element with min/max values, labels, and event listeners.
        /// </summary>
        private void ConfigureSlider(SliderForSpeed sliderInstance, string label, UnityEngine.Events.UnityAction<float> onValueChanged)
        {
            if (sliderInstance == null || sliderInstance.SpeedSlider == null) return;

            var slider = sliderInstance.SpeedSlider;
            slider.minValue = 0f;
            slider.maxValue = 10f;
            slider.value = 5f;

            if (sliderInstance.NameText != null) sliderInstance.NameText.text = label;
            if (sliderInstance.MinValue != null) sliderInstance.MinValue.text = "0";
            if (sliderInstance.MaxValue != null) sliderInstance.MaxValue.text = "10";

            slider.onValueChanged.AddListener(onValueChanged);
        }

        // --- 6. Group Control (Used by UI) ---

        /// <summary>
        /// Maps a raw slider value to a playback speed and applies it to all clips in the specified animation group.
        /// </summary>
        private void ApplyGroupSpeed(AnimationGroup group, float rawSliderValue)
        {
            float mappedSpeed = GetMappedSpeed(rawSliderValue);
            if (ReverseAnimationToggle != null && ReverseAnimationToggle.isOn)
            {
                mappedSpeed *= -1f;
            }

            foreach (var clip in group.Clips)
            {
                if (animations.TryGetValue(clip.animationId, out var data))
                {
                    data.CurrentSliderSpeedValue = rawSliderValue; // Cache for reverse toggle
                }
                SetSpeed(clip.animationId, mappedSpeed);
            }
        }

        /// <summary>
        /// Converts a raw slider value into normalized blend weight and applies it to all clips in the group.
        /// </summary>
        private void ApplyGroupAmplitude(AnimationGroup group, float rawSliderValue)
        {
            float mappedWeight = rawSliderValue / 10f;
            foreach (var clip in group.Clips)
            {
                SetWeight(clip.animationId, mappedWeight);
            }
        }

        /// <summary>
        /// Recalculates and updates playback speeds across all animations when reverse toggle or speed settings change.
        /// </summary>
        private void RefreshAllSpeeds()
        {
            bool isReversed = ReverseAnimationToggle != null && ReverseAnimationToggle.isOn;

            foreach (var kvp in animations)
            {
                var data = kvp.Value;
                float mappedSpeed = GetMappedSpeed(data.CurrentSliderSpeedValue);
                SetSpeed(kvp.Key, isReversed ? -mappedSpeed : mappedSpeed);
            }
        }

        /// <summary>
        /// Converts a raw slider value into a non-linear animation playback speed multiplier.
        /// </summary>
        private float GetMappedSpeed(float rawValue)
        {
            return rawValue <= 5f ? rawValue / 5f : 1f + (rawValue - 5f) / 10f;
        }

        // --- 7. Your Original Individual Control Methods ---

        /// <summary>
        /// Creates and connects an AnimationClipPlayable to the mixer graph with given weight and speed.
        /// </summary>
        public bool AddAnimation(string animationId, AnimationClip clip, float weight = 1f, float speed = 1f)
        {
            if (string.IsNullOrEmpty(animationId) || clip == null || animations.ContainsKey(animationId))
                return false;

            if (currentInputIndex >= maxAnimations)
            {
                Debug.LogError("Maximum animation limit reached.");
                return false;
            }

            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetSpeed(speed);

            graph.Connect(playable, 0, mixer, currentInputIndex);
            mixer.SetInputWeight(currentInputIndex, Mathf.Clamp01(weight));

            animations.Add(animationId, new AnimationData
            {
                Index = currentInputIndex,
                Playable = playable,
                CurrentSliderSpeedValue = 5f
            });

            currentInputIndex++;
            return true;
        }

        /// <summary>
        /// Enables the specified animation by setting its mixer weight to full (1.0).
        /// </summary>
        public void EnableAnimation(string animationId) => SetWeight(animationId, 1f);
        /// <summary>
        /// Disables the specified animation by setting its mixer weight to zero.
        /// </summary>
        public void DisableAnimation(string animationId) => SetWeight(animationId, 0f);

        /// <summary>
        /// Sets the blend weight (clamped between 0 and 1) for the specified animation clip in the mixer.
        /// </summary>
        public void SetWeight(string animationId, float weight)
        {
            if (animations.TryGetValue(animationId, out var data))
                mixer.SetInputWeight(data.Index, Mathf.Clamp01(weight));
        }

        /// <summary>
        /// Retrieves the current mixer blend weight of the specified animation.
        /// </summary>
        public float GetWeight(string animationId)
        {
            return animations.TryGetValue(animationId, out var data) ? mixer.GetInputWeight(data.Index) : 0f;
        }

        /// <summary>
        /// Sets the playback speed for the specified animation playable.
        /// </summary>
        public void SetSpeed(string animationId, float speed)
        {
            if (animations.TryGetValue(animationId, out var data))
                data.Playable.SetSpeed(speed);
        }

        /// <summary>
        /// Retrieves the current playback speed of the specified animation playable.
        /// </summary>
        public double GetSpeed(string animationId)
        {
            return animations.TryGetValue(animationId, out var data) ? data.Playable.GetSpeed() : 0;
        }

        /// <summary>
        /// Pauses the specified animation by setting its playback speed to zero.
        /// </summary>
        public void PauseAnimation(string animationId) => SetSpeed(animationId, 0f);
        /// <summary>
        /// Resumes normal playback speed (1.0) for the specified animation.
        /// </summary>
        public void ResumeAnimation(string animationId) => SetSpeed(animationId, 1f);

        /// <summary>
        /// Checks whether an animation with the specified identifier is currently registered.
        /// </summary>
        public bool ContainsAnimation(string animationId) => animations.ContainsKey(animationId);

        /// <summary>
        /// Disconnects and destroys the animation playable for the given identifier, removing it from mixer management.
        /// </summary>
        public void RemoveAnimation(string animationId)
        {
            if (!animations.TryGetValue(animationId, out var data)) return;

            mixer.SetInputWeight(data.Index, 0f);

            if (data.Playable.IsValid())
                data.Playable.Destroy();

            animations.Remove(animationId);
        }
    }
}