using System;
using UnityEngine;

namespace GuideToNonstopHarvest.Prototype
{
    /// <summary>
    /// Species별 스탯. Secondary = 벼 드랍률 / 면화 섬유량 / 향료초 희귀도.
    /// </summary>
    [Serializable]
    public struct CropStats : IEquatable<CropStats>
    {
        public CropSpecies Species;
        public int GrowthSpeed;
        public int Secondary;

        public CropStats(CropSpecies species, int growthSpeed, int secondary)
        {
            Species = species;
            GrowthSpeed = ClampGrowth(species, growthSpeed);
            Secondary = ClampSecondary(species, secondary);
        }

        public static CropStats RiceStarter => new CropStats(CropSpecies.Rice, 0, 0);
        public static CropStats CottonStarter => new CropStats(CropSpecies.Cotton, 0, 0);
        public static CropStats SpiceleafStarter => new CropStats(CropSpecies.Spiceleaf, 0, 0);

        /// <summary>레거시 별칭 — 벼 스타터.</summary>
        public static CropStats Starter => RiceStarter;

        public static int GrowthMax(CropSpecies species) => species switch
        {
            CropSpecies.Rice => 5,
            CropSpecies.Cotton => 8,
            CropSpecies.Spiceleaf => 10,
            _ => 5
        };

        public static int SecondaryMax(CropSpecies species) => species switch
        {
            CropSpecies.Rice => 5,
            CropSpecies.Cotton => 12,
            CropSpecies.Spiceleaf => 20,
            _ => 5
        };

        public static int ClampGrowth(CropSpecies species, int value) =>
            Mathf.Clamp(value, 0, GrowthMax(species));

        public static int ClampSecondary(CropSpecies species, int value) =>
            Mathf.Clamp(value, 0, SecondaryMax(species));

        /// <summary>Mature 필요 틱 (Species별 공식).</summary>
        public int GrowTimeTicks
        {
            get
            {
                int g = GrowthSpeed;
                switch (Species)
                {
                    case CropSpecies.Cotton:
                        // 1200 - (1000 * 성장) / 8
                        return 1200 - (1000 * g) / 8;
                    case CropSpecies.Spiceleaf:
                        // 2400 - 200 * 성장
                        return 2400 - 200 * g;
                    default:
                        // 벼: 1200 - 200 * 성장
                        return 1200 - 200 * g;
                }
            }
        }

        /// <summary>상점 즉시 판매가(개당).</summary>
        public int ShopSellPricePerCrop
        {
            get
            {
                switch (Species)
                {
                    case CropSpecies.Cotton:
                        return 3;
                    case CropSpecies.Spiceleaf:
                        return 8 + Secondary / 2;
                    default:
                        return 10;
                }
            }
        }

        /// <summary>표시용 짧은 이름.</summary>
        public string SpeciesName => Species switch
        {
            CropSpecies.Cotton => "면화",
            CropSpecies.Spiceleaf => "향료초",
            _ => "벼"
        };

        public string SecondaryLabel => Species switch
        {
            CropSpecies.Cotton => "섬유",
            CropSpecies.Spiceleaf => "희귀",
            _ => "드랍"
        };

        public bool Equals(CropStats other) =>
            Species == other.Species
            && GrowthSpeed == other.GrowthSpeed
            && Secondary == other.Secondary;

        public override bool Equals(object obj) => obj is CropStats other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Species, GrowthSpeed, Secondary);

        public override string ToString() =>
            $"{SpeciesName} 성장{GrowthSpeed}/{SecondaryLabel}{Secondary}";
    }
}
