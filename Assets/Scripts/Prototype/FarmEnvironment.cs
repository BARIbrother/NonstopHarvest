namespace GuideToNonstopHarvest.Prototype
{
    /// <summary>
    /// 밭 환경(양분·습도·공기) 필터.
    /// 프로토타입: 전 Species 생육 가능. 세부 수치는 tofill E01.
    /// </summary>
    public static class FarmEnvironment
    {
        /// <summary>현재 칸 환경에서 해당 Species가 자랄 수 있는지.</summary>
        public static bool CanGrow(CropSpecies species)
        {
            // (가정) 환경 수치 생략 — 구현된 Species는 모두 통과
            switch (species)
            {
                case CropSpecies.Rice:
                case CropSpecies.Cotton:
                case CropSpecies.Spiceleaf:
                    return true;
                default:
                    return false;
            }
        }
    }
}
