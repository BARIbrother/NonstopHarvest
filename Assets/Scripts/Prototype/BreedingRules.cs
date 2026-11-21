using System.Collections.Generic;
using UnityEngine;

namespace GuideToNonstopHarvest.Prototype
{
    /// <summary>
    /// 교배: Mature 부모 → 자식 Species 후보 → 환경 필터 → 랜덤 선정 → 스탯 상속.
    /// 상속: 부모 평균 + RandomInt(-2,4), Species 범위 클램프.
    /// 비료 하한: 평균이 캡 미만이면 자식이 평균 아래로 안 떨어짐.
    /// </summary>
    public static class BreedingRules
    {
        /// <summary>인접 부모로부터 생길 수 있는 자식 Species 후보(중복 제거).</summary>
        public static List<CropSpecies> CollectOffspringCandidates(IReadOnlyList<CropStats> parents)
        {
            var set = new HashSet<CropSpecies>();
            if (parents == null || parents.Count == 0)
                return new List<CropSpecies>();

            for (int i = 0; i < parents.Count; i++)
                set.Add(parents[i].Species); // 동종 자식 가능

            // 벼 유전자 풀(감자·당근)·면화×콩→향료는 프로토 Species 미포함 → 후보에 안 넣음
            return new List<CropSpecies>(set);
        }

        /// <summary>환경에서 자랄 수 있는 Species만 남김.</summary>
        public static List<CropSpecies> FilterByEnvironment(IReadOnlyList<CropSpecies> candidates)
        {
            var result = new List<CropSpecies>();
            if (candidates == null) return result;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (FarmEnvironment.CanGrow(candidates[i]))
                    result.Add(candidates[i]);
            }

            return result;
        }

        /// <summary>필터된 풀에서 균등 랜덤 1종.</summary>
        public static bool TryPickSpecies(IReadOnlyList<CropSpecies> pool, out CropSpecies species)
        {
            if (pool == null || pool.Count == 0)
            {
                species = default;
                return false;
            }

            species = pool[Random.Range(0, pool.Count)];
            return true;
        }

        /// <summary>지정 Species로 스탯 상속.</summary>
        public static CropStats InheritAs(CropSpecies species, IReadOnlyList<CropStats> parents, bool fertilizerFloor)
        {
            if (parents == null || parents.Count == 0)
                return new CropStats(species, 0, 0);

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

        /// <summary>레거시: 첫 부모 Species 기준 상속.</summary>
        public static CropStats Inherit(IReadOnlyList<CropStats> parents, bool fertilizerFloor)
        {
            if (parents == null || parents.Count == 0)
                return CropStats.RiceStarter;
            return InheritAs(parents[0].Species, parents, fertilizerFloor);
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
