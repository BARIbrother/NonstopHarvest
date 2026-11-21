using System.Collections.Generic;
using GuideToNonstopHarvest.Prototype.Machine;
using UnityEngine;

namespace GuideToNonstopHarvest.Prototype
{
    public enum CellKind
    {
        Empty,
        Crop,
        Machine
    }

    /// <summary>
    /// 칸 비료 상태. 성장 보너스·교접 하한을 각각 1회분으로 추적 (tofill F02 가정).
    /// </summary>
    public struct FertilizerState
    {
        public bool GrowthBonusActive;
        public bool BreedFloorActive;

        public bool AnyActive => GrowthBonusActive || BreedFloorActive;

        public static FertilizerState FullSpray => new FertilizerState
        {
            GrowthBonusActive = true,
            BreedFloorActive = true
        };
    }

    /// <summary>밭 한 칸.</summary>
    public sealed class FarmCell
    {
        public CellKind Kind = CellKind.Empty;
        public RiceStats Stats;
        public float ElapsedTicks;
        public bool StatsRevealed;
        public FertilizerState Fertilizer;
        public MachineBase Machine;

        public bool IsEmpty => Kind == CellKind.Empty;
        public bool IsMill => Kind == CellKind.Machine && Machine is MillMachine;

        public int EffectiveGrowTimeTicks
        {
            get
            {
                int baseTicks = Stats.GrowTimeTicks;
                if (Fertilizer.GrowthBonusActive)
                    baseTicks = Mathf.Max(1, baseTicks - FertilizerRules.GrowthBonusTicks);
                return baseTicks;
            }
        }

        public float Progress
        {
            get
            {
                if (Kind != CellKind.Crop) return 0f;
                int t = EffectiveGrowTimeTicks;
                return t <= 0 ? 1f : Mathf.Clamp01(ElapsedTicks / t);
            }
        }

        public int Stage
        {
            get
            {
                float p = Progress;
                if (p < 0.20f) return 0;
                if (p < 1f) return 1;
                return 2;
            }
        }

        public bool IsMature => Kind == CellKind.Crop && Stage >= 2;

        public void Clear()
        {
            Kind = CellKind.Empty;
            Stats = default;
            ElapsedTicks = 0f;
            StatsRevealed = false;
            Fertilizer = default;
            Machine = null;
        }

        public void Plant(RiceStats stats, bool statsRevealed = false)
        {
            Kind = CellKind.Crop;
            Stats = stats;
            ElapsedTicks = 0f;
            StatsRevealed = statsRevealed;
            Fertilizer = default;
            Machine = null;
        }

        public void PlaceMachine(MachineBase machine)
        {
            Kind = CellKind.Machine;
            Stats = default;
            ElapsedTicks = 0f;
            StatsRevealed = false;
            Fertilizer = default;
            Machine = machine;
        }
    }

    /// <summary>5×5 밭. 성장·심기·수확·교접·비료·기계(DungeonFront식 틱 페이즈).</summary>
    public sealed class FarmGrid
    {
        public const int Size = 5;

        readonly FarmCell[,] _cells = new FarmCell[Size, Size];
        float _tickAccumulator;

        static readonly Vector2Int[] Cardinals =
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0)
        };

        public FarmGrid()
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                _cells[x, y] = new FarmCell();
        }

        public FarmCell Get(int x, int y) => _cells[x, y];

        public bool InBounds(int x, int y) =>
            x >= 0 && y >= 0 && x < Size && y < Size;

        public void Tick(float deltaTime)
        {
            float tickDelta = GameTicks.Delta(deltaTime);

            // 작물 성장은 연속 틱 누적
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                FarmCell cell = _cells[x, y];
                if (cell.Kind == CellKind.Crop && !cell.IsMature)
                    cell.ElapsedTicks += tickDelta;
            }

            // 기계: DungeonFront TickManager처럼 정수 틱마다 Complete → Start
            _tickAccumulator += tickDelta;
            while (_tickAccumulator >= 1f)
            {
                _tickAccumulator -= 1f;
                TickMachinesOneStep();
            }
        }

        void TickMachinesOneStep()
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                MachineBase m = _cells[x, y].Machine;
                if (m == null) continue;
                m.TickCompleteProduction();
            }

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                MachineBase m = _cells[x, y].Machine;
                if (m == null) continue;
                m.TickStartProduction();
            }
        }

        public bool TryPlant(int x, int y, RiceStats stats, InventoryState inv)
        {
            if (!InBounds(x, y)) return false;
            FarmCell cell = _cells[x, y];
            if (!cell.IsEmpty) return false;
            if (!inv.TryConsumeSeed(stats)) return false;
            cell.Plant(stats, statsRevealed: true);
            return true;
        }

        public bool TryPlaceBreedingSupport(int x, int y, InventoryState inv)
        {
            if (!InBounds(x, y)) return false;
            FarmCell cell = _cells[x, y];
            if (!cell.IsEmpty) return false;

            List<RiceStats> parents = new List<RiceStats>(4);
            List<FarmCell> parentCells = new List<FarmCell>(4);
            bool anyBreedFloor = false;

            for (int i = 0; i < Cardinals.Length; i++)
            {
                int nx = x + Cardinals[i].x;
                int ny = y + Cardinals[i].y;
                if (!InBounds(nx, ny)) continue;
                FarmCell n = _cells[nx, ny];
                if (n.Kind != CellKind.Crop) continue;
                parents.Add(n.Stats);
                parentCells.Add(n);
                if (n.Fertilizer.BreedFloorActive)
                    anyBreedFloor = true;
            }

            if (parents.Count < 1) return false;
            if (!inv.TryConsumeSupport()) return false;

            RiceStats child = BreedingRules.Inherit(parents, anyBreedFloor);
            cell.Plant(child, statsRevealed: false);

            if (anyBreedFloor)
            {
                for (int i = 0; i < parentCells.Count; i++)
                {
                    if (!parentCells[i].Fertilizer.BreedFloorActive) continue;
                    var f = parentCells[i].Fertilizer;
                    f.BreedFloorActive = false;
                    parentCells[i].Fertilizer = f;
                }
            }

            return true;
        }

        public bool TryHarvest(int x, int y, InventoryState inv)
        {
            if (!InBounds(x, y)) return false;
            FarmCell cell = _cells[x, y];
            if (!cell.IsMature) return false;

            inv.AddRiceCrop(1);
            cell.ElapsedTicks = 0f;
            if (cell.Fertilizer.GrowthBonusActive)
            {
                var f = cell.Fertilizer;
                f.GrowthBonusActive = false;
                cell.Fertilizer = f;
            }

            return true;
        }

        /// <summary>작물 제거. 수확물·씨앗 반환 없음.</summary>
        public bool TryDigOut(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            FarmCell cell = _cells[x, y];
            if (cell.Kind != CellKind.Crop) return false;
            cell.Clear();
            return true;
        }

        public bool TrySprayFertilizer(int x, int y, InventoryState inv)
        {
            if (!InBounds(x, y)) return false;
            FarmCell cell = _cells[x, y];
            if (cell.Kind != CellKind.Crop) return false;
            if (!inv.TryConsumeFertilizer()) return false;
            cell.Fertilizer = FertilizerState.FullSpray;
            return true;
        }

        public bool TryPlaceMill(int x, int y, InventoryState inv)
        {
            if (!InBounds(x, y)) return false;
            FarmCell cell = _cells[x, y];
            if (!cell.IsEmpty) return false;
            if (!inv.TryConsumeMillItem()) return false;
            cell.PlaceMachine(new MillMachine());
            return true;
        }

        /// <summary>
        /// Mill 클릭: 아웃풋 수령 우선, 아니면 인벤 벼→인풋 투입.
        /// (DungeonFront 포트 이송 UI의 원클릭 축소판)
        /// </summary>
        public bool TryInteractMachine(int x, int y, InventoryState inv, out string message)
        {
            message = null;
            if (!InBounds(x, y)) return false;
            FarmCell cell = _cells[x, y];
            if (cell.Machine == null) return false;

            MachineBase mill = cell.Machine;

            if (mill.TryTakeOutput(MachineItemId.Fertilizer, 99, out int fertTaken) && fertTaken > 0)
            {
                inv.AddFertilizer(fertTaken);
                message = $"비료 +{fertTaken}";
                return true;
            }

            if (inv.RiceCrops > 0 && mill.TryInsert(MachineItemId.CropProduce, 1))
            {
                inv.TryConsumeRiceCrop(1);
                message = "벼 투입 → 자동 가공";
                return true;
            }

            if (mill.HasActiveWip)
            {
                message = $"가공 중 {Mathf.FloorToInt(mill.Progress01 * 100f)}%";
                return true;
            }

            message = inv.RiceCrops <= 0 ? "벼(작물) 필요" : "투입 실패";
            return false;
        }
    }
}
