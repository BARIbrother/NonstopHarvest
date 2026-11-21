namespace GuideToNonstopHarvest.Prototype.Machine
{
    /// <summary>
    /// DungeonFront Machine 생산 루프 축소판.
    /// TickComplete(진행·산출) → TickStart(인풋 소비·WIP 시작).
    /// </summary>
    public abstract class MachineBase
    {
        public readonly MachinePort InputPort = new MachinePort();
        public readonly MachinePort OutputPort = new MachinePort();

        public MachineRecipe CurrentRecipe { get; protected set; }

        /// <summary>틱당 진행도. DungeonFront workSpeed. (1초=20틱 기준 Mill 기본 1)</summary>
        public int WorkSpeed { get; protected set; } = 1;

        public int ProgressTicks { get; private set; }
        public bool HasActiveWip { get; private set; }

        public float Progress01
        {
            get
            {
                if (!HasActiveWip || CurrentRecipe == null || CurrentRecipe.RecipeTime <= 0)
                    return 0f;
                return UnityEngine.Mathf.Clamp01((float)ProgressTicks / CurrentRecipe.RecipeTime);
            }
        }

        public bool OutputReady =>
            OutputPort.GetCount(MachineItemId.Fertilizer) > 0
            || OutputPort.GetCount(MachineItemId.CropProduce) > 0;

        /// <summary>생산 완료 페이즈: workSpeed만큼 진행, recipeTime 도달 시 아웃풋.</summary>
        public bool TickCompleteProduction()
        {
            if (!HasActiveWip || CurrentRecipe == null) return false;

            if (ProgressTicks < CurrentRecipe.RecipeTime)
            {
                ProgressTicks += WorkSpeed;
                if (ProgressTicks < CurrentRecipe.RecipeTime)
                    return false;
            }

            if (!OutputPort.CanFit(CurrentRecipe.Outputs))
                return false;
            if (!OutputPort.TryAddOutputs(CurrentRecipe.Outputs))
                return false;

            ProgressTicks = 0;
            HasActiveWip = false;
            return true;
        }

        /// <summary>생산 시작 페이즈: 인풋 매칭·소비 후 WIP.</summary>
        public void TickStartProduction()
        {
            if (HasActiveWip || CurrentRecipe == null) return;
            if (!InputPort.MatchesRecipe(CurrentRecipe.Inputs)) return;
            if (!OutputPort.CanFit(CurrentRecipe.Outputs)) return;
            if (!InputPort.TryConsume(CurrentRecipe.Inputs)) return;

            HasActiveWip = true;
            ProgressTicks = 0;
        }

        public bool TryInsert(MachineItemId id, int count) => InputPort.TryAdd(id, count);

        public bool TryTakeOutput(MachineItemId id, int count, out int taken) =>
            OutputPort.TryTake(id, count, out taken);

        public bool TryTakeAnyOutput(out MachineItemId id, out int count) =>
            OutputPort.TryTakeFirst(out id, out count);

        /// <summary>인풋 포트에서 회수 (기계 UI).</summary>
        public bool TryTakeInput(MachineItemId id, int count, out int taken) =>
            InputPort.TryTake(id, count, out taken);
    }
}
