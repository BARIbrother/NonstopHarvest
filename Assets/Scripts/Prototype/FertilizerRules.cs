namespace GuideToNonstopHarvest.Prototype
{
    /// <summary>
    /// 비료 수치 (프로토 가정, tofill F02~F08).
    /// 교접 하한 캡·성장 보너스·살포 소비.
    /// </summary>
    public static class FertilizerRules
    {
        /// <summary>부모 평균이 이 값 미만일 때만 하한 보호.</summary>
        public const int BreedFloorCap = 3;

        /// <summary>스탯 독립 성장 보너스 (필요 틱 감소). 40틱=2초.</summary>
        public const int GrowthBonusTicks = 40;

        /// <summary>칸 살포 1회당 비료 소비.</summary>
        public const int ConsumePerSpray = 1;

        /// <summary>살포 호버 필요 틱. 10틱=0.5초.</summary>
        public const float SprayHoverTicks = 10f;
    }
}
