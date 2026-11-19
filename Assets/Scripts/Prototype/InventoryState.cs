using System;
using System.Collections.Generic;

namespace GuideToNonstopHarvest.Prototype
{
    /// <summary>
    /// 골드·벼·지지대·비료·씨앗·Mill/뿌리개 해금.
    /// </summary>
    public sealed class InventoryState
    {
        public const int RiceSellPrice = 10;
        public const int SupportBuyPrice = 5;
        public const int MillBuyPrice = 100;
        public const int SprayerBuyPrice = 0;
        public const int StartingSupports = 10;

        public int Gold { get; private set; }
        public int RiceCrops { get; private set; }
        public int Supports { get; private set; }
        public int Fertilizer { get; private set; }
        public int MillsInInventory { get; private set; }

        /// <summary>첫 비료 획득 후 상점 해금.</summary>
        public bool SprayerUnlocked { get; private set; }
        public bool OwnsSprayer { get; private set; }

        readonly List<SeedStack> _seeds = new List<SeedStack>();

        public IReadOnlyList<SeedStack> Seeds => _seeds;

        public InventoryState()
        {
            Gold = 0;
            RiceCrops = 0;
            Supports = StartingSupports;
            Fertilizer = 0;
            MillsInInventory = 0;
            SprayerUnlocked = false;
            OwnsSprayer = false;
            AddSeed(RiceStats.Starter, 1);
        }

        public void AddSeed(RiceStats stats, int count = 1)
        {
            if (count <= 0) return;
            for (int i = 0; i < _seeds.Count; i++)
            {
                if (_seeds[i].Stats.Equals(stats))
                {
                    _seeds[i] = new SeedStack(stats, _seeds[i].Count + count);
                    return;
                }
            }

            _seeds.Add(new SeedStack(stats, count));
        }

        public bool TryConsumeSeed(RiceStats stats)
        {
            for (int i = 0; i < _seeds.Count; i++)
            {
                if (!_seeds[i].Stats.Equals(stats) || _seeds[i].Count <= 0)
                    continue;

                int next = _seeds[i].Count - 1;
                if (next <= 0) _seeds.RemoveAt(i);
                else _seeds[i] = new SeedStack(stats, next);
                return true;
            }

            return false;
        }

        public bool TryConsumeSupport()
        {
            if (Supports <= 0) return false;
            Supports--;
            return true;
        }

        public bool TryConsumeFertilizer(int amount = FertilizerRules.ConsumePerSpray)
        {
            if (Fertilizer < amount) return false;
            Fertilizer -= amount;
            return true;
        }

        public void AddRiceCrop(int count = 1)
        {
            if (count > 0) RiceCrops += count;
        }

        public bool TryConsumeRiceCrop(int count = 1)
        {
            if (RiceCrops < count) return false;
            RiceCrops -= count;
            return true;
        }

        public void AddFertilizer(int count = 1)
        {
            if (count <= 0) return;
            Fertilizer += count;
            // 첫 비료 → 뿌리개 상점 해금
            if (!SprayerUnlocked)
                SprayerUnlocked = true;
        }

        public int SellAllRice()
        {
            if (RiceCrops <= 0) return 0;
            int gained = RiceCrops * RiceSellPrice;
            Gold += gained;
            RiceCrops = 0;
            return gained;
        }

        public bool TryBuySupport()
        {
            if (Gold < SupportBuyPrice) return false;
            Gold -= SupportBuyPrice;
            Supports++;
            return true;
        }

        public bool TryBuyMill()
        {
            if (Gold < MillBuyPrice) return false;
            Gold -= MillBuyPrice;
            MillsInInventory++;
            return true;
        }

        public bool TryConsumeMillItem()
        {
            if (MillsInInventory <= 0) return false;
            MillsInInventory--;
            return true;
        }

        public bool TryBuySprayer()
        {
            if (!SprayerUnlocked || OwnsSprayer) return false;
            if (Gold < SprayerBuyPrice) return false;
            Gold -= SprayerBuyPrice;
            OwnsSprayer = true;
            return true;
        }
    }

    [Serializable]
    public struct SeedStack
    {
        public RiceStats Stats;
        public int Count;

        public SeedStack(RiceStats stats, int count)
        {
            Stats = stats;
            Count = count;
        }
    }
}
