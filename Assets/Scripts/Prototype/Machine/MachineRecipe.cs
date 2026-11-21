namespace GuideToNonstopHarvest.Prototype.Machine
{
    /// <summary>
    /// 기계 레시피. DungeonFront Recipe SO의 런타임 축소판
    /// (인풋/아웃풋/recipeTime).
    /// </summary>
    public sealed class MachineRecipe
    {
        public readonly string Id;
        public readonly MachineItemStack[] Inputs;
        public readonly MachineItemStack[] Outputs;
        /// <summary>완성까지 필요한 진행도. workSpeed 누적이 이 값 이상이면 산출.</summary>
        public readonly int RecipeTime;

        public MachineRecipe(string id, MachineItemStack[] inputs, MachineItemStack[] outputs, int recipeTime)
        {
            Id = id;
            Inputs = inputs ?? System.Array.Empty<MachineItemStack>();
            Outputs = outputs ?? System.Array.Empty<MachineItemStack>();
            RecipeTime = recipeTime;
        }
    }

    public readonly struct MachineItemStack
    {
        public readonly MachineItemId Id;
        public readonly int Count;

        public MachineItemStack(MachineItemId id, int count)
        {
            Id = id;
            Count = count;
        }
    }
}
