using System;
using UnityEngine;

namespace GuideToNonstopHarvest.Prototype
{
    /// <summary>
    /// 벼 Species 전용 스탯. 범위 0~5.
    /// </summary>
    [Serializable]
    public struct RiceStats : IEquatable<RiceStats>
    {
        public const int Min = 0;
        public const int Max = 5;

        public int GrowthSpeed;
        public int DropRate;

        public RiceStats(int growthSpeed, int dropRate)
        {
            GrowthSpeed = Clamp(growthSpeed);
            DropRate = Clamp(dropRate);
        }

        public static RiceStats Starter => new RiceStats(0, 0);

        public static int Clamp(int value) => Mathf.Clamp(value, Min, Max);

        /// <summary>
        /// Mature까지 틱. growTimeTicks = 1200 - 200 * 성장속도
        /// (0→1200틱=60초, 5→200틱=10초. 전 작물 기본. 1초=20틱)
        /// </summary>
        public int GrowTimeTicks => 1200 - 200 * GrowthSpeed;

        /// <summary>씨앗 드랍 확률(레거시). 드랍률 효과 미정.</summary>
        public float SeedDropChance => 0.2f + 0.8f * (DropRate / 5f);

        public bool Equals(RiceStats other) =>
            GrowthSpeed == other.GrowthSpeed && DropRate == other.DropRate;

        public override bool Equals(object obj) => obj is RiceStats other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(GrowthSpeed, DropRate);

        public override string ToString() => $"성장{GrowthSpeed}/드랍{DropRate}";
    }
}
