using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace GamePadInput
{
    // Слой над Steam Input API: Facepunch.Steamworks (Steamworks.SteamInput) для
    // контроллеров/IMU/хаптики + сырой вызов SetInputActionManifestPath из steam_api
    // для собственного action-манифеста (всё через рефлексию, всё опционально).
    internal static class SteamInputApi
    {
        private const float RadToDeg = 57.29578f;
        private const float DeadzoneDegPerSec = 2f;
        private const string ManifestAccessorExport = "SteamAPI_SteamInput_v00";
        private const string SetManifestExport = "SteamAPI_ISteamInput_SetInputActionManifestPath";

        private static bool inited;
        private static double lastInitTry = -1.0;
        private static double lastPollTime = -1.0;
        private static double readySince = -1.0;

        private static MethodInfo runFrame;
        private static MethodInfo getControllers;
        private static MethodInfo getMotionData;
        private static MethodInfo inputTypeFor;
        private static MethodInfo hapticPulse;
        private static MethodInfo repeatedHaptic;
        private static MethodInfo stopVibration;
        private static MethodInfo getDigitalActionHandle;
        private static MethodInfo getDigitalActionData;
        private static MethodInfo getAnalogActionHandle;
        private static MethodInfo getAnalogActionData;
        private static MethodInfo getActionSetHandle;
        private static MethodInfo activateActionSet;
        private static FieldInfo rotVelX;
        private static FieldInfo rotVelY;
        private static FieldInfo digitalStateField;
        private static FieldInfo digitalActiveField;
        private static FieldInfo analogXField;
        private static FieldInfo analogYField;

        // Порядок строго соответствует GPadButton: A,B,X,Y,LB,RB,LS,RS,Start,Back,DPad*,LT,RT
        private static readonly string[][] DigitalNameSets =
        {
            new[] { "a", "b", "x", "y", "leftbumper", "rightbumper", "leftstick", "rightstick", "start", "back", "dpup", "dpdown", "dpleft", "dpright", "lefttrigger", "righttrigger" },
            new[] { "a_button", "b_button", "x_button", "y_button", "left_bumper", "right_bumper", "left_stick_click", "right_stick_click", "start_button", "back_button", "d_pad_up", "d_pad_down", "d_pad_left", "d_pad_right", "left_trigger", "right_trigger" }
        };
        private const string LeftStickAction = "leftstick_move";
        private const string RightStickAction = "rightstick_move";

        private static readonly IntPtr[] digitalHandles = new IntPtr[16];
        private static readonly bool[] actionButtons = new bool[16];
        private static IntPtr leftStickHandle;
        private static IntPtr rightStickHandle;
        private static IntPtr actionSetHandle;

        public static bool ActionsAvailable { get; private set; }
        public static bool[] ActionButtons { get { return actionButtons; } }
        public static float ActionLeftX { get; private set; }
        public static float ActionLeftY { get; private set; }
        public static float ActionRightX { get; private set; }
        public static float ActionRightY { get; private set; }

        public static bool IsReady { get; private set; }
        public static bool HasControllers { get; private set; }
        public static IntPtr FirstController { get; private set; }
        public static bool MotionAvailable { get; private set; }
        public static float MotionPitch { get; private set; }
        public static float MotionYaw { get; private set; }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr d_Accessor();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int d_SetManifestPath(IntPtr self, IntPtr path);

        public static bool EnsureReady(double gameTime)
        {
            if (IsReady) { return true; }
            if (lastInitTry >= 0 && gameTime >= 0 && gameTime - lastInitTry < 3.0) { return false; }
            lastInitTry = gameTime >= 0 ? gameTime : 0.0;
            TryInit();
            return IsReady;
        }

        private static bool noControllersLogged;
        private static bool controllersSeenLogged;

        public static void PollControllers(double gameTime)
        {
            if (!IsReady) { HasControllers = false; return; }
            if (gameTime >= 0 && gameTime == lastPollTime) { return; }
            lastPollTime = gameTime;
            try
            {
                runFrame.Invoke(null, null);
                object[] args = { new IntPtr[16] };
                object result = getControllers.Invoke(null, args);
                IntPtr[] found = result as IntPtr[] ?? args[0] as IntPtr[];
                IntPtr controller = IntPtr.Zero;
                if (found != null)
                {
                    foreach (IntPtr handle in found)
                    {
                        if (handle != IntPtr.Zero) { controller = handle; break; }
                    }
                }
                if (controller == IntPtr.Zero)
                {
                    HasControllers = false;
                    FirstController = IntPtr.Zero;
                    MotionAvailable = false;
                    ActionsAvailable = false;
                    if (!noControllersLogged && gameTime >= 0 && gameTime - readySince > 5.0)
                    {
                        noControllersLogged = true;
                        LuaCsLogger.LogMessage("GamepadInput: Steam Input API ready but no controllers attached - Steam Input is likely running the keyboard/mouse template; set a Gamepad template layout for Barotrauma in Steam, or disable Steam Input and connect the pad directly");
                    }
                    return;
                }
                if (!controllersSeenLogged)
                {
                    controllersSeenLogged = true;
                    noControllersLogged = false;
                    LuaCsLogger.LogMessage("GamepadInput: Steam Input controller attached (" + GetInputTypeName() + ")");
                }
                HasControllers = true;
                FirstController = controller;
                ReadMotion(controller);
                if (!ActionsAvailable) { ResolveActions(controller); }
                if (ActionsAvailable) { ReadActions(controller); }
            }
            catch
            {
                HasControllers = false;
                MotionAvailable = false;
            }
        }

        private static void ReadMotion(IntPtr controller)
        {
            object motion = getMotionData.Invoke(null, new object[] { controller });
            float pitch = Convert.ToSingle(rotVelX.GetValue(motion)) * RadToDeg;
            float yaw = Convert.ToSingle(rotVelY.GetValue(motion)) * RadToDeg;
            pitch = Math.Abs(pitch) < DeadzoneDegPerSec ? 0f : pitch;
            yaw = Math.Abs(yaw) < DeadzoneDegPerSec ? 0f : yaw;
            MotionPitch = pitch;
            MotionYaw = yaw;
            MotionAvailable = pitch != 0f || yaw != 0f;
        }

        // Имена действий — SDL-конвенция (Steam генерирует дефолтные биндинги для legacy-сетов по ней).
        private static void ResolveActions(IntPtr controller)
        {
            if (getDigitalActionHandle == null || getDigitalActionData == null || digitalStateField == null) { return; }
            foreach (string[] names in DigitalNameSets)
            {
                int resolved = 0;
                IntPtr[] handles = new IntPtr[16];
                for (int i = 0; i < 16; i++)
                {
                    handles[i] = GetDigitalHandle(names[i]);
                    if (handles[i] != IntPtr.Zero) { resolved++; }
                }
                if (resolved < 8) { continue; }
                Array.Copy(handles, digitalHandles, 16);
                leftStickHandle = getAnalogActionHandle != null ? GetWrappedHandle(getAnalogActionHandle, LeftStickAction) : IntPtr.Zero;
                rightStickHandle = getAnalogActionHandle != null ? GetWrappedHandle(getAnalogActionHandle, RightStickAction) : IntPtr.Zero;
                actionSetHandle = getActionSetHandle != null ? GetWrappedHandle(getActionSetHandle, "Default") : IntPtr.Zero;
                if (actionSetHandle != IntPtr.Zero && activateActionSet != null)
                {
                    try { activateActionSet.Invoke(null, new[] { controller, WrapHandleArg(activateActionSet, 1, actionSetHandle) }); }
                    catch { }
                }
                ActionsAvailable = true;
                LuaCsLogger.LogMessage("GamepadInput: Steam Input actions resolved (" + resolved + "/16, set \"" + names[0] + "...\")");
                return;
            }
            LuaCsLogger.LogMessage("GamepadInput: Steam Input actions did not resolve - open Steam Input layout for Barotrauma once so Steam generates bindings for the mod's actions");
        }

        private static void ReadActions(IntPtr controller)
        {
            for (int i = 0; i < 16; i++)
            {
                actionButtons[i] = digitalHandles[i] != IntPtr.Zero && GetDigitalState(controller, digitalHandles[i]);
            }
            float lx, ly, rx, ry;
            GetAnalogXY(controller, leftStickHandle, out lx, out ly);
            GetAnalogXY(controller, rightStickHandle, out rx, out ry);
            ActionLeftX = lx;
            ActionLeftY = ly;
            ActionRightX = rx;
            ActionRightY = ry;
        }

        private static IntPtr GetDigitalHandle(string name)
        {
            try
            {
                return UnwrapHandle(getDigitalActionHandle.Invoke(null, new object[] { name }));
            }
            catch { return IntPtr.Zero; }
        }

        private static IntPtr GetWrappedHandle(MethodInfo method, string name)
        {
            try
            {
                return UnwrapHandle(method.Invoke(null, new object[] { name }));
            }
            catch { return IntPtr.Zero; }
        }

        private static bool GetDigitalState(IntPtr controller, IntPtr handle)
        {
            try
            {
                object data = getDigitalActionData.Invoke(null, WrapActionArgs(getDigitalActionData, controller, handle));
                bool state = digitalStateField != null && (bool)digitalStateField.GetValue(data);
                bool active = digitalActiveField == null || (bool)digitalActiveField.GetValue(data);
                return state && active;
            }
            catch { return false; }
        }

        private static void GetAnalogXY(IntPtr controller, IntPtr handle, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (handle == IntPtr.Zero || analogXField == null) { return; }
            try
            {
                object data = getAnalogActionData.Invoke(null, WrapActionArgs(getAnalogActionData, controller, handle));
                x = Convert.ToSingle(analogXField.GetValue(data));
                y = Convert.ToSingle(analogYField.GetValue(data));
            }
            catch { }
        }

        private static object[] WrapActionArgs(MethodInfo method, IntPtr controller, IntPtr handle)
        {
            ParameterInfo[] pars = method.GetParameters();
            return new[]
            {
                ConvertArg(controller, pars[0].ParameterType),
                ConvertArg(handle, pars.Length > 1 ? pars[1].ParameterType : typeof(IntPtr))
            };
        }

        private static object WrapHandleArg(MethodInfo method, int index, IntPtr handle)
        {
            Type paramType = method.GetParameters()[index].ParameterType;
            return ConvertArg(handle, paramType);
        }

        private static IntPtr UnwrapHandle(object value)
        {
            if (value is IntPtr pointer) { return pointer; }
            if (value == null) { return IntPtr.Zero; }
            Type type = value.GetType();
            if (type.IsValueType && !type.IsPrimitive && !type.IsEnum)
            {
                foreach (FieldInfo field in type.GetFields())
                {
                    if (field.FieldType == typeof(IntPtr) && field.GetValue(value) is IntPtr inner)
                    {
                        return inner;
                    }
                }
            }
            return IntPtr.Zero;
        }

        public static string GetInputTypeName()
        {
            if (inputTypeFor == null || FirstController == IntPtr.Zero) { return "Steam Input device"; }
            try
            {
                return FriendlyType(inputTypeFor.Invoke(null, new object[] { FirstController }).ToString());
            }
            catch
            {
                return "Steam Input device";
            }
        }

        private static string FriendlyType(string raw)
        {
            switch (raw)
            {
                case "SteamController": return "Steam Controller";
                case "XBox360Controller": return "Xbox 360";
                case "XBoxOneController": return "Xbox One";
                case "GenericXInput": return "XInput";
                case "PS4Controller": return "DualShock 4";
                case "PsFourController": return "DualShock 4";
                case "PS5Controller": return "DualSense";
                case "PsFiveController": return "DualSense";
                case "DeckController": return "Steam Deck";
                case "SwitchJoyConPair": return "Joy-Con pair";
                case "SwitchJoyConSingle": return "Joy-Con";
                case "SwitchProController": return "Switch Pro";
                case "MobileTouch": return "Mobile touch";
                default: return string.IsNullOrEmpty(raw) ? "Steam Input device" : raw;
            }
        }

        public static bool TriggerHaptic(float strength, float durationSeconds)
        {
            if (!IsReady || FirstController == IntPtr.Zero) { return false; }
            try
            {
                if (repeatedHaptic != null && repeatedHaptic.GetParameters().Length == 6)
                {
                    ushort onUs = (ushort)Math.Clamp((int)(4000f + strength * 16000f), 4000, 60000);
                    ushort offUs = 5000;
                    ushort repeat = (ushort)Math.Clamp((int)(durationSeconds * 1000000f / (onUs + offUs)), 1, 500);
                    for (int pad = 0; pad <= 1; pad++)
                    {
                        repeatedHaptic.Invoke(null, BuildArgs(repeatedHaptic, pad, onUs, offUs, repeat, 0u));
                    }
                    return true;
                }
                if (hapticPulse != null)
                {
                    ushort durationUs = (ushort)Math.Clamp((int)(durationSeconds * 1000000f), 1000, 60000);
                    for (int pad = 0; pad <= 1; pad++)
                    {
                        hapticPulse.Invoke(null, BuildArgs(hapticPulse, pad, durationUs));
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }

        public static bool StopHaptic()
        {
            if (!IsReady || FirstController == IntPtr.Zero || stopVibration == null) { return false; }
            try
            {
                stopVibration.Invoke(null, new object[] { FirstController });
                return true;
            }
            catch { return false; }
        }

        private static object[] BuildArgs(MethodInfo method, params object[] values)
        {
            ParameterInfo[] pars = method.GetParameters();
            object[] args = new object[pars.Length];
            args[0] = FirstController;
            for (int i = 1; i < pars.Length; i++)
            {
                object value = i - 1 < values.Length ? values[i - 1] : null;
                args[i] = ConvertArg(value, pars[i].ParameterType);
            }
            return args;
        }

        private static object ConvertArg(object value, Type paramType)
        {
            if (paramType.IsEnum)
            {
                return Enum.ToObject(paramType, value == null ? 0 : Convert.ToInt32(value));
            }
            if (value is IntPtr pointer && paramType.IsValueType && !paramType.IsPrimitive && paramType != typeof(IntPtr))
            {
                try { return Activator.CreateInstance(paramType, pointer); }
                catch { return pointer; }
            }
            if (value != null && paramType.IsPrimitive && value.GetType() != paramType)
            {
                return Convert.ChangeType(value, paramType, CultureInfo.InvariantCulture);
            }
            return value;
        }

        private static void TryInit()
        {
            try
            {
                if (!SteamInputCompat.SteamAvailable) { return; }
                Type steamInput = FindSteamInputType();
                if (steamInput == null) { return; }

                TrySetActionManifestPath();

                MethodInfo init = steamInput.GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
                if (init != null)
                {
                    object[] initArgs = init.GetParameters().Length > 0 ? new object[] { false } : null;
                    object initResult = init.Invoke(null, initArgs);
                    if (initResult is bool ok && !ok) { return; }
                }

                runFrame = steamInput.GetMethod("RunFrame", BindingFlags.Public | BindingFlags.Static);
                getControllers = steamInput.GetMethod("GetConnectedControllers", BindingFlags.Public | BindingFlags.Static);
                getMotionData = steamInput.GetMethod("GetMotionData", BindingFlags.Public | BindingFlags.Static);
                if (runFrame == null || getControllers == null || getMotionData == null) { return; }
                inputTypeFor = steamInput.GetMethod("GetInputTypeForHandle", BindingFlags.Public | BindingFlags.Static);
                hapticPulse = steamInput.GetMethod("TriggerHapticPulse", BindingFlags.Public | BindingFlags.Static);
                repeatedHaptic = steamInput.GetMethod("TriggerRepeatedHapticPulse", BindingFlags.Public | BindingFlags.Static);
                stopVibration = steamInput.GetMethod("StopVibration", BindingFlags.Public | BindingFlags.Static);
                getDigitalActionHandle = steamInput.GetMethod("GetDigitalActionHandle", BindingFlags.Public | BindingFlags.Static);
                getDigitalActionData = steamInput.GetMethod("GetDigitalActionData", BindingFlags.Public | BindingFlags.Static);
                getAnalogActionHandle = steamInput.GetMethod("GetAnalogActionHandle", BindingFlags.Public | BindingFlags.Static);
                getAnalogActionData = steamInput.GetMethod("GetAnalogActionData", BindingFlags.Public | BindingFlags.Static);
                getActionSetHandle = steamInput.GetMethod("GetActionSetHandle", BindingFlags.Public | BindingFlags.Static);
                activateActionSet = steamInput.GetMethod("ActivateActionSet", BindingFlags.Public | BindingFlags.Static);
                if (getDigitalActionData != null)
                {
                    Type dataState = getDigitalActionData.ReturnType;
                    digitalStateField = dataState.GetField("State") ?? dataState.GetField("bState");
                    digitalActiveField = dataState.GetField("Active") ?? dataState.GetField("bActive");
                }
                if (getAnalogActionData != null)
                {
                    Type dataAnalog = getAnalogActionData.ReturnType;
                    analogXField = dataAnalog.GetField("X") ?? dataAnalog.GetField("x");
                    analogYField = dataAnalog.GetField("Y") ?? dataAnalog.GetField("y");
                }

                Type motionType = getMotionData.ReturnType;
                rotVelX = motionType.GetField("RotVelX");
                rotVelY = motionType.GetField("RotVelY");
                if (rotVelX == null || rotVelY == null) { return; }

                inited = true;
                IsReady = true;
                readySince = lastPollTime >= 0 ? lastPollTime : lastInitTry;
                LuaCsLogger.LogMessage("GamepadInput: Steam Input API initialized");
            }
            catch (Exception e)
            {
                LuaCsLogger.LogMessage("GamepadInput: Steam Input API init failed: " + e.Message);
            }
        }

        // ISteamInput::SetInputActionManifestPath должен вызываться до Init;
        // Facepunch этот метод не оборачивает, поэтому идём напрямую в steam_api.
        private static void TrySetActionManifestPath()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Daedalic Entertainment GmbH", "Barotrauma", "GamepadInput", "steam_input");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "game_actions_4920.vdf");
                File.WriteAllText(path, BuildActionManifest());

                string libName = OperatingSystem.IsWindows() ? "steam_api64.dll"
                    : OperatingSystem.IsMacOS() ? "libsteam_api.dylib" : "libsteam_api.so";
                if (!NativeLibrary.TryLoad(libName, out IntPtr lib)) { return; }

                IntPtr self = IntPtr.Zero;
                for (int version = 1; version <= 3; version++)
                {
                    try
                    {
                        IntPtr proc = NativeLibrary.GetExport(lib, ManifestAccessorExport + version.ToString(CultureInfo.InvariantCulture));
                        var accessor = Marshal.GetDelegateForFunctionPointer<d_Accessor>(proc);
                        self = accessor();
                        if (self != IntPtr.Zero) { break; }
                    }
                    catch { }
                }
                if (self == IntPtr.Zero) { return; }

                try
                {
                    IntPtr setPathProc = NativeLibrary.GetExport(lib, SetManifestExport);
                    var setPath = Marshal.GetDelegateForFunctionPointer<d_SetManifestPath>(setPathProc);
                    IntPtr pathPtr = Marshal.StringToHGlobalAnsi(path);
                    try
                    {
                        int ok = setPath(self, pathPtr);
                        LuaCsLogger.LogMessage("GamepadInput: Steam Input action manifest " + (ok != 0 ? "loaded" : "rejected") + " (" + path + ")");
                    }
                    finally { Marshal.FreeHGlobal(pathPtr); }
                }
                catch
                {
                    // старый SDK без SetInputActionManifestPath — не критично
                }
            }
            catch
            {
                // манифест опционален: гиро/хаптика/идентификация работают и без него
            }
        }

        private static string BuildActionManifest()
        {
            string[] digital =
            {
                "a", "b", "x", "y", "start", "guide", "back",
                "leftbumper", "rightbumper", "leftstick", "rightstick",
                "dpup", "dpdown", "dpleft", "dpright",
                "lefttrigger", "righttrigger"
            };
            StringBuilder inputs = new StringBuilder();
            foreach (string action in digital)
            {
                inputs.Append("\t\t\t\t\"").Append(action).Append("\"\n")
                    .Append("\t\t\t\t{\n")
                    .Append("\t\t\t\t\t\"activators\"\n\t\t\t\t\t{\n")
                    .Append("\t\t\t\t\t\t\"Full_Trigger\"\n\t\t\t\t\t\t{\n")
                    .Append("\t\t\t\t\t\t\t\"outputs\"\n\t\t\t\t\t\t\t{\n")
                    .Append("\t\t\t\t\t\t\t\t\"digital_action\"\t\"").Append(action).Append("\"\n")
                    .Append("\t\t\t\t\t\t\t}\n\t\t\t\t\t\t}\n\t\t\t\t\t}\n\t\t\t\t}\n");
            }
            foreach (string action in new[] { "leftstick_move", "rightstick_move" })
            {
                inputs.Append("\t\t\t\t\"").Append(action).Append("\"\n")
                    .Append("\t\t\t\t{\n")
                    .Append("\t\t\t\t\t\"activators\"\n\t\t\t\t\t{\n")
                    .Append("\t\t\t\t\t\t\"joystick_move\"\n\t\t\t\t\t\t{\n")
                    .Append("\t\t\t\t\t\t\t\"outputs\"\n\t\t\t\t\t\t\t{\n")
                    .Append("\t\t\t\t\t\t\t\t\"analog_action\"\t\"").Append(action).Append("\"\n")
                    .Append("\t\t\t\t\t\t\t}\n\t\t\t\t\t\t}\n\t\t\t\t\t}\n\t\t\t\t}\n");
            }
            return "\"In Game Action Manifest\"\n"
                + "{\n"
                + "\t\"actions\"\n\t{\n"
                + "\t\t\"Default\"\n\t\t{\n"
                + "\t\t\t\"title\"\t\t\t\"Gamepad Input\"\n"
                + "\t\t\t\"legacy_set\"\t\"1\"\n"
                + "\t\t\t\"button_set_a\"\t\"0\"\n"
                + "\t\t\t\"inputs\"\n\t\t\t{\n"
                + inputs.ToString()
                + "\t\t\t}\n\t\t}\n\t}\n"
                + "\t\"action_set_layer_counts\"\n\t{\n\t}\n"
                + "\t\"localization\"\n\t{\n"
                + "\t\t\"english\"\n\t\t{\n"
                + "\t\t\t\"title\"\t\t\t\"Gamepad Input for Barotrauma\"\n"
                + "\t\t\t\"description\"\t\"Actions exposed by the Gamepad Input mod.\"\n"
                + "\t\t}\n\t}\n"
                + "}\n";
        }

        public static void Shutdown()
        {
            try
            {
                if (!inited) { return; }
                FindSteamInputType()?.GetMethod("Shutdown", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            }
            catch { }
            inited = false;
            IsReady = false;
            HasControllers = false;
            MotionAvailable = false;
            FirstController = IntPtr.Zero;
            lastPollTime = -1.0;
        }

        // У динамических сборок (скрипты LuaCs компилируются в рантайме) GetName() может кидать — сканируем безопасно.
        private static Type FindSteamInputType()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetName().Name != "Facepunch.Steamworks") { continue; }
                    Type type = assembly.GetType("Steamworks.SteamInput");
                    if (type != null) { return type; }
                }
                catch { }
            }
            return null;
        }
    }
}
