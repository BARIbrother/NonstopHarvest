using System.Collections.Generic;
using UnityEngine;

namespace GuideToNonstopHarvest.Prototype
{
    public enum CellKind
    {
        Empty,
        Crop,
        Mill
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

        // Mill
        public bool MillProcessing;
        public bool MillOutputReady;
        public float MillElapsedTicks;
        public const float MillProcessTicks = 100f;

        public bool IsEmpty => Kind == CellKind.Empty;

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

        public float MillProgress =>
            Kind == CellKind.Mill && MillProcessing
                ? Mathf.Clamp01(MillElapsedTicks / MillProcessTicks)
                : 0f;

        public void Clear()
        {
            Kind = CellKind.Empty;
            Stats = default;
            ElapsedTicks = 0f;
            StatsRevealed = false;
            Fertilizer = default;
            MillProcessing = false;
            MillOutputReady = false;
            MillElapsedTicks = 0f;
        }

        public void Plant(RiceStats stats, bool statsRevealed = false)
        {
            Kind = CellKind.Crop;
            Stats = stats;
            ElapsedTicks = 0f;
            StatsRevealed = statsRevealed;
            Fertilizer = default;
            MillProcessing = false;
            MillOutputReady = false;
            MillElapsedTicks = 0f;
        }

        public void PlaceMill()
        {
            Kind = CellKind.Mill;
            Stats = default;
            ElapsedTicks = 0f;
            StatsRevealed = false;
            Fertilizer = default;
            MillProcessing = false;
            MillOutputReady = false;
            MillElapsedTicks = 0f;
        }
    }

    /// <summary>5×5 밭. 성장·심기·수확·교접·비료·Mill.</summary>
    public sealed class FarmGrid
    {
        public const int Size = 5;

        readonly FarmCell[,] _cells = new FarmCell[Size, Size];

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
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                FarmCell cell = _cells[x, y];
                if (cell.Kind == CellKind.Crop && !cell.IsMature)
                    cell.ElapsedTicks += tickDelta;

                if (cell.Kind == CellKind.Mill && cell.MillProcessing)
                {
                    cell.MillElapsedTicks += tickDelta;
                    if (cell.MillElapsedTicks >= FarmCell.MillProcessTicks)
                    {
                        cell.MillProcessing = false;
                        cell.MillElapsedTicks = 0f;
                        cell.MillOutputReady = true;
                    }
                }
            }
        }

        /// <summary>Mill 산출 대기면 비료 지급.</summary>
        public bool TryCollectMillOutput(int x, int y, InventoryState inv)
        {
            if (!InBounds(x, y)) return false;
            FarmCell cell = _cells[x, y];
            if (cell.Kind != CellKind.Mill || !cell.MillOutputReady) return false;
            inv.AddFertilizer(1);
            cell.MillOutputReady = false;
            return true;
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

            // 교접 하한 1회분 소비
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
            // 1사이클 성장 보너스 소모
            if (cell.Fertilizer.GrowthBonusActive)
            {
                var f = cell.Fertilizer;
                f.GrowthBonusActive = false;
                cell.Fertilizer = f;
            }

            return true;
        }

        /// <summary>작물 칸에 비료 살포. 이미 있으면 지속만 갱신.</summary>
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
            cell.PlaceMill();
            return true;
        }

        /// <summary>Mill에 벼 1개 투입해 가공 시작.</summary>
        public bool TryStartMill(int x, int y, InventoryState inv)
        {
            if (!InBounds(x, y)) return false;
            FarmCell cell = _cells[x, y];
            if (cell.Kind != CellKind.Mill) return false;
            if (cell.MillProcessing || cell.MillOutputReady) return false;
            if (!inv.TryConsumeRiceCrop(1)) return false;
            cell.MillProcessing = true;
            cell.MillElapsedTicks = 0f;
            return true;
        }
    }
}
