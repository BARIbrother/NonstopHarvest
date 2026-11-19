using System.Collections.Generic;
using UnityEngine;

namespace GuideToNonstopHarvest.Prototype
{
    /// <summary>
    /// 교배 상속: 부모 평균 + RandomInt(-2,4), 0~5 클램프.
    /// 비료 하한: 평균이 캡 미만이면 자식이 평균 아래로 안 떨어짐.
    /// </summary>
    public static class BreedingRules
    {
        public static RiceStats Inherit(IReadOnlyList<RiceStats> parents, bool fertilizerFloor)
        {
            if (parents == null || parents.Count == 0)
                return RiceStats.Starter;

            float growthSum = 0f;
            float dropSum = 0f;
            for (int i = 0; i < parents.Count; i++)
            {
                growthSum += parents[i].GrowthSpeed;
                dropSum += parents[i].DropRate;
            }

            float growthAvg = growthSum / parents.Count;
            float dropAvg = dropSum / parents.Count;

            int growth = ResolveStat(growthAvg, fertilizerFloor);
            int drop = ResolveStat(dropAvg, fertilizerFloor);
            return new RiceStats(growth, drop);
        }

        static int ResolveStat(float parentAvg, bool fertilizerFloor)
        {
            int rolled = Mathf.RoundToInt(parentAvg + RandomIntInclusive(-2, 4));
            int value = RiceStats.Clamp(rolled);
            if (!fertilizerFloor) return value;

            int avgInt = Mathf.RoundToInt(parentAvg);
            // 캡 도달 전: 부모 평균 미만 금지
            if (avgInt < FertilizerRules.BreedFloorCap)
                value = Mathf.Max(value, RiceStats.Clamp(avgInt));
            return value;
        }

        public static int RandomIntInclusive(int minInclusive, int maxInclusive) =>
            Random.Range(minInclusive, maxInclusive + 1);
    }
}
