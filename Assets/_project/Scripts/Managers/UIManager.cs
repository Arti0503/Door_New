using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CCEPL_CAD.Models;
using DG.Tweening;
using Newtonsoft.Json;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CCEPL_CAD
{
    public class UIManager : MonoBehaviour
    {
        private RefManager _refManager => RefManager.Instance != null
            ? RefManager.Instance
            : GameObject.FindObjectOfType<RefManager>();

        private UIReference _uiReference;

        private UIReference _uiRef
        {
            get
            {
                if (_uiReference == null && _refManager != null) _uiReference = _refManager.UIReference;
                return _uiReference;
            }
            set => _uiReference = value;
        }

        Faults _faults;

        private int StepPerform = -1;
        private int _currentFaultIndex = -1;
        public int CurrentFaultIndex => _currentFaultIndex;
        private int maxAttempt = 3;
        private List<FaultTab> _faultTabs = new();
        private List<FaultStep> _faultSteps = new();
        private List<Tool> _tools = new();
        [SerializeField] private List<int> _stepsAttempt;
        [SerializeField] private List<int> _wrongAttemptsPerStep = new();
        [SerializeField] private int _faultMistakes = 0;
        public int FaultMistakes => _faultMistakes;

        private bool leftOpen = false;
        private bool rightOpen = false;

        private Vector2 toolsPanelOpenPos;
        private Vector2 toolsPanelClosedPos;

        private Vector2 modesPanelOpenPos;
        private Vector2 modesPanelClosedPos;
        Tween _faultAnimation;
        private Tool _tool;
        private int currentClipIndex = 0;
        private bool splashFinished = false;
        internal ToolObject _selectedTool;

        private RunningFaultProgress _runningFaultProgress = null;
        private bool _positionsInitialized = false;

        private void EnsurePanelPositions()
        {
            if (_positionsInitialized) return;
            if (_uiRef == null || _uiRef.ToolPanel == null || _uiRef.FaultPanel == null) return;

            toolsPanelOpenPos = _uiRef.ToolPanel.anchoredPosition;
            modesPanelOpenPos = _uiRef.FaultPanel.anchoredPosition;
            toolsPanelClosedPos = toolsPanelOpenPos + Vector2.left * (_uiRef.ToolPanel.rect.width + 50);
            modesPanelClosedPos = modesPanelOpenPos + Vector2.right * (_uiRef.FaultPanel.rect.width + 50);
            _positionsInitialized = true;
        }

        /// <summary>
        /// Initializes panel anchor positions and places tools and fault mode panels offscreen in closed state.
        /// </summary>
        IEnumerator Start()
        {
            yield return null;
            EnsurePanelPositions();
            _uiRef.ToolPanel.anchoredPosition = toolsPanelClosedPos;
            _uiRef.FaultPanel.anchoredPosition = modesPanelClosedPos;
            leftOpen = false;
            rightOpen = false;
        }

        /// <summary>
        /// Registers UI button click listeners, binds event manager delegates, and initializes tool buttons and panel states.
        /// </summary>

        private void OnEnable()
        {
            _uiRef = _refManager.UIReference;
            _uiRef.ToolsBtn.onClick.AddListener(ToggleToolsUI);
            _uiRef.ToolsBackBtn.onClick.AddListener(ToggleToolsUI);
            _uiRef.ModesBtn.onClick.AddListener(ToggleModesUI);
            _uiRef.ModesBackBtn.onClick.AddListener(ToggleModesUI);
            _uiRef.RepairedFaultSubmitBtn.onClick.AddListener(SubmitRepairedFault);

            // [REMOVED FROM UI] Skip, BackStep, and UI Reset buttons disabled/commented out
            if (_uiRef.SkipFaultStepBtn != null)
                _uiRef.SkipFaultStepBtn.onClick.AddListener(() => SkipCurrentStep(true));
            if (_uiRef.BackStepBtn != null) _uiRef.BackStepBtn.onClick.AddListener(PreviousStep);
            if (_uiRef.FaultResetBtn != null) _uiRef.FaultResetBtn.onClick.AddListener(RequestResetFault);

            // Remove control buttons from exam mode toggle list so toggling modes never re-shows them
            if (_uiRef.HiddenObjectForExamMode != null)
            {
                _uiRef.HiddenObjectForExamMode.RemoveAll(x => x == null || (_uiRef.SkipFaultStepBtn != null &&
                                                                            x == _uiRef.SkipFaultStepBtn.gameObject) ||
                                                              (_uiRef.BackStepBtn != null &&
                                                               x == _uiRef.BackStepBtn.gameObject) ||
                                                              (_uiRef.FaultResetBtn != null &&
                                                               x == _uiRef.FaultResetBtn.gameObject));
            }

            _uiRef.TestModeToggle.onValueChanged.AddListener((state) =>
            {
                _uiRef.TestModeToggleText.color = state ? Color.red : Color.black;
                _uiRef.ModeBGImage.sprite = state ? _uiRef.TestModeBGSprite : _uiRef.ExamModeBGSprite;
                _uiRef.HiddenObjectForExamMode.ForEach(x => x.SetActive(state));
                LayoutRebuilder.ForceRebuildLayoutImmediate(_uiRef.ModeBGImage.rectTransform);
                HideRemovedControlButtons();
            });
            _uiRef.ExamModeToggle.onValueChanged.AddListener((state) =>
                _uiRef.ExamModeToggleText.color = state ? Color.red : Color.black);
            if (_uiRef.VideoPanel != null && _uiRef.VideoPlayer != null && _uiRef.VideoClips != null &&
                _uiRef.VideoClips.Count > 0)
            {
                _uiRef.VideoPlayer.clip = _uiRef.VideoClips[currentClipIndex];
                _uiRef.VideoPlayer.Play();
                _uiRef.VideoPanel.gameObject.SetActive(true);
                _uiRef.VideoPlayer.loopPointReached += VideoPlayeManager;
            }

            EventManager.OnToolSelected += ToolSelect;
            EventManager.OnFaultDetect += FaultDetect;
            EventManager.OnFaultClick += OnFaultClick;
            EventManager.OnStepComplete += OnStepComplete;
            EventManager.OnWrongToolSelected += WrongToolSelected;
            EventManager.OnLogMessage += OnLogMessage;
            EventManager.TriggerDisplayText += DisaplayStepMessage;
            EventManager.RequestTriggerFault += TriggerFaultFromNetwork;
            EventManager.RequestResetFault += RequestResetFault;
            EventManager.RequestSkipStep += HandleSkipRequest;
            EventManager.ModelSwitch += ModelSwitch;

            _uiRef.SkipFaultStepBtn.gameObject.SetActive(RefManager.Instance.FaultManager.Modes == Modes.Easy);
            _uiRef.BackStepBtn.gameObject.SetActive(RefManager.Instance.FaultManager.Modes == Modes.Easy);

            HideRemovedControlButtons();
            Init();
        }

        private void HideRemovedControlButtons()
        {
            if (_uiRef == null) return;
            if (_uiRef.SkipFaultStepBtn != null) _uiRef.SkipFaultStepBtn.gameObject.SetActive(false);
            if (_uiRef.BackStepBtn != null) _uiRef.BackStepBtn.gameObject.SetActive(false);
            if (_uiRef.FaultResetBtn != null) _uiRef.FaultResetBtn.gameObject.SetActive(false);
        }

        /// <summary>
        /// Handles incoming log notification messages and triggers styled banner display for success or error states.
        /// </summary>
        void OnLogMessage(string text, bool isError)
        {
            DisaplayMessage(text, isError ? _uiRef.FailureMessageTextColor : _uiRef.SuccessMessageTextColor,
                isError ? _uiRef.FailureMessageBGColor : _uiRef.SuccessMessageBGColor,
                isError ? _uiRef.FailureMessageOutlineColor : _uiRef.SuccessMessageOutlineColor);
        }

        /// <summary>
        /// Unregisters UI button click listeners and unsubscribes from all event delegates and video player callbacks.
        /// </summary>
        private void OnDisable()
        {
            _uiRef.ToolsBtn.onClick.RemoveAllListeners();
            _uiRef.ToolsBackBtn.onClick.RemoveAllListeners();
            _uiRef.ModesBtn.onClick.RemoveAllListeners();
            _uiRef.ModesBackBtn.onClick.RemoveAllListeners();
            _uiRef.RepairedFaultSubmitBtn.onClick.RemoveAllListeners();
            _uiRef.SkipFaultStepBtn.onClick.RemoveAllListeners();
            _uiRef.BackStepBtn.onClick.RemoveAllListeners();
            _uiRef.FaultResetBtn.onClick.RemoveAllListeners();
            _uiRef.VideoPlayer.loopPointReached -= VideoPlayeManager;
            EventManager.OnToolSelected -= ToolSelect;
            EventManager.OnFaultDetect -= FaultDetect;
            EventManager.OnFaultClick -= OnFaultClick;
            EventManager.OnStepComplete -= OnStepComplete;
            EventManager.OnWrongToolSelected -= WrongToolSelected;
            EventManager.OnLogMessage -= OnLogMessage;
            EventManager.TriggerDisplayText -= DisaplayStepMessage;
            EventManager.RequestTriggerFault -= TriggerFaultFromNetwork;
            EventManager.RequestResetFault -= RequestResetFault;
            EventManager.RequestSkipStep -= HandleSkipRequest;
            EventManager.ModelSwitch -= ModelSwitch;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (_refManager != null && _refManager.FaultManager != null && _refManager.FaultManager.IsFaultStart && !DirectModelRotatorAndScaler.IsLocked)
                {
                    Debug.Log("[UIManager] Key Shortcut (N/RightArrow) -> Fast Skip Step");
                    HandleSkipRequest();
                }
            }
        }

        private void HandleSkipRequest()
        {
            SkipCurrentStep(true);
        }

        public void SetToolsUI(bool open, bool animate = true)
        {
            EnsurePanelPositions();
            if (leftOpen == open) return;
            leftOpen = open;

            if (animate && _uiRef != null && _uiRef.ToolPanel != null)
            {
                var seq = DOTween.Sequence();
                seq.Append(_uiRef.ToolPanel.DOAnchorPos(leftOpen ? toolsPanelOpenPos : toolsPanelClosedPos, 0.45f));
                seq.Join(_uiRef.ToolPanel.DOScale(leftOpen ? 1f : 0.95f, 0.45f));
                seq.SetEase(Ease.OutBack);
            }
            else if (_uiRef != null && _uiRef.ToolPanel != null)
            {
                _uiRef.ToolPanel.anchoredPosition = leftOpen ? toolsPanelOpenPos : toolsPanelClosedPos;
                _uiRef.ToolPanel.localScale = leftOpen ? Vector3.one : new Vector3(0.95f, 0.95f, 1f);
            }
        }

        public void SetModesUI(bool open, bool animate = true)
        {
            EnsurePanelPositions();
            if (rightOpen == open) return;
            rightOpen = open;
            if (animate && _uiRef != null && _uiRef.FaultPanel != null)
            {
                var seq = DOTween.Sequence();
                seq.Append(_uiRef.FaultPanel.DOAnchorPos(rightOpen ? modesPanelOpenPos : modesPanelClosedPos, 0.45f));
                seq.Join(_uiRef.FaultPanel.DOScale(rightOpen ? 1f : 0.95f, 0.45f));
                seq.SetEase(Ease.OutBack);
            }
            else if (_uiRef != null && _uiRef.FaultPanel != null)
            {
                _uiRef.FaultPanel.anchoredPosition = rightOpen ? modesPanelOpenPos : modesPanelClosedPos;
                _uiRef.FaultPanel.localScale = rightOpen ? Vector3.one : new Vector3(0.95f, 0.95f, 1f);
            }
        }

        /// <summary>
        /// Toggles the visibility of the tools UI panel between open and closed positions using DOTween animation.
        /// </summary>
        private void ToggleToolsUI() => SetToolsUI(!leftOpen);

        /// <summary>
        /// Toggles the visibility of the fault modes panel between open and closed positions using DOTween animation.
        /// </summary>
        private void ToggleModesUI() => SetModesUI(!rightOpen);

        #region Automate Scroll UI

        private void ScrollToFaultTab(FaultTab targetTab)
        {
            if (targetTab == null || _uiRef == null || _uiRef.FaultTabParent == null) return;

            var scrollRect = _uiRef.FaultTabParent.GetComponentInParent<ScrollRect>();
            if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null) return;

            DOVirtual.DelayedCall(0.05f, () =>
            {
                if (targetTab == null || scrollRect == null || scrollRect.content == null ||
                    scrollRect.viewport == null) return;

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

                RectTransform targetRect = targetTab.GetComponent<RectTransform>();
                if (targetRect == null) return;

                float contentWidth = scrollRect.content.rect.width;
                float viewportWidth = scrollRect.viewport.rect.width;
                if (contentWidth <= viewportWidth) return;

                Vector3 localPos = scrollRect.content.InverseTransformPoint(targetRect.position);
                float childCenterX = localPos.x - scrollRect.content.rect.xMin;
                float halfView = viewportWidth * 0.5f;
                float targetScroll = Mathf.Clamp(childCenterX - halfView, 0f, contentWidth - viewportWidth);
                float normalized = Mathf.Clamp01(targetScroll / (contentWidth - viewportWidth));

                DOTween.To(() => scrollRect.horizontalNormalizedPosition,
                        x => scrollRect.horizontalNormalizedPosition = x, normalized, 0.4f)
                    .SetEase(Ease.OutCubic);
            });
        }

        private void ScrollToTool(Tool targetTool)
        {
            if (targetTool == null || _uiRef == null || _uiRef.ToolParent == null) return;

            var scrollRect = _uiRef.ToolParent.GetComponentInParent<ScrollRect>();
            if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null) return;

            DOVirtual.DelayedCall(0.05f, () =>
            {
                if (targetTool == null || scrollRect == null || scrollRect.content == null ||
                    scrollRect.viewport == null) return;

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

                RectTransform targetRect = targetTool.GetComponent<RectTransform>();
                if (targetRect == null) return;

                float contentHeight = scrollRect.content.rect.height;
                float viewportHeight = scrollRect.viewport.rect.height;
                if (contentHeight <= viewportHeight) return;

                Vector3 localPos = scrollRect.content.InverseTransformPoint(targetRect.position);
                float childDistFromTop = Mathf.Abs(localPos.y);
                float halfView = viewportHeight * 0.5f;
                float targetScroll = Mathf.Clamp(childDistFromTop - halfView, 0f, contentHeight - viewportHeight);
                float normalized = 1f - Mathf.Clamp01(targetScroll / (contentHeight - viewportHeight));

                DOTween.To(() => scrollRect.verticalNormalizedPosition, x => scrollRect.verticalNormalizedPosition = x,
                        normalized, 0.4f)
                    .SetEase(Ease.OutCubic);
            });
        }

        private void ScrollToStep(FaultStep targetStep)
        {
            if (targetStep == null || _uiRef == null || _uiRef.FaultStepParent == null) return;

            var scrollRect = _uiRef.FaultStepParent.GetComponentInParent<ScrollRect>();
            if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null) return;

            DOVirtual.DelayedCall(0.05f, () =>
            {
                if (targetStep == null || scrollRect == null || scrollRect.content == null ||
                    scrollRect.viewport == null) return;

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

                RectTransform targetRect = targetStep.GetComponent<RectTransform>();
                if (targetRect == null) return;

                float contentHeight = scrollRect.content.rect.height;
                float viewportHeight = scrollRect.viewport.rect.height;
                if (contentHeight <= viewportHeight) return;

                Vector3 localPos = scrollRect.content.InverseTransformPoint(targetRect.position);
                float childDistFromTop = Mathf.Abs(localPos.y);
                float halfView = viewportHeight * 0.5f;
                float targetScroll = Mathf.Clamp(childDistFromTop - halfView, 0f, contentHeight - viewportHeight);
                float normalized = 1f - Mathf.Clamp01(targetScroll / (contentHeight - viewportHeight));

                DOTween.To(() => scrollRect.verticalNormalizedPosition, x => scrollRect.verticalNormalizedPosition = x,
                        normalized, 0.4f)
                    .SetEase(Ease.OutCubic);
            });
        }

        #endregion


        /// <summary>
        /// Handles tool selection events by updating the active tool reference and resetting tool button states.
        /// </summary>
        void ToolSelect(ToolType toolType, ToolObject toolObject)
        {
            //_uiRef.ToolPanel.SetActive(false);
            if (_tool && _tool._toolType == toolType) _tool = null;
            _tools.ForEach(x => x.ResetSelection());
            _uiRef.MessageObj.gameObject.SetActive(false);
        }

        /// <summary>
        /// Handles step completion event by marking the current step done, incrementing progress, and checking next step.
        /// </summary>
        void OnStepComplete()
        {
            if (_refManager == null || _refManager.FaultManager == null ||
                !_refManager.FaultManager.IsFaultStart) return;
            _tools.ForEach(x => x.ResetSelection());
            _faultSteps[StepPerform].OnStepComplete();

            int stepNumber = StepPerform + 1;
            int totalSteps = _faultSteps.Count;
            bool isLastStep = (StepPerform + 1 >= _faultSteps.Count);

            StepPerform++;

            EventManager.OnStepAccomplished?.Invoke(_currentFaultIndex, stepNumber, totalSteps, isLastStep);

            CheckAllStepAreCompleted();
        }

        /// <summary>
        /// Checks whether all steps in the fault procedure are finished, enabling submission or preparing the next step.
        /// </summary>
        private void CheckAllStepAreCompleted()
        {
            _uiRef.FaultDisplayObj.gameObject.SetActive(false);
            if (_faultAnimation != null)
            {
                _faultAnimation.Kill();
                _faultAnimation = null;
            }

            if (StepPerform >= _faultSteps.Count)
            {
                _uiRef.RepairedFaultSubmitBtn.interactable = true;
                if (_uiRef.SkipFaultStepBtn != null) _uiRef.SkipFaultStepBtn.interactable = false;
                if (_uiRef.BackStepBtn != null) _uiRef.BackStepBtn.interactable = false;
                if (_uiRef.FaultResetBtn != null) _uiRef.FaultResetBtn.interactable = false;
                EventManager.OnLogMessage?.Invoke("Fault Repaired Successfully", false);
                Debug.Log($"[UIManager] Fault Repaired Successfully | Total Mistakes: {_faultMistakes}");

                EventManager.OnFaultFinished?.Invoke(_currentFaultIndex, _faultSteps.Count, _faultMistakes);
            }
            else
            {
                var step = _faultSteps[StepPerform];
                if (_uiRef.SkipFaultStepBtn != null) _uiRef.SkipFaultStepBtn.interactable = step.IsSkippable();
                if (_uiRef.BackStepBtn != null) _uiRef.BackStepBtn.interactable = StepPerform > 0;
                step.OnStepStart();
                ScrollToStep(step);

                _tool = _tools.Find(x => x._toolType == step.Step.RequireToolType);
                if (step.IsSkippable() && _tool != null)
                {
                    _tool.HighLightTool();
                    ScrollToTool(_tool);
                }
            }

            HideRemovedControlButtons();
        }

        /// <summary>
        /// Retrieves the currently executing fault step UI object, or null if no step is active.
        /// </summary>
        internal FaultStep GetCurrentFaultStep()
        {
            return StepPerform >= _faultSteps.Count || StepPerform == -1 ? null : _faultSteps[StepPerform];
        }


        /// <summary>
        /// Resets panels and controls to default states and instantiates tool buttons from the ScriptableObject configuration.
        /// </summary>
        void Init()
        {
            //Disable all panels
            //_uiRef.ToolPanel.gameObject.SetActive(false);
            //_uiRef.FaultPanel.gameObject.SetActive(false);
            _uiRef.ComponentPanel.gameObject.SetActive(false);
            _uiRef.MessageObj.gameObject.SetActive(false);
            _uiRef.RepairedFaultSubmitBtn.interactable = false;
            _uiRef.RepairedFaultSubmitBtn.transform.parent.gameObject.SetActive(false);
            _uiRef.RepairedFaultSubmitBtn.interactable = false;
            if (_uiRef.SkipFaultStepBtn != null) _uiRef.SkipFaultStepBtn.interactable = true;
            if (_uiRef.BackStepBtn != null) _uiRef.BackStepBtn.interactable = false;
            if (_uiRef.FaultResetBtn != null) _uiRef.FaultResetBtn.interactable = true;
            HideRemovedControlButtons();

            _faultMistakes = 0;
            UpdateMistakeCounterUI();

            //Testmode is default
            _uiRef.TestModeToggle.SetIsOnWithoutNotify(true);
            _uiRef.ExamModeToggle.SetIsOnWithoutNotify(false);

            //Tools Instantiate
            foreach (var toolsData in _refManager.ToolsDataSo.ToolsData)
            {
                if (toolsData.ToolObject == null) continue;
                var tool = Instantiate(_uiRef.ToolPrefab, _uiRef.ToolParent);
                tool.SetTool(new ToolData()
                {
                    ToolIcon = toolsData.ToolIcon,
                    ToolName = toolsData.ToolType.ToString(),
                    ToolType = toolsData.ToolType,
                    ToolObject = toolsData.ToolObject
                });
                _tools.Add(tool);
            }
        }

        /// <summary>
        /// Handles fault detection events by caching the faults and instantiating UI tabs for each fault.
        /// </summary>
        void FaultDetect(Faults faults)
        {
            EventManager.ModelSwitch?.Invoke();
            if (_faultTabs.Count > 0)
            {
                if (_refManager.FaultManager.IsFaultStart) ResetUI();
                foreach (var tab in _faultTabs)
                {
                    if (tab != null) Destroy(tab.gameObject);
                }

                _faultTabs.Clear();
                _faultTabs = new List<FaultTab>();
            }

            this._faults = faults;
            foreach (var fault in faults.FaultList)
            {
                var faultTab = Instantiate(_uiRef.FaultTabPrefab, _uiRef.FaultTabParent);
                faultTab.Init(fault.FaultIndex);
                _faultTabs.Add(faultTab);
            }

            if (_runningFaultProgress != null &&
                _faultTabs.Exists(x => x._faultIndex == _runningFaultProgress.CurrentFaultIndex))
            {
                StartFault(_runningFaultProgress.CurrentFaultIndex, true);
            }
            else ScrollToFaultTab(_faultTabs[0]);
        }


        public void TriggerFaultFromNetwork(int faultCode)
        {
            EnsureFaultsLoaded();
            if (_faults == null || _faults.FaultList == null)
            {
                EventManager.OnLogMessage?.Invoke($"Cannot trigger fault {faultCode}: Fault list is not loaded yet.",
                    true);
                return;
            }

            var faultData = _faults.FaultList.Find(x => x.FaultIndex == faultCode);
            if (faultData == null)
            {
                EventManager.OnLogMessage?.Invoke($"Fault code {faultCode} not found in FaultList.", true);
                return;
            }

            if (_refManager != null && _refManager.FaultManager != null && _refManager.FaultManager.IsFaultStart)
            {
                ResetUI();
                EventManager.OnFaultCompleted?.Invoke();
            }

            EventManager.OnFaultClick?.Invoke(faultCode);
        }

        private void EnsureFaultsLoaded()
        {
            if (_faults == null && _refManager != null && _refManager.FaultManager != null)
            {
                _faults = _refManager.FaultManager.faults;
            }
        }

        /// <summary>
        /// Selects and begins a fault procedure when a fault tab is clicked, setting up step UI elements and starting the first step.
        /// </summary>
        void OnFaultClick(int faultIndex)
        {
            StartFault(faultIndex, false);
        }

        private void StartFault(int faultIndex, bool isRestoring)
        {
            if (!isRestoring && _refManager != null && _refManager.FaultManager != null &&
                _refManager.FaultManager.IsFaultStart)
            {
                EventManager.OnLogMessage?.Invoke("Please finish the selected fault first.", true);
                return;
            }

            EnsureFaultsLoaded();
            if (_faults == null || _faults.FaultList == null) return;

            var faultData = _faults.FaultList.Find(x => x.FaultIndex == faultIndex);
            if (faultData == null) return;

            // Automatically open both Tool Panel (left) and Fault/Modes Panel (right)
            SetToolsUI(true);
            SetModesUI(true);

            _currentFaultIndex = faultIndex;
            if (isRestoring)
            {
                CustomizedUIBase.Instance?.TriggerFault(faultIndex);
            }
            if (_refManager != null && _refManager.FaultManager != null) _refManager.FaultManager.IsFaultStart = true;
            _faultTabs.ForEach(x => x.ResetGraphics());
            var matchingTab = _faultTabs.Find(x => x._faultIndex == faultIndex);
            if (matchingTab != null)
            {
                matchingTab.SelectTab();
                ScrollToFaultTab(matchingTab);
            }

            _uiRef.FaultDescription.text = faultData.Message;
            foreach (var stepTemp in _faultSteps)
            {
                if (stepTemp != null) Destroy(stepTemp.gameObject);
            }

            _faultSteps.Clear();

            foreach (var fault in faultData.Steps)
            {
                var stepTemp = Instantiate(_uiRef.FaultStepPrefab, _uiRef.FaultStepParent);
                stepTemp.SetFault(fault);
                _faultSteps.Add(stepTemp);
            }

            _uiRef.MessageObj.gameObject.SetActive(false);
            _uiRef.RepairedFaultSubmitBtn.transform.parent.gameObject.SetActive(true);
            _uiRef.RepairedFaultSubmitBtn.interactable = false;
            if (_uiRef.FaultResetBtn != null) _uiRef.FaultResetBtn.interactable = true;
            _tools.ForEach(x => x.ResetSelection(true));
            if (!isRestoring || _runningFaultProgress == null)
            {
                _runningFaultProgress = null;
                StepPerform = 0;
                _faultMistakes = 0;
                UpdateMistakeCounterUI();
                _wrongAttemptsPerStep = new List<int>();
                _stepsAttempt = new List<int>();

                for (var i = 0; i < _faultSteps.Count; i++)
                {
                    _wrongAttemptsPerStep.Add(0);
                    _stepsAttempt.Add(0);
                }

                if (_faultSteps.Count > 0)
                {
                    var step = _faultSteps[0];
                    if (_uiRef.SkipFaultStepBtn != null) _uiRef.SkipFaultStepBtn.interactable = step.IsSkippable();
                    if (_uiRef.BackStepBtn != null) _uiRef.BackStepBtn.interactable = false;
                    step.OnStepStart();
                    ScrollToStep(step);
                    _tool = _tools.Find(x => x._toolType == step.Step.RequireToolType);
                    if (step.IsSkippable() && _tool != null)
                    {
                        _tool.HighLightTool();
                        ScrollToTool(_tool);
                    }
                }

                HideRemovedControlButtons();
                EventManager.OnFaultStarted?.Invoke(faultIndex, _faultSteps.Count);
            }
            else
            {
                _faultMistakes = _runningFaultProgress.FaultMistakes;
                _wrongAttemptsPerStep = _runningFaultProgress.WrongAttemptsPerStep != null
                    ? new List<int>(_runningFaultProgress.WrongAttemptsPerStep)
                    : new List<int>();
                _stepsAttempt = _runningFaultProgress.StepsAttempt != null
                    ? new List<int>(_runningFaultProgress.StepsAttempt)
                    : new List<int>();
                while (_wrongAttemptsPerStep.Count < _faultSteps.Count) _wrongAttemptsPerStep.Add(0);
                while (_stepsAttempt.Count < _faultSteps.Count) _stepsAttempt.Add(0);
                UpdateMistakeCounterUI();
                if (_runningFaultProgress.StepStates != null)
                {
                    for (var i = 0; i < _faultSteps.Count && i < _runningFaultProgress.StepStates.Count; i++)
                    {
                        _faultSteps[i].RestoreVisualState(_runningFaultProgress.StepStates[i]);
                    }
                }

                int targetStepIndex = Mathf.Clamp(_runningFaultProgress.StepPerform, 0, _faultSteps.Count);
                StepPerform = targetStepIndex;

                // Sequentially restore and sample all completed steps' animations onto the 3D model
                // so that cumulative 3D model states are fully preserved
                if (_refManager != null && _refManager.FaultManager != null)
                {
                    _refManager.FaultManager.RestoreCumulativeAnimation(faultData.Steps, targetStepIndex);
                }

                if (StepPerform >= _faultSteps.Count)
                {
                    _uiRef.RepairedFaultSubmitBtn.interactable = true;
                    if (_uiRef.SkipFaultStepBtn != null) _uiRef.SkipFaultStepBtn.interactable = false;
                    if (_uiRef.BackStepBtn != null) _uiRef.BackStepBtn.interactable = false;
                    if (_uiRef.FaultResetBtn != null) _uiRef.FaultResetBtn.interactable = false;
                }
                else
                {
                    var step = _faultSteps[StepPerform];
                    if (_uiRef.SkipFaultStepBtn != null) _uiRef.SkipFaultStepBtn.interactable = step.IsSkippable();
                    if (_uiRef.BackStepBtn != null) _uiRef.BackStepBtn.interactable = StepPerform > 0;

                    step.OnStepStart();
                    ScrollToStep(step);

                    _tool = _tools.Find(x => x._toolType == step.Step.RequireToolType);
                    if (step.IsSkippable() && _tool != null)
                    {
                        _tool.HighLightTool();
                        ScrollToTool(_tool);
                    }
                }

                HideRemovedControlButtons();
                EventManager.OnFaultStarted?.Invoke(faultIndex, _faultSteps.Count);
                _runningFaultProgress = null;
            }
        }

        /// <summary>
        /// Handles wrong tool selection events, increments the attempt counter, and auto-skips the step if max attempts are exceeded.
        /// </summary>
        void WrongToolSelected()
        {
            EventManager.OnLogMessage?.Invoke("Wrong tool selected. Please select a right tool.", true);
            _faultMistakes++;
            UpdateMistakeCounterUI();
            if (StepPerform >= 0 && StepPerform < _wrongAttemptsPerStep.Count)
            {
                _wrongAttemptsPerStep[StepPerform]++;
                if (StepPerform < _stepsAttempt.Count)
                {
                    _stepsAttempt[StepPerform] = _faultMistakes;
                }

                if (_wrongAttemptsPerStep[StepPerform] >= maxAttempt)
                {
                    SkipCurrentStep();
                }
            }
        }

        /// <summary>
        /// Skips or rejects the current fault step, plays the step animation, and advances to the next step.
        /// </summary>
        void SkipCurrentStep(bool isSkipped = false)
        {
            if (StepPerform < 0 || StepPerform >= _faultSteps.Count) return;

            _tools.ForEach(x => x.ResetSelection(true));

            // Lock button during animation to prevent duplicate triggers
            if (_uiRef.SkipFaultStepBtn != null) _uiRef.SkipFaultStepBtn.interactable = false;
            if (_uiRef.BackStepBtn != null) _uiRef.BackStepBtn.interactable = false;
            var stepNumber = StepPerform + 1;
            var totalSteps = _faultSteps.Count;
            var isLastStep = (StepPerform + 1 >= _faultSteps.Count);
            if (isSkipped)
            {
                _faultSteps[StepPerform].OnStepSkip();
                if (_selectedTool)
                {
                    DestroyImmediate(_selectedTool.gameObject, true);
                    HandleToolOperation.Instance._tool = null;
                    _selectedTool = null;
                }
            }
            else
            {
                _faultSteps[StepPerform].OnStepReject();
            }

            var currentStep = _faultSteps[StepPerform];
            var animDuration = currentStep.Step.AnimationClip ? currentStep.Step.AnimationClip.length + 2f : 2f;

            RefManager.Instance.FaultManager.PlayStepAnimation(currentStep.Step, false);

            _faultAnimation = DOVirtual.DelayedCall(animDuration, () =>
            {
                _faultAnimation = null;
                StepPerform++;
                EventManager.OnStepAccomplished?.Invoke(_currentFaultIndex, stepNumber, totalSteps, isLastStep);
                CheckAllStepAreCompleted();
            });
        }

        /// <summary>
        /// Steps back to the preceding fault step, reversing animations and resetting step UI and tool states.
        /// </summary>
        void PreviousStep()
        {
            if (StepPerform <= 0 || StepPerform > _faultSteps.Count) return;

            // Lock UI buttons during back-step animation to prevent duplicate calls
            _uiRef.SkipFaultStepBtn.interactable = false;
            _uiRef.BackStepBtn.interactable = false;

            _tools.ForEach(x => x.ResetSelection(true));
            if (_selectedTool)
            {
                DestroyImmediate(_selectedTool.gameObject, true);
                HandleToolOperation.Instance._tool = null;
                _selectedTool = null;
            }

            // Reset visual state of current step if within bounds
            if (StepPerform < _faultSteps.Count)
            {
                _faultSteps[StepPerform].OnStepReject();
            }

            // Step back to previous step index
            StepPerform--;

            if (_stepsAttempt != null && _stepsAttempt.Count > 1)
            {
                _stepsAttempt.RemoveAt(_stepsAttempt.Count - 1);
            }

            var targetStep = _faultSteps[StepPerform];
            targetStep.OnStepReject();

            if (targetStep.Step != null && targetStep.Step.AnimationClip != null)
            {
                var animDuration = targetStep.Step.AnimationClip ? targetStep.Step.AnimationClip.length + 2f : 2f;
                RefManager.Instance.FaultManager.PlayStepAnimation(targetStep.Step, false, isReverse: true);

                if (_faultAnimation != null)
                {
                    _faultAnimation.Kill();
                    _faultAnimation = null;
                }

                _faultAnimation = DOVirtual.DelayedCall(animDuration, () =>
                {
                    _faultAnimation = null;
                    CheckAllStepAreCompleted();
                });
            }
            else
            {
                CheckAllStepAreCompleted();
            }
        }

        /// <summary>
        /// Converts the attempts history to byte array, invokes fault submission event, and resets the interface.
        /// </summary>
        void SubmitRepairedFault()
        {
            EventManager.SubmitFault?.Invoke(ByteConverter.ConvertListToBytes(_stepsAttempt));
            ResetUI();
            _runningFaultProgress = null;
            EventManager.OnFaultCompleted?.Invoke();
        }

        private void RequestResetFault()
        {
            if (_refManager != null && _refManager.FaultManager != null && _refManager.FaultManager.IsFaultStart)
            {
                _runningFaultProgress = null;
                ResetUI();
                EventManager.OnFaultCompleted?.Invoke();
            }
        }

        /// <summary>
        /// Resets the fault workflow UI, clears active animations, destroys spawned tools and step elements, and notifies fault completion.
        /// </summary>
        private void ResetUI()
        {
            if (_faultAnimation != null)
            {
                _faultAnimation.Kill();
                _faultAnimation = null;
            }

            if (_refManager != null && _refManager.FaultManager != null)
            {
                _refManager.FaultManager.StopActiveStepAnimation();
                _refManager.FaultManager.IsFaultStart = false;
            }

            _currentFaultIndex = -1;
            _faultMistakes = 0;
            UpdateMistakeCounterUI();
            _wrongAttemptsPerStep?.Clear();
            _stepsAttempt?.Clear();
            _uiRef.RepairedFaultSubmitBtn.transform.parent.gameObject.SetActive(false);
            if (_selectedTool)
            {
                DestroyImmediate(_selectedTool.gameObject, true);
                _selectedTool = null;
                HandleToolOperation.Instance._tool = null;
            }

            _uiRef.RepairedFaultSubmitBtn.interactable = false;
            if (_uiRef.SkipFaultStepBtn != null) _uiRef.SkipFaultStepBtn.interactable = true;
            if (_uiRef.FaultResetBtn != null) _uiRef.FaultResetBtn.interactable = true;
            if (_uiRef.BackStepBtn != null) _uiRef.BackStepBtn.interactable = false;
            HideRemovedControlButtons();

            _uiRef.FaultDisplayObj.gameObject.SetActive(false);
            _faultSteps.ForEach(b =>
            {
                b.OnStepReject();
                DestroyImmediate(b.gameObject);
            });
            _faultSteps.Clear();
            _faultTabs.ForEach(x => x.ResetGraphics());
            _uiRef.FaultDescription.text = string.Empty;
            _tools.ForEach(x => x.ResetSelection(true));
            SetToolsUI(false);
            SetModesUI(false);
        }

        private void UpdateMistakeCounterUI()
        {
            if (_uiRef == null) return;

            var faultMgr = _refManager != null ? _refManager.FaultManager : null;
            bool isFaultActive = _currentFaultIndex >= 0 || (faultMgr != null && faultMgr.IsFaultStart);
            string countText = $"Incorrect Actions: {_faultMistakes}";
            Color textColor = _faultMistakes > 0 ? new Color(0.85f, 0.0f, 0.0f, 1f) : new Color(0.2f, 0.2f, 0.2f, 1f);

            if (_uiRef.MistakeCounterText != null)
            {
                _uiRef.MistakeCounterText.text = countText;
                _uiRef.MistakeCounterText.color = textColor;
            }

            if (_uiRef.MistakeCounterBadge != null)
            {
                _uiRef.MistakeCounterBadge.SetActive(true);
            }
        }

        /// <summary>
        /// Displays a styled popup message with specified colors and automatically hides it after a 2-second delay.
        /// </summary>
        void DisaplayMessage(string message, Color textColor, Color bgColor, Color borderColor)
        {
            _uiRef.MessageText.text = message;
            _uiRef.MessageText.color = textColor;
            _uiRef.MessageObj.color = bgColor;
            _uiRef.MessageOutline.effectColor = borderColor;
            _uiRef.MessageObj.gameObject.SetActive(true);

            DOVirtual.DelayedCall(2f, () => _uiRef.MessageObj.gameObject.SetActive(false));
        }

        /// <summary>
        /// Displays step instructions in the primary fault prompt UI banner.
        /// </summary>
        private void DisaplayStepMessage(string obj)
        {
            _uiRef.FaultDisplayText.text = obj;
            _uiRef.FaultDisplayObj.gameObject.SetActive(true);
            _uiRef.FaultDisplayText.color = Color.blue;
        }

        /// <summary>
        /// Handles the video player end-of-playback event to dismiss the splash video panel.
        /// </summary>
        void VideoPlayeManager(VideoPlayer source)
        {
            if (_uiRef.VideoClips.Count > 1 && currentClipIndex == 0)
            {
                currentClipIndex++;
                _uiRef.VideoPlayer.clip = _uiRef.VideoClips[currentClipIndex];
                _uiRef.VideoPlayer.Play();
                return;
            }

            if (splashFinished)
                return;

            splashFinished = true;
            _uiRef.VideoPanel.gameObject.SetActive(false);
        }

        void ModelSwitch()
        {
            if (!RefManager.Instance.FaultManager.IsFaultStart || _currentFaultIndex < 0) return;
            _runningFaultProgress = null;
            var isAnimate = DirectModelRotatorAndScaler.IsLocked;
            int stepToSave = isAnimate ? StepPerform + 1 : StepPerform;
            var stepStates = _faultSteps != null
                ? _faultSteps.Select(step => step.CurrentVisualState).ToList()
                : new List<StepVisualState>();

            if (isAnimate && StepPerform >= 0 && StepPerform < stepStates.Count)
            {
                stepStates[StepPerform] =
                    GetCurrentFaultStep().CurrentVisualState is StepVisualState.InProgress or StepVisualState.NotStarted
                        ? StepVisualState.Completed
                        : stepStates[StepPerform];
            }

            _runningFaultProgress = new RunningFaultProgress()
            {
                CurrentFaultIndex = _currentFaultIndex,
                StepPerform = stepToSave,
                FaultMistakes = _faultMistakes,
                WrongAttemptsPerStep = _wrongAttemptsPerStep != null
                    ? new List<int>(_wrongAttemptsPerStep)
                    : new List<int>(),
                StepsAttempt = _stepsAttempt != null ? new List<int>(_stepsAttempt) : new List<int>(),
                StepStates = stepStates,
            };
        }
    }
}