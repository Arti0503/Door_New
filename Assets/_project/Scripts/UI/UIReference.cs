using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UIOutline = UnityEngine.UI.Outline;

namespace CCEPL_CAD
{
    public class UIReference : MonoBehaviour
    {
        [Header("Prefab References")]
        public FaultTab FaultTabPrefab;
        public Tool ToolPrefab;
        public ObjComponent ComponentPrefab;
        public FaultStep FaultStepPrefab;

        [Space(5), Header("Application Mode References")]
        public Toggle TestModeToggle;
        public TMP_Text TestModeToggleText;
        public Sprite TestModeBGSprite;
        public Toggle ExamModeToggle;
        public TMP_Text ExamModeToggleText;
        public Sprite ExamModeBGSprite;
        public Image ModeBGImage;
        public List<GameObject> HiddenObjectForExamMode;

        [Space(5), Header("Parent Transfrom References")]
        public RectTransform FaultTabParent;
        public RectTransform ToolParent;
        public RectTransform ComponentParent;
        public RectTransform FaultStepParent;

        [Space(5), Header("Button References")]
        public Button ToolsBtn;
        public Button ToolsBackBtn;
        public Button ModesBtn;
        public Button ModesBackBtn;
        public Button RepairedFaultSubmitBtn;
        public Button SkipFaultStepBtn;
        public Button BackStepBtn;
        public Button FaultResetBtn;

        [Space(5), Header("UI Panel References")]
        public RectTransform ToolPanel;
        public RectTransform ComponentPanel;
        public RectTransform FaultPanel;
        
        [Space(5), Header("Display Message References")]
        public TMP_Text MessageText;
        public Image MessageObj;
        public UIOutline MessageOutline;
        public Color SuccessMessageTextColor;
        public Color SuccessMessageBGColor;
        public Color SuccessMessageOutlineColor;
        public Color FailureMessageTextColor;
        public Color FailureMessageBGColor;
        public Color FailureMessageOutlineColor;
        
        [Space(5), Header("Display fault text")]
        public TMP_Text FaultDisplayText;
        public Image FaultDisplayObj;

        [Space(5), Header("Other UI References")]
        public TMP_Text FaultDescription;
        
        [Space(5), Header("Mistake Counter References")]
        public TMP_Text MistakeCounterText;
        public GameObject MistakeCounterBadge;

        [Space(5), Header("Video Panel References")]
        public RectTransform VideoPanel;
        public VideoPlayer VideoPlayer;
        public List<VideoClip> VideoClips;
    }
}