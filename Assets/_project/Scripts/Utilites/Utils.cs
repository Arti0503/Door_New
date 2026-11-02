using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.EventSystems;

namespace CCEPL_CAD
{
    public static class ByteConverter
    {
        /// <summary>
        /// Converts a list of 32-bit integers into a raw byte array.
        /// </summary>
        public static byte[] ConvertListToBytes(List<int> list)
        {
            byte[] bytes =
                new byte[list.Count * sizeof(int)];

            Buffer.BlockCopy(
                list.ToArray(),
                0,
                bytes,
                0,
                bytes.Length);

            return bytes;
        }

        /// <summary>
        /// Converts a raw byte array back into an array of 32-bit integers.
        /// </summary>
        public static int[] ConvertBytesToInts(byte[] bytes)
        {
            int[] result =
                new int[bytes.Length / sizeof(int)];

            Buffer.BlockCopy(
                bytes,
                0,
                result,
                0,
                bytes.Length);

            return result;
        }

        /// <summary>
        /// Maps a UI slider value (typically 1 to 10) to an animation playback speed multiplier (1x to 2x).
        /// </summary>
        public static float SliderToSpeed(float sliderValue)
        {
            return Mathf.Lerp(1f, 2f, (sliderValue - 1f) / 9f);
        }

        /// <summary>
        /// Inserts spaces between PascalCase words, acronyms, and alphanumeric transitions for human-readable display.
        /// </summary>
        public static string SplitWords(string input)
        {
            return Regex.Replace(
                input,
                @"(?<=[A-Z])(?=[A-Z][a-z])|(?<=[a-z])(?=[A-Z])|(?<=[A-Za-z])(?=\d)|(?<=\d)(?=[A-Za-z])",
                " ");
        }
    }

    public static class Utils
    {
        public static bool _isDragStartedOnUI = false;

        /// <summary>
        /// Checks if the mouse or touch pointer is currently interacting with or originated on a UI element.
        /// </summary>
        public static bool IsPointerOverUI()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            // 1. Mobile Touches
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began)
                {
                    _isDragStartedOnUI = eventSystem.IsPointerOverGameObject(touch.fingerId);
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    _isDragStartedOnUI = false;
                    return false;
                }

                return _isDragStartedOnUI;
            }

            // 2. Mouse / Editor / PC
            if (Input.GetMouseButtonDown(0))
            {
                _isDragStartedOnUI = eventSystem.IsPointerOverGameObject();
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _isDragStartedOnUI = false;
                return false;
            }
            
            if (Input.GetMouseButton(0)) return _isDragStartedOnUI;

            return eventSystem.IsPointerOverGameObject();
        }

        /// <summary>
        /// Determines whether an <see cref="Animator"/> controller contains a parameter with the specified name.
        /// </summary>
        public static bool HasParameter(Animator animator, string parameterName)
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == parameterName)
                    return true;
            }

            return false;
        }
        
        /// <summary>
        /// Finds a child GameObject by name recursively.
        /// </summary>
        public static GameObject FindChildByName(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
                return null;

            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child.gameObject;

                GameObject result = FindChildByName(child, childName);

                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Finds a child by name and changes its material.
        /// </summary>

        public static (HighLightObject, Material) ChangeChildMaterial(
            this Transform parent,
            string childName,
            Material material)
        {
            if (parent == null || material == null)
                return (null, null);

            var child = FindChildByName(parent, childName);

            if (child == null)
            {
                Debug.LogWarning($"Child '{childName}' not found under '{parent.name}'.");
                return (null, null);
            }

            var renderer = child.GetComponent<Renderer>();

            if (renderer == null)
            {
                Debug.LogWarning($"No Renderer found on '{child.name}'.");
                return (null, null);
            }

            var highLightObject = child.AddComponent<HighLightObject>();
            var instance = MonoBehaviour.Instantiate(material);
            renderer.sharedMaterial = instance;

            return (highLightObject, instance);
        }
    }
}