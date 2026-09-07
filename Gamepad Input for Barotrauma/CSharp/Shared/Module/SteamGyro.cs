namespace GamePadInput
{
    // Фасад над SteamInputApi для статуса и форсированного источника гиро.
    internal static class SteamGyro
    {
        public static bool Available { get { return SteamInputApi.HasControllers; } }
        public static bool HasMotion { get { return SteamInputApi.MotionAvailable; } }
        public static float Yaw { get { return SteamInputApi.MotionYaw; } }
        public static float Pitch { get { return SteamInputApi.MotionPitch; } }

        public static void Poll(double gameTime)
        {
            if (!SteamInputApi.EnsureReady(gameTime)) { return; }
            SteamInputApi.PollControllers(gameTime);
        }

        public static void Shutdown()
        {
            SteamInputApi.Shutdown();
        }
    }
}
