using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace GamePadInput
{
    internal class SdlPadBackend : IPadBackend
    {
        private const float GyroDeadzoneDegPerSec = 2f;
        private const float RadToDeg = 57.29578f;

        private IntPtr handle = IntPtr.Zero;
        private readonly bool[] buttons = new bool[16];
        private readonly float[] gyroBuffer = new float[3];
        private bool gyroDeviceEnabled;
        private bool gyroSensorAvailable;
        private int gyroSensorType = SdlNative.SensorGyro;
        private bool gyroDataErrorLogged;
        private bool touchHadPrev;
        private byte touchPrevDown;
        private float touchPrevX;
        private float touchPrevY;

        public string Name { get { return "SDL2"; } }

        public bool IsAvailable { get { return SdlNative.TryLoad(); } }

        public bool IsConnected
        {
            get { return handle != IntPtr.Zero && SdlNative.GameControllerGetAttached(handle) != 0; }
        }

        public bool HasGyro { get { return gyroDeviceEnabled; } }

        public bool TryConnect()
        {
            if (IsConnected) { return true; }
            if (!SdlNative.TryLoad()) { return false; }
            Close();

            int count = SdlNative.GetNumGameControllers();
            for (int i = 0; i < count; i++)
            {
                IntPtr opened = SdlNative.GameControllerOpen(i);
                if (opened != IntPtr.Zero)
                {
                    handle = opened;
                    EnableGyro();
                    return true;
                }
            }
            return false;
        }

        private void EnableGyro()
        {
            gyroDeviceEnabled = false;
            gyroSensorAvailable = false;
            gyroDataErrorLogged = false;
            gyroSensorType = SdlNative.SensorGyro;
            if (!SdlNative.SensorsSupported)
            {
                LuaCsLogger.LogMessage("GamepadInput: gyro disabled (SDL2 is older than 2.0.9, sensors API missing)");
                return;
            }

            IntPtr namePtr = SdlNative.GameControllerName(handle);
            string name = namePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(namePtr) : null;
            if (string.IsNullOrEmpty(name)) { name = "Gamepad"; }

            int[] sensorTypes = { SdlNative.SensorGyro, SdlNative.SensorGyroL, SdlNative.SensorGyroR };
            for (int i = 0; i < sensorTypes.Length; i++)
            {
                int type = sensorTypes[i];
                if (SdlNative.GameControllerHasSensor(handle, type) == 0) { continue; }
                gyroSensorAvailable = true;
                gyroSensorType = type;
                int result = -1;
                try { result = SdlNative.GameControllerSetSensorEnabled(handle, type, 1); } catch { }
                gyroDeviceEnabled = result == 0;
                string label = type == SdlNative.SensorGyro ? "gyro" : (type == SdlNative.SensorGyroL ? "gyro-L" : "gyro-R");
                LuaCsLogger.LogMessage("GamepadInput: " + name + " gyro [" + label + "]: detected, SDL enable result " + result + (gyroDeviceEnabled ? "" : " (FAILED: " + SdlNative.LastError + ")"));
                return;
            }
            LuaCsLogger.LogMessage("GamepadInput: " + name + " gyro: no sensor exposed by SDL2 (combined Joy-Cons or Steam Input often hide it; try pairing Joy-Cons separately or disabling Steam Input)");
        }

        public void Disconnect()
        {
            Close();
        }

        private void Close()
        {
            if (handle == IntPtr.Zero) { return; }
            try { SdlNative.GameControllerClose(handle); } catch { }
            handle = IntPtr.Zero;
            gyroDeviceEnabled = false;
            gyroSensorAvailable = false;
            gyroDataErrorLogged = false;
        }

        public PadSnapshot Poll()
        {
            PadSnapshot snap = default;
            if (!IsConnected && !TryConnect()) { return snap; }

            IntPtr gc = handle;
            snap.IsConnected = true;
            snap.SdlType = SdlNative.GameControllerGetType != null ? SdlNative.GameControllerGetType(gc) : -1;
            snap.VendorId = SdlNative.GameControllerGetVendor != null ? SdlNative.GameControllerGetVendor(gc) : (ushort)0;

            IntPtr namePtr = SdlNative.GameControllerName(gc);
            snap.DeviceName = namePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(namePtr) : "Gamepad";
            if (string.IsNullOrEmpty(snap.DeviceName)) { snap.DeviceName = "Gamepad"; }

            snap.LeftX = ConvertAxis(SdlNative.GameControllerGetAxis(gc, SdlNative.AxisLeftX));
            snap.LeftY = -ConvertAxis(SdlNative.GameControllerGetAxis(gc, SdlNative.AxisLeftY));
            snap.RightX = ConvertAxis(SdlNative.GameControllerGetAxis(gc, SdlNative.AxisRightX));
            snap.RightY = -ConvertAxis(SdlNative.GameControllerGetAxis(gc, SdlNative.AxisRightY));
            snap.LeftTrigger = ConvertTrigger(SdlNative.GameControllerGetAxis(gc, SdlNative.AxisTriggerLeft));
            snap.RightTrigger = ConvertTrigger(SdlNative.GameControllerGetAxis(gc, SdlNative.AxisTriggerRight));

            buttons[(int)GPadButton.A] = SdlNative.GameControllerGetButton(gc, 0) != 0;
            buttons[(int)GPadButton.B] = SdlNative.GameControllerGetButton(gc, 1) != 0;
            buttons[(int)GPadButton.X] = SdlNative.GameControllerGetButton(gc, 2) != 0;
            buttons[(int)GPadButton.Y] = SdlNative.GameControllerGetButton(gc, 3) != 0;
            buttons[(int)GPadButton.Back] = SdlNative.GameControllerGetButton(gc, 4) != 0;
            buttons[(int)GPadButton.Start] = SdlNative.GameControllerGetButton(gc, 6) != 0;
            buttons[(int)GPadButton.LS] = SdlNative.GameControllerGetButton(gc, 7) != 0;
            buttons[(int)GPadButton.RS] = SdlNative.GameControllerGetButton(gc, 8) != 0;
            buttons[(int)GPadButton.LB] = SdlNative.GameControllerGetButton(gc, 9) != 0;
            buttons[(int)GPadButton.RB] = SdlNative.GameControllerGetButton(gc, 10) != 0;
            buttons[(int)GPadButton.DPadUp] = SdlNative.GameControllerGetButton(gc, 11) != 0;
            buttons[(int)GPadButton.DPadDown] = SdlNative.GameControllerGetButton(gc, 12) != 0;
            buttons[(int)GPadButton.DPadLeft] = SdlNative.GameControllerGetButton(gc, 13) != 0;
            buttons[(int)GPadButton.DPadRight] = SdlNative.GameControllerGetButton(gc, 14) != 0;
            buttons[(int)GPadButton.LT] = snap.LeftTrigger > GPadInput.TriggerThreshold;
            buttons[(int)GPadButton.RT] = snap.RightTrigger > GPadInput.TriggerThreshold;
            snap.Buttons = buttons;

            snap.HasGyro = gyroDeviceEnabled;
            snap.GyroAvailable = gyroSensorAvailable;
            if (gyroDeviceEnabled)
            {
                if (SdlNative.GameControllerGetSensorData(gc, gyroSensorType, gyroBuffer, 3) == 0)
                {
                    float pitchDeg = gyroBuffer[0] * RadToDeg;
                    float yawDeg = gyroBuffer[1] * RadToDeg;
                    snap.GyroPitch = Math.Abs(pitchDeg) < GyroDeadzoneDegPerSec ? 0f : pitchDeg;
                    snap.GyroYaw = Math.Abs(yawDeg) < GyroDeadzoneDegPerSec ? 0f : yawDeg;
                }
                else if (!gyroDataErrorLogged)
                {
                    gyroDataErrorLogged = true;
                    LuaCsLogger.LogMessage("GamepadInput: gyro enabled but SDL_GameControllerGetSensorData failed: " + SdlNative.LastError);
                }
            }

            if (SdlNative.TouchSupported && SdlNative.GameControllerGetNumTouchpads(gc) > 0)
            {
                byte fingerDown;
                float fingerX, fingerY;
                if (SdlNative.GameControllerGetTouchpadFinger(gc, 0, 0, out fingerDown, out fingerX, out fingerY) == 0)
                {
                    snap.HasTouchpad = true;
                    snap.TouchpadDown = fingerDown != 0;
                    if (fingerDown != 0)
                    {
                        if (touchPrevDown != 0 && touchHadPrev)
                        {
                            snap.TouchDX = fingerX - touchPrevX;
                            snap.TouchDY = fingerY - touchPrevY;
                        }
                        touchPrevX = fingerX;
                        touchPrevY = fingerY;
                        touchPrevDown = fingerDown;
                        touchHadPrev = true;
                    }
                    else
                    {
                        touchPrevDown = 0;
                        touchHadPrev = false;
                    }
                }
            }

            if (SdlNative.PowerSupported)
            {
                IntPtr joystick = SdlNative.GameControllerGetJoystick(gc);
                int level = SdlNative.JoystickCurrentPowerLevel(joystick);
                if (level < -1 || level > 4) { level = -1; }
                snap.Power = (PadPower)level;
            }

            return snap;
        }

        public void SetRumble(float strength, float durationSeconds)
        {
            if (handle == IntPtr.Zero || !SdlNative.RumbleSupported) { return; }
            ushort magnitude = (ushort)(MathHelper.Clamp(strength, 0f, 1f) * 65535f);
            try { SdlNative.GameControllerRumble(handle, magnitude, magnitude, (uint)(durationSeconds * 1000f)); } catch { }
        }

        public void StopRumble()
        {
            if (handle == IntPtr.Zero || !SdlNative.RumbleSupported) { return; }
            try { SdlNative.GameControllerRumble(handle, 0, 0, 1); } catch { }
        }

        private static float ConvertAxis(short raw)
        {
            return raw < 0 ? raw / 32768f : raw / 32767f;
        }

        private static float ConvertTrigger(short raw)
        {
            float value = raw / 32767f;
            return value < 0f ? 0f : value;
        }
    }
}
