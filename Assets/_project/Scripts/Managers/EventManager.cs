using System;
using System.Collections.Generic;
using CCEPL_CAD.Models;

namespace CCEPL_CAD
{
    public static class EventManager
    {
        public static Action StartApplication;
        public static Action<string> OnFaultsReceived;
        public static Action<ToolType, ToolObject> OnToolSelected;
        public static Action OnWrongToolSelected;
        public static Action<int> OnFaultClick;
        public static Action<Faults> OnFaultDetect;
        public static Action<string> OnSendData;

        public static Action OnStepComplete;
        public static Action OnFaultCompleted;
        public static Action<byte[]> SubmitFault;

        public static Action<List<float>> ReceivedUDPData;
        public static Action<GenericParameterGroup> ReceivedGenericParameters;
        public static Action ResetAnimation;
        public static Action<bool> OnCameraLockStateChanged;
        public static Action<UnityEngine.Vector3, UnityEngine.Vector3, float> MoveCameraToTransform;

        public static Action<ToolObject> Spawn3DToolObject;
        
        public static Action<string, bool> OnLogMessage;
        public static Action<string> TriggerDisplayText;
        
        // LabVIEW Telemetry & Remote Control
        public static Action<int, int, int, bool> OnStepAccomplished; // (faultCode, stepNumber, totalSteps, isFaultCompleted)
        public static Action<int, int, int> OnFaultFinished; // (faultCode, totalSteps, totalMistakes)
        public static Action<int, int> OnFaultStarted; // (faultCode, totalSteps)
        public static Action<int> RequestTriggerFault; // (faultCode)
        public static Action RequestResetFault;
        public static Action RequestSkipStep;
        public static Action RequestPreviousStep;
        public static Action ModelSwitch;
        
        //HVAC Specific
        public static Action<bool> RequestToggleCompressionHighlights; // (bool isVisible)
        public static Action<int, bool> RequestToggleCompressorHighlight; // (int compressorIndex 1..4, bool isVisible)
        public static Action<bool> RequestToggleShowHideObjects; // (bool isVisible)
        public static Action<bool> RequestToggleArrows; // (bool isVisible)
        public static Action<bool> OnCompressionHighlightsChanged; // (bool isVisible)
        public static Action<bool> OnArrowsChanged; // (bool isVisible)
        public static Action<string, bool> RequestToggleSensorHighlight; // ("FAT", "RAT", "SAT", "Smoke", bool isVisible)
        public static Action<string, bool> OnSensorHighlightChanged; // ("FAT", "RAT", "SAT", "Smoke", bool isVisible)
    }
}