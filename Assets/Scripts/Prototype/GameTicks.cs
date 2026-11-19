using UnityEngine;

namespace GuideToNonstopHarvest.Prototype
{
    /// <summary>
    /// 게임 시간 단위. 1초 = 20틱.
    /// </summary>
    public static class GameTicks
    {
        public const int PerSecond = 20;

        public static float FromSeconds(float seconds) => seconds * PerSecond;

        public static float ToSeconds(float ticks) => ticks / PerSecond;

        /// <summary>이번 프레임 deltaTime이 몇 틱인지.</summary>
        public static float Delta(float deltaTime) => deltaTime * PerSecond;
    }
}
