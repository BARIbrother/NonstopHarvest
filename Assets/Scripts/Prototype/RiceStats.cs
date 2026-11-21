using System;

namespace GuideToNonstopHarvest.Prototype
{
    /// <summary>
    /// 레거시 별칭. 신규 코드는 <see cref="CropStats"/> 사용.
    /// 벼 Species의 Growth/Drop = CropStats Growth/Secondary.
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
            GrowthSpeed = CropStats.ClampGrowth(CropSpecies.Rice, growthSpeed);
            DropRate = CropStats.ClampSecondary(CropSpecies.Rice, dropRate);
        }

        public static RiceStats Starter => new RiceStats(0, 0);

        public CropStats AsCrop => new CropStats(CropSpecies.Rice, GrowthSpeed, DropRate);

        public int GrowTimeTicks => AsCrop.GrowTimeTicks;

        public bool Equals(RiceStats other) =>
            GrowthSpeed == other.GrowthSpeed && DropRate == other.DropRate;

        public override bool Equals(object obj) => obj is RiceStats other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(GrowthSpeed, DropRate);

        public override string ToString() => AsCrop.ToString();
    }
}
