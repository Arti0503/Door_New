using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using CCEPL_CAD;

namespace CCEPL_CAD.Models
{
    [Serializable]
    public class Faults
    {
        public List<Fault> FaultList;
    }

    [Serializable]
    public class StepProgressPayload
    {
        public string @event = "step_completed";
        public int fault_code;
        public int step_number;
        public int total_steps;
        public string status = "success";
        public bool is_fault_completed;
        public bool compression_highlights;
        public bool arrows_active;
    }

    [Serializable]
    public class FaultCompletedPayload
    {
        public string @event = "fault_completed";
        public int fault_code;
        public string status = "success";
        public int total_steps;
        public int total_mistakes;
        public bool is_fault_completed = true;
        public bool compression_highlights = true;
        public bool arrows_active = true;
    }

    [Serializable]
    public class FaultStartedPayload
    {
        public string @event = "fault_started";
        public int fault_code;
        public int total_steps;
        public string status = "started";
        public bool compression_highlights = false;
        public bool arrows_active = false;
    }

    [Serializable]
    public class FaultResetPayload
    {
        public string @event = "fault_reset";
        public string status = "idle";
        public bool compression_highlights = true;
        public bool arrows_active = true;
    }

    [Serializable]
    public class CompressionHighlightsPayload
    {
        public string @event = "compression_highlights_changed";
        public bool compression_highlights;
        public string status;
    }

    [Serializable]
    public class ArrowsPayload
    {
        public string @event = "arrows_changed";
        public bool arrows_active;
        public string status;
    }

    [Serializable]
    public class SensorHighlightPayload
    {
        public string @event = "sensor_highlight_changed";
        public string sensor;
        public bool is_visible;
        public string status;
    }

    [Serializable]
    public class TelemetryErrorPayload
    {
        public string @event = "error";
        public string message;
        public string status = "error";
    }

    public enum StepSendFormat
    {
        JSON,
        RawNumber,
        Both
    }

    [Serializable]
    public class Fault
    {
        public int FaultIndex;
        public string Message;
        public List<Step> Steps;
        public bool IsCompleted = false;
    }

    [Serializable]
    public class Step
    {
        public string Message;
        public ToolType RequireToolType;
        public string ComponentName;
        public string ComponentCode;
        public List<IntrectableComponentPart> Targets;
        public AnimationClip AnimationClip;
        [Range(-1,1)]
        public int AnimationSpeed;
        public bool IsCompleted = false;

        [Header("Camera Focus Settings")]
        public bool UseCustomCameraTransform;
        public Vector3 TargetCameraPosition;
        public Vector3 TargetCameraRotation;

        public bool IsDisplayeText = false;
        public string DisaplayMessage = string.Empty;

        /// <summary>
        /// Creates a deep clone of this step, duplicating property values and interactive target references.
        /// </summary>
        public Step Clone()
        {
            return new Step
            {
                Message = this.Message,
                RequireToolType = this.RequireToolType,
                ComponentName = this.ComponentName,
                ComponentCode = this.ComponentCode,
                Targets = this.Targets != null ? new List<IntrectableComponentPart>(this.Targets) : new List<IntrectableComponentPart>(),
                AnimationClip = this.AnimationClip,
                AnimationSpeed = this.AnimationSpeed,
                IsCompleted = this.IsCompleted,
                UseCustomCameraTransform = this.UseCustomCameraTransform,
                TargetCameraPosition = this.TargetCameraPosition,
                TargetCameraRotation = this.TargetCameraRotation,
                IsDisplayeText = this.IsDisplayeText,
                DisaplayMessage = this.DisaplayMessage
            };
        }

        /// <summary>
        /// Validates step configuration against rules, checking target assignments, tool registration, messages, and animation parameters.
        /// </summary>
        public List<StepValidationError> ValidateStep(Dictionary<ToolType, RegisteredToolInfo> registeredToolMap = null)
        {
            var errors = new List<StepValidationError>();
            bool isVisualStep = FaultManager.IsVisualInspectionStep(this);

            if (Targets != null && Targets.Count > 0)
            {
                for (int i = 0; i < Targets.Count; i++)
                {
                    if (Targets[i] == null)
                    {
                        errors.Add(new StepValidationError(
                            true,
                            $"Target list size is {Targets.Count}, but slot #{i} is UNASSIGNED (null)!"
                        ));
                    }
                }
            }
            else if (!isVisualStep)
            {
                // Interactive tool steps REQUIRE target objects
                errors.Add(new StepValidationError(
                    true,
                    $"Requires tool '{RequireToolType}', but no Target objects are assigned!"
                ));
            }

            if (!isVisualStep && registeredToolMap != null)
            {
                if (!registeredToolMap.TryGetValue(RequireToolType, out RegisteredToolInfo info) || !info.IsPresent)
                {
                    errors.Add(new StepValidationError(
                        true,
                        $"Required Tool '{RequireToolType}' is NOT present in ToolsDataSO ScriptableObject!"
                    ));
                }
                else if (!info.HasToolObject)
                {
                    errors.Add(new StepValidationError(
                        true,
                        $"Required Tool '{RequireToolType}' in ToolsDataSO has NO 'ToolObject' assigned (null)!"
                    ));
                }
            }

            if (string.IsNullOrWhiteSpace(Message))
            {
                errors.Add(new StepValidationError(false, "Step description message is empty."));
            }
            
            if (AnimationClip != null && AnimationSpeed == 0)
            {
                errors.Add(new StepValidationError(
                    true,
                    "An AnimationClip is assigned, but AnimationSpeed is set to 0!"
                ));
            }
            
            if(IsDisplayeText && string.IsNullOrWhiteSpace(DisaplayMessage))
            {
                errors.Add(new StepValidationError(
                    true,
                    "IsDisplayeText is enabled, but DisaplayMessage is empty!"
                ));
            }

            return errors;
        }
    }

    [Serializable]
    public struct RegisteredToolInfo
    {
        public ToolType ToolType;
        public bool IsPresent;
        public bool HasToolObject;
        public bool HasToolIcon;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegisteredToolInfo"/> struct with registration details.
        /// </summary>
        public RegisteredToolInfo(ToolType toolType, bool isPresent, bool hasToolObject, bool hasToolIcon)
        {
            ToolType = toolType;
            IsPresent = isPresent;
            HasToolObject = hasToolObject;
            HasToolIcon = hasToolIcon;
        }
    }

    [Serializable]
    public struct StepValidationError
    {
        public bool IsError;
        public string Message;

        /// <summary>
        /// Initializes a new instance of the <see cref="StepValidationError"/> struct.
        /// </summary>
        public StepValidationError(bool isError, string message)
        {
            IsError = isError;
            Message = message;
        }
    }

    [Serializable]
    public class ToolData
    {
        public string ToolName;
        public Sprite ToolIcon;
        public ToolType ToolType;
        public ToolObject ToolObject;
    }

    [Serializable]
    public class ComponentData
    {
        public string ComponentName;
        public Sprite ComponentIcon;
        public string ComponentCode;
    }

    [Serializable]
    public class FaultTabData
    {
        public string Fault;
    }

    [Serializable]
    public class ToolsData
    {
        public ToolType ToolType;
        public Sprite ToolIcon;
        public ToolObject ToolObject;
    }

    public enum ToolType
    {
        None = -1,
        SquareKey7X7,
        AirGun,
        AirLineCaps,
        AlignmentGauge,
        AllenKeySet,
        AllenKeys,
        AntiFrettingCompound,
        BearingPuller,
        BearingPullerSet,
        BearingTools,
        BearingWithdrawalPlate,
        BlankingCaps,
        Blower,
        BogieFrameLiftingCrane,
        Bracket,
        BrakeAdjustmentWrench,
        BrassDrift,
        BristleBrush,
        BristleBrushOrCloth,
        Bristles,
        CalibratedThermometer,
        CarbodyLiftingCraneOrJacks,
        ClampThermometer,
        CleaningSolvent,
        Cloth,
        CompressedAir,
        CompressedAirBlowGun,
        CompressedAirGun,
        DimensionalInspection,
        DriftPunch,
        EarObservation,
        ElectronicLeakDetector,
        FeelerGauge,
        FinComb,
        FlashLight,
        GreaseApplicator,
        GreaseGun,
        GuideRope,
        Hammer,
        HandTool,
        HighPressureAirHose,
        HoldingFixture,
        HotWaterSoftensTheBristles,
        HydraulicJack,
        HydraulicPress,
        HydraulicPuller,
        HydraulicWheelPress,
        InspectionLamp,
        InspectionMirror,
        InsulatedHandGloves,
        InsulatedElectricalHandGloves,
        InsulatedGloves,
        InsulatedSafetyGloves,
        Lifting,
        LiftingBeam,
        LiftingChain,
        LiftingDevice,
        LiftingSling,
        LintFreeCloth,
        ListeningCheck,
        ManifoldGaugeSet,
        ManualPullCheck,
        MeasurementGauge,
        Multimeter,
        NozzleGun,
        OpenEndSpannerSet,
        OverheadCrane,
        PaperCloth,
        PinPunch,
        PipeWrench,
        Pliers,
        Plugs,
        PneumaticLeakDetectionSpray,
        PortableFixtures,
        PositioningTools,
        PryBar,
        PryBar_IfRequired,
        PTE_PressureTestEquipment,
        Puller,
        Punch,
        RatchetWrench,
        SafetyGloves,
        SafetyStands,
        Screwdriver,
        ScrewdriverSet,
        Slings,
        Socket,
        SocketSet,
        SocketWrench,
        SocketWrenchSet,
        Sockets,
        SoftBristleBrus,
        SoftBristleBrush,
        SoftNonMetallicBristledBrush,
        SoftFaceHammer,
        Spanner,
        SpannerKit,
        SpannerSet,
        SpringCompressor,
        StandardWrench,
        StandardWrenches,
        StayRod,
        StraightEdge,
        SupportStand,
        TaggingLabels,
        Tester,
        Thermometer,
        ThermometerMultimeter,
        ToolKit,
        TorqueWrench,
        Vacuum,
        VacuumCleaner,
        VacuumCleanerWithBrushNozzle,
        Visual,
        VisualCheck,
        VisualCheckInThroghtSightGlass,
        VisualInspection,
        VisualInspectionLamp,
        WetCloth,
        WheelPuller,
        WireCutter,
        LongNosePlier,
        StepLadder,
        ServiceKey,
        SharpKnife
    }

    public enum FaultStage
    {
        Start,
        ChooseTheTool,
        ChooseTheComponent
    }

    public enum ApplicationMode
    {
        TestMode,
        ExamMode
    }

    [Serializable]
    public class InteractableComponentData
    {
        public string ComponentName;
        public string ComponentCode;
    }
    
    [Serializable]
    public class AnimationLayerWaight
    {
        public string LayerName;
        public List<int> LayerIndexs;
    }
    
    [Serializable]
    public class AnimationColorChange
    {
        public string AnimationName;
        public Material AnimationMaterial;
        public Color Color1;
        public Color Color2;
        public int DefaultValue;
    }

    [Serializable]
    public class BlendShapeChange
    {
        public string BlendShapeName;
        public List<int> BlendShapeIndexs;
        public SkinnedMeshRenderer TargetMesh;
    }

    [Serializable]
    public class SpeedSliderData
    {
        public string ParameterName;
        public Slider SliderComponent;
    }
    
    [Serializable]
    public class AmplitudeSliderData
    {
        public string LayerName;
        public List<int> LayerIndex;
        public Slider SliderComponent;
    }
    
    
    [Serializable]
    public class AnimationConfigData
    {
        public SystemSettings Bogie;
        public SystemSettings Hvac;
        public SystemSettings Door;
    }

    public class FlexibleBoolConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(bool) || objectType == typeof(bool?);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Boolean)
            {
                return (bool)reader.Value;
            }
            if (reader.TokenType == JsonToken.Integer || reader.TokenType == JsonToken.Float)
            {
                return Convert.ToInt64(reader.Value) != 0;
            }
            if (reader.TokenType == JsonToken.String)
            {
                string text = (string)reader.Value;
                if (bool.TryParse(text, out bool b)) return b;
                if (int.TryParse(text, out int i)) return i != 0;
            }
            return false;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }

    public class FlexibleFloatConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(float) || objectType == typeof(float?);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Integer)
            {
                return Convert.ToSingle(reader.Value);
            }
            if (reader.TokenType == JsonToken.String)
            {
                string text = (string)reader.Value;
                if (float.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float f))
                {
                    return f;
                }
            }
            return 0f;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }
    
    [Serializable]
    public class GenericParameter
    {
        public string Name;
        public string Label;
        public string Unit;
        public string DataType;
        [JsonConverter(typeof(FlexibleFloatConverter))]
        public float Value;
        public string Remark;

        public int AsInt => Mathf.RoundToInt(Value);
        public bool AsBool => Value > 0.5f;
        public float AsFloat => Value;
    }
    
    [Serializable]
    public class GenericParameterGroup
    {
        public List<GenericParameter> Parameters;
    }

    [Serializable]
    public class SystemSettings
    {
        public List<NumericParameter> Speed;
        public List<NumericParameter> Amplitude;
        public List<BooleanParameter> Boolean;
    }

    [Serializable]
    public class NumericParameter
    {
        public string Name;
        [JsonConverter(typeof(FlexibleFloatConverter))]
        public float Value;
    }

    [Serializable]
    public class BooleanParameter
    {
        public string Name;
        [JsonConverter(typeof(FlexibleBoolConverter))]
        public bool Value;
    }

    [Serializable]
    public enum Modes
    {
        None,
        Easy,
        Normal
    }
    
    [Serializable]
    public class ColorSliderData
    {
        public string AnimationName;
        public Material TargetMaterial;
        public Color StartColor;
        public Color EndColor;
        public Slider SliderComponent;
        public int DefaultValue;
    }

    [Serializable]
    public class BlendShapeChangeData
    {
        public string BlendShapeName;
        public List<int> BlendShapeIndexs;
        public SkinnedMeshRenderer TargetMesh;
        public Slider SliderComponent;
    }

    /// <summary>
    /// Preserves the execution state and visual progress of a fault workflow for a specific 3D model.
    /// Allows resuming from the exact step and mistake count when switching back between models.
    /// </summary>
    [Serializable]
    public class RunningFaultProgress
    {
        public int CurrentFaultIndex = -1;
        public int StepPerform = -1;
        public int FaultMistakes = 0;
        public List<int> StepsAttempt = new List<int>();
        public List<int> WrongAttemptsPerStep = new List<int>();
        public List<StepVisualState> StepStates = new List<StepVisualState>();
    }
}