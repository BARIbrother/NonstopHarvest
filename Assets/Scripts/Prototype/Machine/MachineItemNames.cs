namespace GuideToNonstopHarvest.Prototype.Machine
{
    /// <summary>기계 아이템 표시명.</summary>
    public static class MachineItemNames
    {
        public static string Display(MachineItemId id) => id switch
        {
            MachineItemId.CropProduce => "작물(벼)",
            MachineItemId.Fertilizer => "비료",
            _ => id.ToString()
        };
    }
}
