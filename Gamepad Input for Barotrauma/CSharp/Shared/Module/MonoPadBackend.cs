using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace GamePadInput
{
    internal class MonoPadBackend : IPadBackend
    {
        private readonly bool[] buttons = new bool[16];
        private bool connectLogged;

        public string Name { get { return "MonoGame"; } }

        public bool IsAvailable { get { return true; } }

        public bool IsConnected { get; private set; }

        public bool TryConnect()
        {
            Poll();
            return IsConnected;
        }

        public void Disconnect()
        {
        }

        public PadSnapshot Poll()
        {
            PadSnapshot snap = default;
            GamePadState state = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.None);
            snap.IsConnected = state.IsConnected;
            IsConnected = state.IsConnected;
            if (!state.IsConnected) { return snap; }

            GamePadCapabilities caps = GamePad.GetCapabilities(PlayerIndex.One);
            snap.DeviceName = string.IsNullOrEmpty(caps.DisplayName) ? "Gamepad" : caps.DisplayName;
            if (!connectLogged)
            {
                connectLogged = true;
                LuaCsLogger.LogMessage("GamepadInput: MonoGame sees device \"" + snap.DeviceName + "\"");
            }
            string lower = snap.DeviceName.ToLowerInvariant();
            snap.VendorId = lower.Contains("virtual") || lower.Contains("steam controller") ? (ushort)0x28DE : (ushort)0;
            snap.SdlType = -1;

            snap.LeftX = state.ThumbSticks.Left.X;
            snap.LeftY = state.ThumbSticks.Left.Y;
            snap.RightX = state.ThumbSticks.Right.X;
            snap.RightY = state.ThumbSticks.Right.Y;
            snap.LeftTrigger = state.Triggers.Left;
            snap.RightTrigger = state.Triggers.Right;

            buttons[(int)GPadButton.A] = state.Buttons.A == ButtonState.Pressed;
            buttons[(int)GPadButton.B] = state.Buttons.B == ButtonState.Pressed;
            buttons[(int)GPadButton.X] = state.Buttons.X == ButtonState.Pressed;
            buttons[(int)GPadButton.Y] = state.Buttons.Y == ButtonState.Pressed;
            buttons[(int)GPadButton.LB] = state.Buttons.LeftShoulder == ButtonState.Pressed;
            buttons[(int)GPadButton.RB] = state.Buttons.RightShoulder == ButtonState.Pressed;
            buttons[(int)GPadButton.LS] = state.Buttons.LeftStick == ButtonState.Pressed;
            buttons[(int)GPadButton.RS] = state.Buttons.RightStick == ButtonState.Pressed;
            buttons[(int)GPadButton.Start] = state.Buttons.Start == ButtonState.Pressed;
            buttons[(int)GPadButton.Back] = state.Buttons.Back == ButtonState.Pressed;
            buttons[(int)GPadButton.DPadUp] = state.DPad.Up == ButtonState.Pressed;
            buttons[(int)GPadButton.DPadDown] = state.DPad.Down == ButtonState.Pressed;
            buttons[(int)GPadButton.DPadLeft] = state.DPad.Left == ButtonState.Pressed;
            buttons[(int)GPadButton.DPadRight] = state.DPad.Right == ButtonState.Pressed;
            buttons[(int)GPadButton.LT] = snap.LeftTrigger > GPadInput.TriggerThreshold;
            buttons[(int)GPadButton.RT] = snap.RightTrigger > GPadInput.TriggerThreshold;
            snap.Buttons = buttons;

            snap.Power = PadPower.Unknown;
            return snap;
        }

        public void SetRumble(float strength, float durationSeconds)
        {
            try { GamePad.SetVibration(PlayerIndex.One, strength, strength); } catch { }
        }

        public void StopRumble()
        {
            try { GamePad.SetVibration(PlayerIndex.One, 0f, 0f); } catch { }
        }
    }
}
