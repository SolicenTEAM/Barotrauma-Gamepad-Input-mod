using System;

namespace GamePadInput
{
    // Кнопки/оси — приоритетно из наших Steam Input действий (манифест), недостающее —
    // от обёрнутого бэкенда (эмулированный пад); гиро, хаптика и имя устройства — из Steam API.
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

                if (SteamInputApi.ActionsAvailable)
                {
                    // Живые действия Steam Input — приоритетный источник (надёжнее чтения
                    // эмуляции через MonoGame/XInput): кнопки/оси из наших действий,
                    // недостающие компоненты добираем из эмулированного пада.
                    if (SteamInputApi.ActionsAlive)
                    {
                        ApplySteamActions(ref snap);
                    }
                    else if (!snap.IsConnected)
                    {
                        // виртуального пада нет (клавиатурно-мышиный шаблон) — пробуем действия как есть
                        ApplySteamActions(ref snap);
                        if (!SteamInputApi.ActionsAlive) { snap.IsConnected = false; }
                    }
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

        private static void ApplySteamActions(ref PadSnapshot snap)
        {
            bool innerConnected = snap.IsConnected;
            bool[] innerButtons = snap.Buttons;

            bool[] buttons = new bool[16];
            bool[] steamButtons = SteamInputApi.ActionButtons;
            for (int i = 0; i < 16; i++)
            {
                buttons[i] = SteamInputApi.IsDigitalBound(i)
                    ? steamButtons[i]
                    : (innerConnected && innerButtons != null && innerButtons.Length == 16 && innerButtons[i]);
            }
            snap.Buttons = buttons;
            snap.LeftTrigger = buttons[(int)GPadButton.LT] ? 1f : 0f;
            snap.RightTrigger = buttons[(int)GPadButton.RT] ? 1f : 0f;

            if (SteamInputApi.HasLeftStickAction)
            {
                snap.LeftX = SteamInputApi.ActionLeftX;
                snap.LeftY = SteamInputApi.ActionLeftY;
            }
            if (SteamInputApi.HasRightStickAction)
            {
                snap.RightX = SteamInputApi.ActionRightX;
                snap.RightY = SteamInputApi.ActionRightY;
            }
            snap.IsConnected = true;
            snap.SteamActionsInput = true;
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
