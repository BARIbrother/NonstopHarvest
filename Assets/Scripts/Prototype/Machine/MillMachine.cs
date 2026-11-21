namespace GuideToNonstopHarvest.Prototype.Machine
{
    /// <summary>
    /// 제분기(Mill). DungeonFront Smelter처럼 틱 자동 가공.
    /// 레시피: 작물×1 → 비료×1, recipeTime 100.
    /// 1초=20틱, workSpeed 1 → 초당 진행 20 → 100/20 = 5초.
    /// </summary>
    public sealed class MillMachine : MachineBase
    {
        public static readonly MachineRecipe CropToFertilizer = new MachineRecipe(
            id: "mill_crop_to_fertilizer",
            inputs: new[] { new MachineItemStack(MachineItemId.CropProduce, 1) },
            outputs: new[] { new MachineItemStack(MachineItemId.Fertilizer, 1) },
            recipeTime: 100);

        public MillMachine()
        {
            CurrentRecipe = CropToFertilizer;
            WorkSpeed = 1;
        }
    }
}
