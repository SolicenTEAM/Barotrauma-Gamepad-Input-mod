using System;

namespace GamePadInput
{
    // Кнопки/оси/тачпад/батарея — от обёрнутого бэкенда (Steam отдаёт виртуальный пад),
    // гиро, хаптика и имя устройства — напрямую из Steam Input API.
    internal class SteamPadBackend : IPadBackend
    {
        private readonly IPadBackend inner;

        public SteamPadBackend(IPadBackend inner)
        {
            this.inner = inner;
        }

        public string Name { get { return "Steam Input"; } }

        public bool IsAvailable { get { return inner.IsAvailable; } }

        public bool IsConnected
        {
            get { return inner.IsConnected || (SteamInputApi.HasControllers && SteamInputApi.ActionsAvailable); }
        }

        public bool TryConnect()
        {
            return inner.TryConnect();
        }

        public void Disconnect()
        {
            inner.Disconnect();
        }

        public PadSnapshot Poll()
        {
            SteamInputApi.PollControllers(-1.0);
            PadSnapshot snap = inner.Poll();

            if (SteamInputApi.HasControllers)
            {
                snap.SdlType = 6;
                snap.VendorId = 0x28DE;
                snap.DeviceName = "Steam Input: " + SteamInputApi.GetInputTypeName();

                if (!snap.IsConnected && SteamInputApi.ActionsAvailable)
                {
                    // Steam Input прячет сырой пад — кнопки/оси приходят из наших действий
                    snap.IsConnected = true;
                    bool[] buttons = new bool[16];
                    Array.Copy(SteamInputApi.ActionButtons, buttons, 16);
                    snap.Buttons = buttons;
                    snap.LeftX = SteamInputApi.ActionLeftX;
                    snap.LeftY = SteamInputApi.ActionLeftY;
                    snap.RightX = SteamInputApi.ActionRightX;
                    snap.RightY = SteamInputApi.ActionRightY;
                    snap.LeftTrigger = SteamInputApi.ActionButtons[(int)GPadButton.LT] ? 1f : 0f;
                    snap.RightTrigger = SteamInputApi.ActionButtons[(int)GPadButton.RT] ? 1f : 0f;
                }

                if (SteamInputApi.MotionAvailable)
                {
                    snap.GyroAvailable = true;
                    snap.HasGyro = true;
                    snap.GyroPitch = SteamInputApi.MotionPitch;
                    snap.GyroYaw = SteamInputApi.MotionYaw;
                }
            }
            return snap;
        }

        public void SetRumble(float strength, float durationSeconds)
        {
            if (!SteamInputApi.TriggerHaptic(strength, durationSeconds))
            {
                inner.SetRumble(strength, durationSeconds);
            }
        }

        public void StopRumble()
        {
            if (!SteamInputApi.StopHaptic())
            {
                inner.StopRumble();
            }
        }
    }
}
