using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CCEPL_CAD.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CCEPL_CAD
{
    public class UDPManager : MonoBehaviour
    {
        [Header("UDP Settings")]
        [SerializeField] private string _remoteIP = "127.0.0.1";
        [SerializeField] private int _listenPort = 5005;
        [SerializeField] private int _remotePort = 5006;
        [SerializeField] private bool _autoUpdateRemoteIPFromSender = true;

        [Header("Step Telemetry Settings (to LabVIEW)")]
        [SerializeField] internal StepSendFormat _stepSendFormat = StepSendFormat.JSON;
        [SerializeField] private bool _sendFaultStartedAck = true;

        private UdpClient _receiver;
        private UdpClient _sender;
        private Thread _receiveThread;
        private volatile bool _isRunning;

        private readonly ConcurrentQueue<byte[]> _messageQueue = new();
        private readonly object _endpointLock = new();
        private IPEndPoint _remoteEndPoint;
        
        private string _lastReceivedMessage = string.Empty;
        private readonly List<float> _cachedFloatBuffer = new(64);

        #region Unity Lifecycle

        /// <summary>
        /// Enables background execution to keep network sockets active when the application is unfocused.
        /// </summary>
        private void Awake()
        {
            Application.runInBackground = true;
        }

        /// <summary>
        /// Subscribes to the fault submission event to handle outgoing UDP fault transmissions.
        /// </summary>
        private void OnEnable()
        {
            EventManager.SubmitFault += SubmitFault;
            EventManager.OnStepAccomplished += HandleStepAccomplished;
            EventManager.OnFaultFinished += HandleFaultFinished;
            EventManager.OnFaultStarted += HandleFaultStarted;
            EventManager.ResetAnimation += HandleReset;
            EventManager.RequestResetFault += HandleReset;
        }

        /// <summary>
        /// Unsubscribes from the fault submission event when the component is disabled.
        /// </summary>
        private void OnDisable()
        {
            EventManager.SubmitFault -= SubmitFault;
            EventManager.OnStepAccomplished -= HandleStepAccomplished;
            EventManager.OnFaultFinished -= HandleFaultFinished;
            EventManager.OnFaultStarted -= HandleFaultStarted;
            EventManager.ResetAnimation -= HandleReset;
            EventManager.RequestResetFault -= HandleReset;
        }

        /// <summary>
        /// Starts the UDP sender and receiver networking threads upon game start.
        /// </summary>
        private void Start()
        {
            StartUDP();
        }

        /// <summary>
        /// Dequeues and decodes pending UDP packets on the Unity main thread each frame.
        /// </summary>
        private void Update()
        {
            ProcessMessages();
        }

        /// <summary>
        /// Cleans up and stops UDP networking sockets and worker threads when the application quits.
        /// </summary>
        private void OnApplicationQuit()
        {
            StopUDP();
        }

        #endregion

        #region UDP Lifecycle

        /// <summary>
        /// Initializes UDP client sockets and starts the asynchronous background thread for listening to incoming packets.
        /// </summary>
        public void StartUDP()
        {
            if (_isRunning) return;

            try
            {
                _remoteEndPoint = new IPEndPoint(IPAddress.Parse(_remoteIP), _remotePort);

                // Receiver Setup
                _receiver = new UdpClient(_listenPort);
                _receiver.Client.ReceiveTimeout = 1000;

                // Sender Setup
                _sender = new UdpClient();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                try
                {
                    const int SIO_UDP_CONNRESET = -1744830452; // 0x9800000C
                    _sender.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, new byte[] { 0 });
                }
                catch { }
#endif

                _isRunning = true;
                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "UDP_Receive_Thread"
                };
                _receiveThread.Start();

                Debug.Log($"[UDPManager] Started. Listening: {_listenPort} | Target: {_remoteIP}:{_remotePort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UDPManager] Start Failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops the background receive thread and closes both sender and receiver UDP clients.
        /// </summary>
        public void StopUDP()
        {
            if (!_isRunning) return;

            _isRunning = false;

            try
            {
                _receiver?.Close();
                _sender?.Close();

                if (_receiveThread != null && _receiveThread.IsAlive)
                {
                    _receiveThread.Join(200);
                }

                Debug.Log("[UDPManager] Stopped.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UDPManager] Stop Error: {ex.Message}");
            }
            finally
            {
                _receiver = null;
                _sender = null;
                _receiveThread = null;
            }
        }

        #endregion

        #region Network Loop & Dispatch

        /// <summary>
        /// Continuously listens for incoming UDP datagrams on a background thread and queues them for main thread processing.
        /// </summary>
        private void ReceiveLoop()
        {
            IPEndPoint senderEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (_isRunning)
            {
                try
                {
                    byte[] data = _receiver.Receive(ref senderEndPoint);

                    if (data != null && data.Length > 0)
                    {
                        if (_autoUpdateRemoteIPFromSender && senderEndPoint.Address != null)
                        {
                            lock (_endpointLock)
                            {
                                var addr = senderEndPoint.Address;
                                if (addr.IsIPv4MappedToIPv6)
                                {
                                    addr = addr.MapToIPv4();
                                }
                                if (!_remoteEndPoint.Address.Equals(addr))
                                {
                                    _remoteEndPoint = new IPEndPoint(addr, _remotePort);
                                }
                            }
                        }

                        _messageQueue.Enqueue(data);
                    }
                }
                catch (SocketException)
                {
                    // Expected on socket timeout or close/shutdown
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Debug.LogError($"[UDPManager] Receive Thread Error: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Dequeues and passes all queued incoming packets to the packet decoder.
        /// </summary>
        private void ProcessMessages()
        {
            while (_messageQueue.TryDequeue(out byte[] data))
            {
                DecodePacket(data);
            }
        }

        #endregion

        #region Send

        /// <summary>
        /// Encodes fault byte array data into a comma-delimited string and transmits it over UDP.
        /// </summary>
        private void SubmitFault(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            SendString(string.Join(",", data));
        }

        /// <summary>
        /// Sends an ASCII encoded string message to the configured remote UDP endpoint.
        /// </summary>
        public void SendString(string message)
        {
            if (string.IsNullOrEmpty(message) || _sender == null) return;

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);

                lock (_endpointLock)
                {
                    if (_remoteEndPoint != null)
                    {
                        _sender.Send(data, data.Length, _remoteEndPoint);
                    }
                }

                Debug.Log($"[UDPManager] SENT -> {message}");
            }
            catch (SocketException sex)
            {
                if (sex.ErrorCode != 10054)
                {
                    Debug.LogWarning($"[UDPManager] Socket warning ({sex.ErrorCode}): {sex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UDPManager] Send Failed: {ex.Message}");
            }
        }

        #endregion

        #region Decoding

        /// <summary>
        /// Decodes incoming UDP raw byte packets to string format and routes them to JSON or CSV parsers.
        /// </summary>
        private void DecodePacket(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            try
            {
                string message = Encoding.UTF8.GetString(data).Trim();

                if (string.IsNullOrEmpty(message) || message == _lastReceivedMessage)
                    return;

                _lastReceivedMessage = message;
                Debug.Log($"[UDPManager] Received -> {message}");

                if (message[0] == '{' || message[0] == '[')
                {
                    DecodeJsonPacket(message);
                }
                else
                {
                    DecodeNonJsonPacket(message);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UDPManager] Decode Error: {ex.Message}");
            }
        }

        private int _lastFaultCommandAction = 0;
        private int _lastFaultCodeTrigger = 0;
		
        /// <summary>
        /// Parses JSON formatted subsystem configuration data, extracts speeds, amplitudes, and booleans, and broadcasts them via events.
        /// </summary>
        private void DecodeJsonPacket(string jsonString)
        {
            try
            {
                EventManager.OnFaultsReceived?.Invoke(jsonString);

                JToken token = JToken.Parse(jsonString);
                if (token is not JObject parsedObject) return;

                // 1. Check for Command & Fault Triggering (including Hvac.Fault array)
                bool hasCommand = false;//TryProcessJsonCommand(parsedObject);

                // Check if this payload also contains continuous animation / settings data
                bool hasSystemSettings = parsedObject.ContainsKey("Hvac") ||
                                         parsedObject.ContainsKey("Bogie") ||
                                         parsedObject.ContainsKey("Door") ||
                                         parsedObject.ContainsKey("Speed") ||
                                         parsedObject.ContainsKey("Amplitude") ||
                                         parsedObject.ContainsKey("Boolean") ||
                                         parsedObject.ContainsKey("Parameters");

                // If it was purely a command/fault packet without any settings, return early
                if (hasCommand && !hasSystemSettings)
                {
                    return;
                }

                // // 2. Fallback / Process SystemSettings for Slider/Animation Parameters (Speed, Amplitude, Boolean)
                // SystemSettings targetSettings = null;
                //
                // // Check for Root AnimationConfigData structure (case-insensitive)
                // if (parsedObject.TryGetValue("Hvac", StringComparison.OrdinalIgnoreCase, out JToken hvacToken))
                // {
                //     targetSettings = hvacToken.ToObject<SystemSettings>();
                // }
                // else if (parsedObject.TryGetValue("Bogie", StringComparison.OrdinalIgnoreCase, out JToken bogieToken))
                // {
                //     targetSettings = bogieToken.ToObject<SystemSettings>();
                // }
                // else if (parsedObject.TryGetValue("Door", StringComparison.OrdinalIgnoreCase, out JToken doorToken))
                // {
                //     targetSettings = doorToken.ToObject<SystemSettings>();
                // }
                // else
                // {
                //     // Fallback to direct SystemSettings payload
                //     targetSettings = parsedObject.ToObject<SystemSettings>();
                // }
                //
                // if (targetSettings == null) return;
                //
                // _cachedFloatBuffer.Clear();
                //
                // ExtractNumericValues(targetSettings.Speed, _cachedFloatBuffer);
                // ExtractNumericValues(targetSettings.Amplitude, _cachedFloatBuffer);
                //
                // if (targetSettings.Boolean != null)
                // {
                //     for (int i = 0; i < targetSettings.Boolean.Count; i++)
                //     {
                //         var bParam = targetSettings.Boolean[i];
                //         _cachedFloatBuffer.Add(bParam.Value ? 1.0f : 0.0f);
                //     }
                // }
                //
                // if (_cachedFloatBuffer.Count > 0)
                // {
                //     // Send out a copy to preserve isolation for event listeners
                //     EventManager.ReceivedUDPData?.Invoke(new List<float>(_cachedFloatBuffer));
                //     SendString(string.Join(",", _cachedFloatBuffer));
                // }
                
                GenericParameterGroup targetParameters = null;
                
                // Check for Root AnimationConfigData structure (case-insensitive)
                if (parsedObject.TryGetValue("Hvac", StringComparison.OrdinalIgnoreCase, out JToken hvacToken))
                {
                    targetParameters = hvacToken.ToObject<GenericParameterGroup>();
                }
                else if (parsedObject.TryGetValue("Bogie", StringComparison.OrdinalIgnoreCase, out JToken bogieToken))
                {
                    targetParameters = bogieToken.ToObject<GenericParameterGroup>();
                }
                else if (parsedObject.TryGetValue("Door", StringComparison.OrdinalIgnoreCase, out JToken doorToken))
                {
                    targetParameters = doorToken.ToObject<GenericParameterGroup>();
                }
                else
                {
                    // Fallback to direct SystemSettings payload
                    targetParameters = parsedObject.ToObject<GenericParameterGroup>();
                }

                if (targetParameters == null) return;
                EventManager.ReceivedGenericParameters?.Invoke(targetParameters);
                SendString(JsonConvert.SerializeObject(targetParameters));
                
                
                var faultParameters = targetParameters.Parameters.FindAll(p => p.Unit is "Code" or "Command");

                var faultCommand = faultParameters.Find(p => p.Unit.Equals("Command", StringComparison.OrdinalIgnoreCase)).AsInt;
                var faultCode = faultParameters.Find(p => p.Name.Equals("Fault_Code_Trigger", StringComparison.OrdinalIgnoreCase)).AsInt;
                var faultCause=  faultParameters.Find(p => p.Name.Equals("Fault_Cause_Trigger", StringComparison.OrdinalIgnoreCase)).AsInt;
                var faultSkip= faultParameters.Find(p => p.Name.Equals("Fault_Skip_Trigger", StringComparison.OrdinalIgnoreCase)).AsInt;
                
                // Command Action: 0 = Idle, 1 = Trigger, 2 = Reset
                if (faultCommand == 1) // Trigger Fault
                {
                    if (faultCode > 0)
                    {
                        if (_lastFaultCommandAction != 1 || _lastFaultCodeTrigger != faultCode)
                        {
                            _lastFaultCommandAction = 1;
                            _lastFaultCodeTrigger = faultCode;
                            Debug.Log($"[UDPManager] LabVIEW Fault Trigger Received -> Fault Code: {faultCode} (Cause: {faultCause})");
                            EventManager.RequestTriggerFault?.Invoke(faultCode);
                        }
                    }
                }
                else if (faultCommand == 2) // Reset Fault
                {
                    if (_lastFaultCommandAction != 2)
                    {
                        _lastFaultCommandAction = 2;
                        _lastFaultCodeTrigger = 0;
                        Debug.Log("[UDPManager] LabVIEW Fault Reset Received -> Executing Reset");
                        EventManager.RequestResetFault?.Invoke();
                        var resetAck = new FaultResetPayload();
                        SendString(JsonConvert.SerializeObject(resetAck, Formatting.None));
                    }
                }
                else if (faultCommand == 0) // Idle
                {
                    _lastFaultCommandAction = 0;
                    if (faultCode == 0)
                    {
                        _lastFaultCodeTrigger = 0;
                    }
                }
                
                if(faultSkip == 1)
                {
                    Debug.Log("[UDPManager] LabVIEW Fault Skip Step Received -> Executing Skip");
                    EventManager.RequestSkipStep?.Invoke();
                }
                
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UDPManager] JSON Parse Error: {ex.Message}");
            }
        }

        private bool TryProcessJsonCommand(JObject obj)
        {
            // A. Check for nested or direct Fault array: "Hvac.Fault" or top-level "Fault"
            if (TryProcessFaultArray(obj))
            {
                return true;
            }

            // B. Check for explicit "command" field
            if (obj.TryGetValue("command", StringComparison.OrdinalIgnoreCase, out JToken cmdToken))
            {
                string cmd = cmdToken.ToString().ToLowerInvariant();
                if (cmd == "trigger_fault" || cmd == "start_fault" || cmd == "trigger" || cmd == "fault")
                {
                    if (TryExtractFaultCode(obj, out int fCode))
                    {
                        Debug.Log($"[UDPManager] Triggering Fault from JSON command: {fCode}");
                        EventManager.RequestTriggerFault?.Invoke(fCode);
                        return true;
                    }
                }
                else if (cmd == "reset" || cmd == "reset_fault" || cmd == "stop")
                {
                    Debug.Log("[UDPManager] Resetting Fault from JSON command");
                    HandleReset();
                    EventManager.RequestResetFault?.Invoke();
                    var resetAck = new FaultResetPayload();
                    SendString(JsonConvert.SerializeObject(resetAck, Formatting.None));
                    return true;
                }
                else if (cmd == "skip" || cmd == "skip_step" || cmd == "next" || cmd == "next_step")
                {
                    Debug.Log("[UDPManager] Skip Step from JSON command");
                    EventManager.RequestSkipStep?.Invoke();
                    return true;
                }
                // else if (cmd == "set_compression_highlights" || cmd == "compression_highlights" || cmd == "set_highlights" || cmd == "toggle_compression_highlights" || cmd == "compression")
                // {
                //     bool state = true;
                //     if (obj.TryGetValue("state", StringComparison.OrdinalIgnoreCase, out JToken stateToken))
                //     {
                //         state = ExtractBooleanFromToken(stateToken);
                //     }
                //     else if (obj.TryGetValue("value", StringComparison.OrdinalIgnoreCase, out JToken valToken))
                //     {
                //         state = ExtractBooleanFromToken(valToken);
                //     }
                //     Debug.Log($"[UDPManager] Compression Highlights Command -> {state}");
                //     EventManager.RequestToggleCompressionHighlights?.Invoke(state);
                //     return true;
                // }
                // else if (cmd == "set_arrows" || cmd == "arrows" || cmd == "set_arrows_active" || cmd == "toggle_arrows")
                // {
                //     bool state = true;
                //     if (obj.TryGetValue("state", StringComparison.OrdinalIgnoreCase, out JToken stateToken))
                //     {
                //         state = ExtractBooleanFromToken(stateToken);
                //     }
                //     else if (obj.TryGetValue("value", StringComparison.OrdinalIgnoreCase, out JToken valToken))
                //     {
                //         state = ExtractBooleanFromToken(valToken);
                //     }
                //     Debug.Log($"[UDPManager] Arrows Command -> {state}");
                //     EventManager.RequestToggleArrows?.Invoke(state);
                //     return true;
                // }
            }

            // C. Check for direct fault code property: { "fault": 101 } or { "fault_code": 101 }
            if (TryExtractFaultCode(obj, out int directFaultCode))
            {
                Debug.Log($"[UDPManager] Triggering Fault from direct JSON property: {directFaultCode}");
                EventManager.RequestTriggerFault?.Invoke(directFaultCode);
                return true;
            }

            // // D. Check for direct compression highlights property: { "compression_highlights": true/false }
            // string[] compKeys = { "compression_highlights", "compression_highlight", "compresser_highlight", "compressor_highlight", "compressor_highlights" };
            // foreach (var key in compKeys)
            // {
            //     if (obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken cToken))
            //     {
            //         bool state = ExtractBooleanFromToken(cToken);
            //         Debug.Log($"[UDPManager] Direct Compression Highlights Property -> {state}");
            //         EventManager.RequestToggleCompressionHighlights?.Invoke(state);
            //         return true;
            //     }
            // }
            //
            // // E. Check for direct arrows property: { "arrows": true/false }
            // string[] arrowKeys = { "arrows", "arrows_active", "arrow_active", "idle_arrows" };
            // foreach (var key in arrowKeys)
            // {
            //     if (obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken aToken))
            //     {
            //         bool state = ExtractBooleanFromToken(aToken);
            //         Debug.Log($"[UDPManager] Direct Arrows Property -> {state}");
            //         EventManager.RequestToggleArrows?.Invoke(state);
            //         return true;
            //     }
            // }

            // F. Check for nested { "Hvac": { ... } } properties
            if (obj.TryGetValue("Hvac", StringComparison.OrdinalIgnoreCase, out JToken hvacToken) && hvacToken is JObject hvacObj)
            {
                if (TryExtractFaultCode(hvacObj, out int nestedCode))
                {
                    Debug.Log($"[UDPManager] Triggering Fault from nested Hvac JSON: {nestedCode}");
                    EventManager.RequestTriggerFault?.Invoke(nestedCode);
                    return true;
                }

                // foreach (var key in compKeys)
                // {
                //     if (hvacObj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken cToken))
                //     {
                //         bool state = ExtractBooleanFromToken(cToken);
                //         Debug.Log($"[UDPManager] Nested Hvac Compression Highlights -> {state}");
                //         EventManager.RequestToggleCompressionHighlights?.Invoke(state);
                //         return true;
                //     }
                // }
                //
                // foreach (var key in arrowKeys)
                // {
                //     if (hvacObj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken aToken))
                //     {
                //         bool state = ExtractBooleanFromToken(aToken);
                //         Debug.Log($"[UDPManager] Nested Hvac Arrows -> {state}");
                //         EventManager.RequestToggleArrows?.Invoke(state);
                //         return true;
                //     }
                // }
            }

            return false;
        }

        private bool TryProcessFaultArray(JObject obj)
        {
            JArray faultArray = null;

            if (obj.TryGetValue("Fault", StringComparison.OrdinalIgnoreCase, out JToken directFaultToken) && directFaultToken is JArray directArray)
            {
                faultArray = directArray;
            }
            else if (obj.TryGetValue("Hvac", StringComparison.OrdinalIgnoreCase, out JToken hvacToken) && hvacToken is JObject hvacObj)
            {
                if (hvacObj.TryGetValue("Fault", StringComparison.OrdinalIgnoreCase, out JToken hvacFaultToken) && hvacFaultToken is JArray hvacArray)
                {
                    faultArray = hvacArray;
                }
            }

            if (faultArray == null || faultArray.Count == 0) return false;

            int faultCode = 0;
            int faultCause = 0;
            int faultCommand = 0;

            foreach (var item in faultArray)
            {
                if (item is JObject paramObj)
                {
                    string name = paramObj.Value<string>("Name");
                    float val = 0f;

                    if (paramObj.TryGetValue("Value", StringComparison.OrdinalIgnoreCase, out JToken valToken))
                    {
                        if (valToken.Type == JTokenType.Integer || valToken.Type == JTokenType.Float)
                        {
                            val = valToken.Value<float>();
                        }
                        else if (float.TryParse(valToken.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                        {
                            val = parsed;
                        }
                    }

                    if (string.Equals(name, "Fault_Code_Trigger", StringComparison.OrdinalIgnoreCase))
                    {
                        faultCode = Mathf.RoundToInt(val);
                    }
                    else if (string.Equals(name, "Fault_Cause_Trigger", StringComparison.OrdinalIgnoreCase))
                    {
                        faultCause = Mathf.RoundToInt(val);
                    }
                    else if (string.Equals(name, "Fault_Command_Action", StringComparison.OrdinalIgnoreCase))
                    {
                        faultCommand = Mathf.RoundToInt(val);
                    }
                }
            }

            // Command Action: 0 = Idle, 1 = Trigger, 2 = Reset
            if (faultCommand == 1) // Trigger Fault
            {
                if (faultCode > 0)
                {
                    if (_lastFaultCommandAction != 1 || _lastFaultCodeTrigger != faultCode)
                    {
                        _lastFaultCommandAction = 1;
                        _lastFaultCodeTrigger = faultCode;
                        Debug.Log($"[UDPManager] LabVIEW Fault Trigger Received -> Fault Code: {faultCode} (Cause: {faultCause})");
                        EventManager.RequestTriggerFault?.Invoke(faultCode);
                    }
                    return true;
                }
            }
            else if (faultCommand == 2) // Reset Fault
            {
                if (_lastFaultCommandAction != 2)
                {
                    _lastFaultCommandAction = 2;
                    _lastFaultCodeTrigger = 0;
                    Debug.Log("[UDPManager] LabVIEW Fault Reset Received -> Executing Reset");
                    EventManager.RequestResetFault?.Invoke();
                    var resetAck = new FaultResetPayload();
                    SendString(JsonConvert.SerializeObject(resetAck, Formatting.None));
                }
                return true;
            }
            else if (faultCommand == 0) // Idle
            {
                _lastFaultCommandAction = 0;
                if (faultCode == 0)
                {
                    _lastFaultCodeTrigger = 0;
                }
            }

            return false;
        }

        private static int ExtractCompressorIndex(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            string lower = name.ToLowerInvariant();
            if (lower.Contains("comp"))
            {
                if (lower.Contains("1")) return 1;
                if (lower.Contains("2")) return 2;
                if (lower.Contains("3")) return 3;
                if (lower.Contains("4")) return 4;
            }
            return -1;
        }


        private static bool TryExtractFaultCode(JObject obj, out int faultCode)
        {
            faultCode = -1;
            string[] keys = { "fault_code", "fault", "FaultIndex", "faultIndex", "code", "faultNumber", "fault_id" };
            foreach (var key in keys)
            {
                if (obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken token))
                {
                    if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
                    {
                        faultCode = token.Value<int>();
                        return true;
                    }
                    if (token.Type == JTokenType.String && int.TryParse(token.Value<string>(), out int parsed))
                    {
                        faultCode = parsed;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool ExtractBooleanFromToken(JToken token)
        {
            if (token == null) return false;
            if (token.Type == JTokenType.Boolean) return token.Value<bool>();
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) return token.Value<int>() > 0;
            if (bool.TryParse(token.ToString(), out bool b)) return b;
            if (int.TryParse(token.ToString(), out int i)) return i > 0;
            return false;
        }

        private void DecodeNonJsonPacket(string message)
        {
            string trimmed = message.Trim();

            // 1. Raw integer fault code: e.g. "101", "102"
            if (int.TryParse(trimmed, out int rawCode))
            {
                if (rawCode >= 100 && rawCode < 1000)
                {
                    Debug.Log($"[UDPManager] Detected Raw Fault Code -> {rawCode}");
                    EventManager.RequestTriggerFault?.Invoke(rawCode);
                    return;
                }
            }

            // 2. Simple text command: "reset", "compression_on", "compression_off", etc.
            string lower = trimmed.ToLowerInvariant();
            if (lower == "reset" || lower == "reset_fault")
            {
                Debug.Log("[UDPManager] Detected Raw Reset Command");
                EventManager.RequestResetFault?.Invoke();
                var resetAck = new FaultResetPayload();
                SendString(JsonConvert.SerializeObject(resetAck, Formatting.None));
                return;
            }
            // if (lower == "compression_on" || lower == "highlights_on" || lower == "compression_1")
            // {
            //     Debug.Log("[UDPManager] Detected Raw Compression Highlights On Command");
            //     EventManager.RequestToggleCompressionHighlights?.Invoke(true);
            //     return;
            // }
            // if (lower == "compression_off" || lower == "highlights_off" || lower == "compression_0")
            // {
            //     Debug.Log("[UDPManager] Detected Raw Compression Highlights Off Command");
            //     EventManager.RequestToggleCompressionHighlights?.Invoke(false);
            //     return;
            // }
            // if (lower == "arrows_on" || lower == "arrows_1")
            // {
            //     Debug.Log("[UDPManager] Detected Raw Arrows On Command");
            //     EventManager.RequestToggleArrows?.Invoke(true);
            //     return;
            // }
            // if (lower == "arrows_off" || lower == "arrows_0")
            // {
            //     Debug.Log("[UDPManager] Detected Raw Arrows Off Command");
            //     EventManager.RequestToggleArrows?.Invoke(false);
            //     return;
            // }
            if (lower == "skip" || lower == "skip_step" || lower == "next")
            {
                Debug.Log("[UDPManager] Detected Raw Skip Command");
                EventManager.RequestSkipStep?.Invoke();
                return;
            }

            // 3. Fallback: CSV Sliders Packet
            DecodeCsvPacket(trimmed);
        }

        private void DecodeCsvPacket(string message)
        {
            _cachedFloatBuffer.Clear();
            var tokens = message.Split(',');

            for (var i = 0; i < tokens.Length; i++)
            {
                if (float.TryParse(tokens[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                {
                    _cachedFloatBuffer.Add(val);
                }
            }

            if (_cachedFloatBuffer.Count > 0)
            {
                EventManager.ReceivedUDPData?.Invoke(new List<float>(_cachedFloatBuffer));
                SendString(string.Join(",", _cachedFloatBuffer));
            }
        }

        /// <summary>
        /// Extracts float values from a list of numeric parameter objects and appends them to a buffer.
        /// </summary>
        private static void ExtractNumericValues(List<NumericParameter> parameters, List<float> outputList)
        {
            if (parameters == null) return;
            foreach (var t in parameters)
            {
                outputList.Add(t.Value);
            }
        }

        #endregion
        
        #region Telemetry & Step Progress Handlers

        private void HandleStepAccomplished(int faultCode, int stepNumber, int totalSteps, bool isFaultCompleted)
        {
            if (_stepSendFormat == StepSendFormat.RawNumber || _stepSendFormat == StepSendFormat.Both)
            {
                SendString(stepNumber.ToString());
            }

            if (_stepSendFormat == StepSendFormat.JSON || _stepSendFormat == StepSendFormat.Both)
            {
                var payload = new StepProgressPayload
                {
                    @event = "step_completed",
                    fault_code = faultCode,
                    step_number = stepNumber,
                    total_steps = totalSteps,
                    status = "success",
                    is_fault_completed = isFaultCompleted,
                    compression_highlights = isFaultCompleted,
                    arrows_active = isFaultCompleted
                };
                string json = JsonConvert.SerializeObject(payload, Formatting.None);
                SendString(json);
            }
        }

        private void HandleFaultFinished(int faultCode, int totalSteps, int totalMistakes)
        {
            var payload = new FaultCompletedPayload
            {
                @event = "fault_completed",
                fault_code = faultCode,
                status = "success",
                total_steps = totalSteps,
                total_mistakes = totalMistakes,
                is_fault_completed = true,
                compression_highlights = true,
                arrows_active = true
            };
            string json = JsonConvert.SerializeObject(payload, Formatting.None);
            SendString(json);
        }

        private void HandleFaultStarted(int faultCode, int totalSteps)
        {
            if (!_sendFaultStartedAck) return;

            var payload = new FaultStartedPayload
            {
                @event = "fault_started",
                fault_code = faultCode,
                total_steps = totalSteps,
                status = "started",
                compression_highlights = false,
                arrows_active = false
            };
            string json = JsonConvert.SerializeObject(payload, Formatting.None);
            SendString(json);
        }
        
        private void HandleReset()
        {
            _lastFaultCommandAction = 0;
            _lastFaultCodeTrigger = 0;
        }

        #endregion
    }
}