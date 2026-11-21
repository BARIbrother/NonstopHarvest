namespace GuideToNonstopHarvest.Prototype.Machine
{
    /// <summary>
    /// 기계 입출력용 아이템 ID. DungeonFront ItemDef 역할의 축소판.
    /// </summary>
    public enum MachineItemId
    {
        None = 0,
        /// <summary>수확된 작물(벼 등). Mill 인풋.</summary>
        CropProduce = 1,
        /// <summary>비료. Mill 아웃풋.</summary>
        Fertilizer = 2
    }
}
