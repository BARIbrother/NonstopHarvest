using System.Collections.Generic;
using UnityEngine;

namespace GuideToNonstopHarvest.Prototype
{
    /// <summary>
    /// 교배 상속: 동종만. 부모 평균 + RandomInt(-2,4), Species 범위 클램프.
    /// 비료 하한: 평균이 캡 미만이면 자식이 평균 아래로 안 떨어짐.
    /// </summary>
    public static class BreedingRules
    {
        public static CropStats Inherit(IReadOnlyList<CropStats> parents, bool fertilizerFloor)
        {
            if (parents == null || parents.Count == 0)
                return CropStats.RiceStarter;

            // 프로토: 부모 Species가 모두 같아야 함. 다르면 첫 부모 Species 기준.
            CropSpecies species = parents[0].Species;
            for (int i = 1; i < parents.Count; i++)
            {
                if (parents[i].Species != species)
                    species = parents[0].Species; // 이종 교차(면화×콩→향료)는 미구현 stub
            }

            float growthSum = 0f;
            float secondarySum = 0f;
            for (int i = 0; i < parents.Count; i++)
            {
                growthSum += parents[i].GrowthSpeed;
                secondarySum += parents[i].Secondary;
            }

            float growthAvg = growthSum / parents.Count;
            float secondaryAvg = secondarySum / parents.Count;

            int growth = ResolveStat(species, growthAvg, fertilizerFloor, isGrowth: true);
            int secondary = ResolveStat(species, secondaryAvg, fertilizerFloor, isGrowth: false);
            return new CropStats(species, growth, secondary);
        }

        static int ResolveStat(CropSpecies species, float parentAvg, bool fertilizerFloor, bool isGrowth)
        {
            int rolled = Mathf.RoundToInt(parentAvg + RandomIntInclusive(-2, 4));
            int value = isGrowth
                ? CropStats.ClampGrowth(species, rolled)
                : CropStats.ClampSecondary(species, rolled);
            if (!fertilizerFloor) return value;

            int avgInt = Mathf.RoundToInt(parentAvg);
            if (avgInt < FertilizerRules.BreedFloorCap)
            {
                value = isGrowth
                    ? Mathf.Max(value, CropStats.ClampGrowth(species, avgInt))
                    : Mathf.Max(value, CropStats.ClampSecondary(species, avgInt));
            }

            return value;
        }

        public static int RandomIntInclusive(int minInclusive, int maxInclusive) =>
            Random.Range(minInclusive, maxInclusive + 1);

        /// <summary>면화 섬유량 → 수확 수량.</summary>
        public static int CottonHarvestCount(int fiber)
        {
            int lo = 1 + fiber * 2 / 12;
            int hi = 1 + fiber * 3 / 12;
            return RandomIntInclusive(lo, hi);
        }
    }
}
