using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObjectVisibilityManager : MonoBehaviour
{
    [SerializeField] private List<GameObject> targetObjects;
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text buttonText;
    internal bool objectsVisible = true;

    /// <summary>
    /// Subscribes to the toggle button click event and resets the object visibility state and UI label.
    /// </summary>
    private void OnEnable()
    {
        toggleButton.onClick.AddListener(ToggleVisibility);
        objectsVisible = true;
        buttonText.text ="Hide Object";
    }

    /// <summary>
    /// Removes listeners from the toggle button when the component is disabled.
    /// </summary>
    private void OnDisable()
    {
        toggleButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// Toggles the active state of all target GameObjects and updates the button label accordingly.
    /// </summary>
    internal void ToggleVisibility()
    {
        objectsVisible = !objectsVisible;
        targetObjects.ForEach(x => x?.SetActive(objectsVisible));
        UpdateButtonText();
    }

    /// <summary>
    /// Updates the toggle button text based on the current visibility state of target objects.
    /// </summary>
    private void UpdateButtonText()
    {
        buttonText.text = $"{(objectsVisible ? "Hide" : "Show")} Object";
    }
}