namespace GamePadInput
{
    public enum PadPower
    {
        Unknown = -1,
        Empty = 0,
        Low = 1,
        Medium = 2,
        Full = 3,
        Wired = 4
    }

    public struct PadSnapshot
    {
        public bool IsConnected;
        public string DeviceName;

        public int SdlType;
        public ushort VendorId;

        public float LeftX;
        public float LeftY;
        public float RightX;
        public float RightY;
        public float LeftTrigger;
        public float RightTrigger;

        public bool[] Buttons;

        public bool HasGyro;
        public bool GyroAvailable;
        public float GyroPitch;
        public float GyroYaw;

        public bool HasTouchpad;
        public bool TouchpadDown;
        public float TouchDX;
        public float TouchDY;

        public PadPower Power;

        public bool IsSteamVirtual
        {
            get { return SdlType == 6 || (SdlType < 0 && VendorId == 0x28DE); }
        }

        public bool IsDown(GPadButton button)
        {
            return Buttons != null && Buttons[(int)button];
        }
    }

    internal interface IPadBackend
    {
        string Name { get; }
        bool IsAvailable { get; }
        bool IsConnected { get; }
        bool TryConnect();
        void Disconnect();
        PadSnapshot Poll();
        void SetRumble(float strength, float durationSeconds);
        void StopRumble();
    }
}
