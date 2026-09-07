using System;
using System.Runtime.InteropServices;

namespace GamePadInput
{
    internal static class SdlNative
    {
        public const int AxisLeftX = 0;
        public const int AxisLeftY = 1;
        public const int AxisRightX = 2;
        public const int AxisRightY = 3;
        public const int AxisTriggerLeft = 4;
        public const int AxisTriggerRight = 5;

        public const int SensorGyro = 2;
        public const int SensorGyroL = 4;
        public const int SensorGyroR = 6;

        public const int TypeVirtual = 6;

        public static IntPtr Handle { get; private set; }
        public static bool Loaded { get; private set; }

        public static bool RumbleSupported { get; private set; }
        public static bool SensorsSupported { get; private set; }
        public static bool TouchSupported { get; private set; }
        public static bool PowerSupported { get; private set; }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_GetNumGameControllers();
        public static d_GetNumGameControllers GetNumGameControllers;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr d_GameControllerOpen(int index);
        public static d_GameControllerOpen GameControllerOpen;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void d_GameControllerClose(IntPtr gc);
        public static d_GameControllerClose GameControllerClose;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_GameControllerGetAttached(IntPtr gc);
        public static d_GameControllerGetAttached GameControllerGetAttached;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr d_GameControllerName(IntPtr gc);
        public static d_GameControllerName GameControllerName;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate short d_GameControllerGetAxis(IntPtr gc, int axis);
        public static d_GameControllerGetAxis GameControllerGetAxis;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate byte d_GameControllerGetButton(IntPtr gc, int button);
        public static d_GameControllerGetButton GameControllerGetButton;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr d_GameControllerGetJoystick(IntPtr gc);
        public static d_GameControllerGetJoystick GameControllerGetJoystick;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr d_GetError();
        public static d_GetError GetError;

        public static string LastError
        {
            get
            {
                try
                {
                    IntPtr ptr = GetError();
                    return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : "";
                }
                catch { return ""; }
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_GameControllerGetType(IntPtr gc);
        public static d_GameControllerGetType GameControllerGetType;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate ushort d_GameControllerGetVendor(IntPtr gc);
        public static d_GameControllerGetVendor GameControllerGetVendor;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_GameControllerRumble(IntPtr gc, ushort lowFrequency, ushort highFrequency, uint durationMs);
        public static d_GameControllerRumble GameControllerRumble;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_GameControllerHasSensor(IntPtr gc, int sensorType);
        public static d_GameControllerHasSensor GameControllerHasSensor;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_GameControllerSetSensorEnabled(IntPtr gc, int sensorType, int enabled);
        public static d_GameControllerSetSensorEnabled GameControllerSetSensorEnabled;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_GameControllerGetSensorData(IntPtr gc, int sensorType, float[] data, int numValues);
        public static d_GameControllerGetSensorData GameControllerGetSensorData;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_GameControllerGetNumTouchpads(IntPtr gc);
        public static d_GameControllerGetNumTouchpads GameControllerGetNumTouchpads;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_GameControllerGetTouchpadFinger(IntPtr gc, int touchpad, int finger, out byte state, out float x, out float y);
        public static d_GameControllerGetTouchpadFinger GameControllerGetTouchpadFinger;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_JoystickCurrentPowerLevel(IntPtr joystick);
        public static d_JoystickCurrentPowerLevel JoystickCurrentPowerLevel;

        public static bool TryLoad()
        {
            if (Loaded) { return true; }

            string[] candidates = OperatingSystem.IsWindows()
                ? new[] { "SDL2.dll" }
                : OperatingSystem.IsMacOS()
                    ? new[] { "libSDL2-2.0.0.dylib", "SDL2" }
                    : new[] { "libSDL2-2.0.so.0", "SDL2", "libSDL2.so" };

            foreach (string library in candidates)
            {
                if (NativeLibrary.TryLoad(library, out IntPtr handle))
                {
                    Handle = handle;
                    Loaded = true;
                    try
                    {
                        Resolve();
                        return true;
                    }
                    catch
                    {
                        Loaded = false;
                        Handle = IntPtr.Zero;
                        return false;
                    }
                }
            }
            return false;
        }

        private static void Resolve()
        {
            GetNumGameControllers = GetExport<d_GetNumGameControllers>("SDL_GetNumGameControllers");
            GameControllerOpen = GetExport<d_GameControllerOpen>("SDL_GameControllerOpen");
            GameControllerClose = GetExport<d_GameControllerClose>("SDL_GameControllerClose");
            GameControllerGetAttached = GetExport<d_GameControllerGetAttached>("SDL_GameControllerGetAttached");
            GameControllerName = GetExport<d_GameControllerName>("SDL_GameControllerName");
            GameControllerGetAxis = GetExport<d_GameControllerGetAxis>("SDL_GameControllerGetAxis");
            GameControllerGetButton = GetExport<d_GameControllerGetButton>("SDL_GameControllerGetButton");
            GameControllerGetJoystick = GetExport<d_GameControllerGetJoystick>("SDL_GameControllerGetJoystick");
            GetError = TryGetExport<d_GetError>("SDL_GetError");

            GameControllerGetType = TryGetExport<d_GameControllerGetType>("SDL_GameControllerGetType");
            GameControllerGetVendor = TryGetExport<d_GameControllerGetVendor>("SDL_GameControllerGetVendor");
            GameControllerRumble = TryGetExport<d_GameControllerRumble>("SDL_GameControllerRumble");
            GameControllerHasSensor = TryGetExport<d_GameControllerHasSensor>("SDL_GameControllerHasSensor");
            GameControllerSetSensorEnabled = TryGetExport<d_GameControllerSetSensorEnabled>("SDL_GameControllerSetSensorEnabled");
            GameControllerGetSensorData = TryGetExport<d_GameControllerGetSensorData>("SDL_GameControllerGetSensorData");
            GameControllerGetNumTouchpads = TryGetExport<d_GameControllerGetNumTouchpads>("SDL_GameControllerGetNumTouchpads");
            GameControllerGetTouchpadFinger = TryGetExport<d_GameControllerGetTouchpadFinger>("SDL_GameControllerGetTouchpadFinger");
            JoystickCurrentPowerLevel = TryGetExport<d_JoystickCurrentPowerLevel>("SDL_JoystickCurrentPowerLevel");

            RumbleSupported = GameControllerRumble != null;
            SensorsSupported = GameControllerHasSensor != null && GameControllerSetSensorEnabled != null && GameControllerGetSensorData != null;
            TouchSupported = GameControllerGetNumTouchpads != null && GameControllerGetTouchpadFinger != null;
            PowerSupported = JoystickCurrentPowerLevel != null && GameControllerGetJoystick != null;
        }

        private static T GetExport<T>(string name) where T : Delegate
        {
            return (T)Marshal.GetDelegateForFunctionPointer(NativeLibrary.GetExport(Handle, name), typeof(T));
        }

        private static T TryGetExport<T>(string name) where T : Delegate
        {
            try
            {
                return GetExport<T>(name);
            }
            catch
            {
                return null;
            }
        }
    }
}
