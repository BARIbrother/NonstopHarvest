using System;
using System.Collections.Generic;

namespace GuideToNonstopHarvest.Prototype
{
    /// <summary>
    /// 골드·작물·지지대·비료·씨앗·Mill/뿌리개 해금.
    /// 교역: 벼 판매 추적·면화/향료초 스택.
    /// </summary>
    public sealed class InventoryState
    {
        public const int RiceSellPrice = 10;
        public const int StarterSeedBuyPrice = 20;
        public const int SupportBuyPrice = 5;
        public const int MillBuyPrice = 100;
        public const int SprayerBuyPrice = 0;
        public const int StartingSupports = 10;

        public int Gold { get; private set; }
        public int Supports { get; private set; }
        public int Fertilizer { get; private set; }
        public int MillsInInventory { get; private set; }

        /// <summary>첫 비료 획득 후 상점 해금.</summary>
        public bool SprayerUnlocked { get; private set; }
        public bool OwnsSprayer { get; private set; }

        /// <summary>벼(작물) 상점 판매 1회 이상 — 교역 해금 T01.</summary>
        public bool RiceSoldOnce { get; private set; }

        readonly Dictionary<CropSpecies, int> _crops = new Dictionary<CropSpecies, int>();
        readonly List<SeedStack> _seeds = new List<SeedStack>();

        public IReadOnlyList<SeedStack> Seeds => _seeds;

        /// <summary>레거시 접근자 — 벼 작물 수량.</summary>
        public int RiceCrops => GetCropCount(CropSpecies.Rice);

        public InventoryState()
        {
            Gold = 0;
            Supports = StartingSupports;
            Fertilizer = 0;
            MillsInInventory = 0;
            SprayerUnlocked = false;
            OwnsSprayer = false;
            RiceSoldOnce = false;
            AddSeed(CropStats.RiceStarter, 1);
        }

        public int GetCropCount(CropSpecies species) =>
            _crops.TryGetValue(species, out int n) ? n : 0;

        public void AddCrop(CropSpecies species, int count = 1)
        {
            if (count <= 0) return;
            _crops.TryGetValue(species, out int cur);
            _crops[species] = cur + count;
        }

        public bool TryConsumeCrop(CropSpecies species, int count = 1)
        {
            if (count <= 0) return true;
            int have = GetCropCount(species);
            if (have < count) return false;
            int next = have - count;
            if (next <= 0) _crops.Remove(species);
            else _crops[species] = next;
            return true;
        }

        public void AddGold(int amount)
        {
            if (amount > 0) Gold += amount;
        }

        public void AddSeed(CropStats stats, int count = 1)
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

        public bool TryConsumeSeed(CropStats stats)
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

        public void AddRiceCrop(int count = 1) => AddCrop(CropSpecies.Rice, count);

        public bool TryConsumeRiceCrop(int count = 1) => TryConsumeCrop(CropSpecies.Rice, count);

        public void AddFertilizer(int count = 1)
        {
            if (count <= 0) return;
            Fertilizer += count;
            if (!SprayerUnlocked)
                SprayerUnlocked = true;
        }

        public int SellAllRice()
        {
            int n = GetCropCount(CropSpecies.Rice);
            if (n <= 0) return 0;
            int gained = n * RiceSellPrice;
            Gold += gained;
            _crops.Remove(CropSpecies.Rice);
            RiceSoldOnce = true;
            return gained;
        }

        public int SellAllOf(CropSpecies species)
        {
            if (species == CropSpecies.Rice) return SellAllRice();
            int n = GetCropCount(species);
            if (n <= 0) return 0;
            // 면화·향료초: 스타터 스탯 기준 상점가 (스택에 스탯 없음)
            int unit = new CropStats(species, 0, 0).ShopSellPricePerCrop;
            int gained = n * unit;
            Gold += gained;
            _crops.Remove(species);
            return gained;
        }

        public bool TryBuyStarterRiceSeed(int count = 1)
        {
            if (count <= 0) return false;
            int cost = StarterSeedBuyPrice * count;
            if (Gold < cost) return false;
            Gold -= cost;
            AddSeed(CropStats.RiceStarter, count);
            return true;
        }

        public bool TryBuyCottonSeed(TradeSystem trade, int count = 1)
        {
            if (trade == null || !trade.CottonShopUnlocked || count <= 0) return false;
            int cost = TradeSystem.CottonSeedPrice * count;
            if (Gold < cost) return false;
            Gold -= cost;
            AddSeed(CropStats.CottonStarter, count);
            return true;
        }

        public bool TryBuySpiceleafSeed(TradeSystem trade, int count = 1)
        {
            if (trade == null || !trade.SpiceleafShopUnlocked || count <= 0) return false;
            int cost = TradeSystem.SpiceleafSeedPrice * count;
            if (Gold < cost) return false;
            Gold -= cost;
            AddSeed(CropStats.SpiceleafStarter, count);
            return true;
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
        public CropStats Stats;
        public int Count;

        public SeedStack(CropStats stats, int count)
        {
            Stats = stats;
            Count = count;
        }
    }
}
