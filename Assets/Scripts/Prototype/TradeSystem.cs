using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuideToNonstopHarvest.Prototype
{
    public enum TradeOrderStatus
    {
        Waiting,
        Active,
        Completed,
        Expired
    }

    public enum TradeReputationTier
    {
        Apprentice = 0, // 0~9
        Regular = 1,    // 10~24
        Merchant = 2,   // 25~49
        Exclusive = 3   // 50+
    }

    public enum TradeUiScreen
    {
        Board,
        Detail,
        Deliver,
        Reward
    }

    /// <summary>
    /// 교역 주문 1건. tofill 기본 제안(T01~T14) 적용.
    /// </summary>
    public sealed class TradeOrder
    {
        public int Id;
        public string Title;
        public CropSpecies Species;
        public int RequiredCount;
        public int DeliveredCount;
        public int RewardGold;
        public int ReputationReward;
        public int TimeLimitTicks;
        public int RemainingTicks;
        public TradeOrderStatus Status;
        public bool TimerStarted;
    }

    /// <summary>
    /// D1 교역: 해금·보드·수락·시한·납품·평판.
    /// 1차: 전량 납품(T04), 시한은 수락 후(T13), 만료 평판-1(T05).
    /// </summary>
    public sealed class TradeSystem
    {
        public const int WaitingSlots = 3;      // T02
        public const int MaxActive = 2;         // T03
        public const int UnlockGoldThreshold = 30; // T01
        public const int CottonRepUnlock = 25;  // T11
        public const int SpiceleafRepUnlock = 50; // T12
        public const int CottonSeedPrice = 35;
        public const int SpiceleafSeedPrice = 60;

        // T08 티어 경계
        public const int TierRegularAt = 10;
        public const int TierMerchantAt = 25;
        public const int TierExclusiveAt = 50;

        readonly List<TradeOrder> _waiting = new List<TradeOrder>();
        readonly List<TradeOrder> _active = new List<TradeOrder>();

        int _nextId = 1;
        bool _tutorialSpawned;
        TradeOrder _lastRewardOrder;
        int _lastRewardGold;
        int _lastRewardRep;
        string _lastRewardMessage = "";

        public bool Unlocked { get; private set; }
        public int Reputation { get; private set; }
        public IReadOnlyList<TradeOrder> Waiting => _waiting;
        public IReadOnlyList<TradeOrder> Active => _active;
        public TradeOrder LastRewardOrder => _lastRewardOrder;
        public int LastRewardGold => _lastRewardGold;
        public int LastRewardRep => _lastRewardRep;
        public string LastRewardMessage => _lastRewardMessage;

        public TradeReputationTier Tier
        {
            get
            {
                if (Reputation >= TierExclusiveAt) return TradeReputationTier.Exclusive;
                if (Reputation >= TierMerchantAt) return TradeReputationTier.Merchant;
                if (Reputation >= TierRegularAt) return TradeReputationTier.Regular;
                return TradeReputationTier.Apprentice;
            }
        }

        public string TierName => Tier switch
        {
            TradeReputationTier.Exclusive => "특약",
            TradeReputationTier.Merchant => "상인",
            TradeReputationTier.Regular => "단골",
            _ => "견습"
        };

        /// <summary>T08 보상 배율.</summary>
        public float RewardMultiplier => Tier switch
        {
            TradeReputationTier.Exclusive => 1.35f,
            TradeReputationTier.Merchant => 1.2f,
            TradeReputationTier.Regular => 1.1f,
            _ => 1f
        };

        public bool CottonShopUnlocked => Unlocked && Reputation >= CottonRepUnlock;
        public bool SpiceleafShopUnlocked => Unlocked && Reputation >= SpiceleafRepUnlock;

        public void TryUnlock(InventoryState inv)
        {
            if (Unlocked || inv == null) return;
            // T01: 벼 판매 1회 또는 골드 ≥ 30
            if (inv.RiceSoldOnce || inv.Gold >= UnlockGoldThreshold)
                Unlock(inv);
        }

        void Unlock(InventoryState inv)
        {
            Unlocked = true;
            if (!_tutorialSpawned)
            {
                _tutorialSpawned = true;
                // 튜토리얼: 벼×3, 넉넉한 시한
                var tutorial = CreateOrder(
                    "튜토리얼 · 마을 식량",
                    CropSpecies.Rice,
                    3,
                    BaseRewardGold(CropSpecies.Rice, 3),
                    1,
                    Mathf.RoundToInt(GameTicks.FromSeconds(8f * 60f))); // ~8분
                _waiting.Add(tutorial);
            }

            FillWaitingSlots(inv);
        }

        public void Tick(float tickDelta, InventoryState inv)
        {
            TryUnlock(inv);
            if (!Unlocked) return;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                TradeOrder o = _active[i];
                if (!o.TimerStarted || o.Status != TradeOrderStatus.Active) continue;
                o.RemainingTicks -= Mathf.CeilToInt(tickDelta);
                if (o.RemainingTicks > 0) continue;

                o.RemainingTicks = 0;
                o.Status = TradeOrderStatus.Expired;
                // T05: 만료 시 평판 −1 (하한 0)
                Reputation = Mathf.Max(0, Reputation - 1);
                _active.RemoveAt(i);
                FillWaitingSlots(inv);
            }
        }

        public TradeOrder FindWaiting(int id)
        {
            for (int i = 0; i < _waiting.Count; i++)
                if (_waiting[i].Id == id) return _waiting[i];
            return null;
        }

        public TradeOrder FindActive(int id)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Id == id) return _active[i];
            return null;
        }

        public bool TryAccept(int orderId, out string message)
        {
            message = null;
            TradeOrder o = FindWaiting(orderId);
            if (o == null)
            {
                message = "주문 없음";
                return false;
            }

            if (_active.Count >= MaxActive)
            {
                message = $"진행 상한 {MaxActive}";
                return false;
            }

            _waiting.Remove(o);
            o.Status = TradeOrderStatus.Active;
            o.TimerStarted = true; // T13
            o.RemainingTicks = o.TimeLimitTicks;
            o.DeliveredCount = 0;
            _active.Add(o);
            message = $"수락: {o.Title}";
            return true;
        }

        public bool TryReject(int orderId, InventoryState inv, out string message)
        {
            message = null;
            TradeOrder o = FindWaiting(orderId);
            if (o == null)
            {
                message = "주문 없음";
                return false;
            }

            _waiting.Remove(o);
            FillWaitingSlots(inv);
            message = "거절 · 새 주문 갱신";
            return true;
        }

        /// <summary>1차 전량 납품만 (T04). 보유 ≥ 요구 잔량일 때만.</summary>
        public bool TryDeliverFull(int orderId, InventoryState inv, out string message)
        {
            message = null;
            TradeOrder o = FindActive(orderId);
            if (o == null || o.Status != TradeOrderStatus.Active)
            {
                message = "진행 중 주문 없음";
                return false;
            }

            int need = o.RequiredCount - o.DeliveredCount;
            if (need <= 0)
            {
                message = "이미 완료";
                return false;
            }

            int have = inv.GetCropCount(o.Species);
            if (have < need)
            {
                message = $"{DisplayName(o.Species)} 부족 ({have}/{need})";
                return false;
            }

            if (!inv.TryConsumeCrop(o.Species, need))
            {
                message = "차감 실패";
                return false;
            }

            o.DeliveredCount += need;

            int gold = Mathf.RoundToInt(o.RewardGold * RewardMultiplier);
            // T06: 콩 상품성 보너스는 프로토에 콩 없음 → 스킵
            // 향료초: 희귀도 배율은 전량 납품 시 보유 평균을 모름 → 스타터 납품 가정 시 배율 1.
            // 스탯 붙은 작물 스택이 없으므로 1차에서는 주문 표기 골드×티어만.

            inv.AddGold(gold);
            Reputation += o.ReputationReward;
            o.Status = TradeOrderStatus.Completed;
            _active.Remove(o);

            _lastRewardOrder = o;
            _lastRewardGold = gold;
            _lastRewardRep = o.ReputationReward;
            _lastRewardMessage = "주문 완료";

            FillWaitingSlots(inv);
            message = $"+{gold}G · 평판 +{o.ReputationReward}";
            return true;
        }

        public void FillWaitingSlots(InventoryState inv)
        {
            if (!Unlocked) return;
            while (_waiting.Count < WaitingSlots)
            {
                TradeOrder next = RollOrder(inv);
                if (next == null) break;
                _waiting.Add(next);
            }
        }

        TradeOrder RollOrder(InventoryState inv)
        {
            List<CropSpecies> pool = BuildSpeciesPool(inv);
            if (pool.Count == 0) return null;

            CropSpecies species = pool[UnityEngine.Random.Range(0, pool.Count)];
            GetBand(species, out int qtyMin, out int qtyMax, out int timeMinSec, out int timeMaxSec);
            int qty = UnityEngine.Random.Range(qtyMin, qtyMax + 1);
            int timeSec = UnityEngine.Random.Range(timeMinSec, timeMaxSec + 1);
            int baseGold = BaseRewardGold(species, qty);
            int rep = species == CropSpecies.Spiceleaf ? 3
                : species == CropSpecies.Cotton ? 2 : 1;

            return CreateOrder(
                PickTitle(species),
                species,
                qty,
                baseGold,
                rep,
                Mathf.RoundToInt(GameTicks.FromSeconds(timeSec)));
        }

        List<CropSpecies> BuildSpeciesPool(InventoryState inv)
        {
            var pool = new List<CropSpecies> { CropSpecies.Rice };
            // 면화·향료초: 상점 해금(평판) 후 주문 풀
            if (CottonShopUnlocked) pool.Add(CropSpecies.Cotton);
            if (SpiceleafShopUnlocked) pool.Add(CropSpecies.Spiceleaf);
            // 감자·당근·콩: 프로토 미구현 — 주문 풀 제외 (stub)
            return pool;
        }

        /// <summary>T07 밴드 + 면화/향료초 가정.</summary>
        static void GetBand(CropSpecies species, out int qtyMin, out int qtyMax, out int timeMinSec, out int timeMaxSec)
        {
            switch (species)
            {
                case CropSpecies.Cotton:
                    qtyMin = 6;
                    qtyMax = 16;
                    timeMinSec = 4 * 60;
                    timeMaxSec = 7 * 60;
                    break;
                case CropSpecies.Spiceleaf:
                    qtyMin = 1;
                    qtyMax = 3;
                    timeMinSec = 3 * 60;
                    timeMaxSec = 6 * 60;
                    break;
                default: // 벼 T07 3~8
                    qtyMin = 3;
                    qtyMax = 8;
                    timeMinSec = 3 * 60;
                    timeMaxSec = 5 * 60;
                    break;
            }
        }

        /// <summary>T09 ≈ 1.35× 상점 시세 중앙값.</summary>
        static int BaseRewardGold(CropSpecies species, int qty)
        {
            int shop = species switch
            {
                CropSpecies.Cotton => 3,
                CropSpecies.Spiceleaf => 8,
                _ => 10
            };
            return Mathf.Max(1, Mathf.RoundToInt(shop * qty * 1.35f));
        }

        static string PickTitle(CropSpecies species) => species switch
        {
            CropSpecies.Cotton => UnityEngine.Random.value < 0.5f ? "직물 길드" : "면화 납품",
            CropSpecies.Spiceleaf => UnityEngine.Random.value < 0.5f ? "특약 · 향신" : "희귀 향료",
            _ => UnityEngine.Random.value < 0.5f ? "마을 식량" : "저장고 보충"
        };

        TradeOrder CreateOrder(
            string title,
            CropSpecies species,
            int qty,
            int gold,
            int rep,
            int timeTicks)
        {
            return new TradeOrder
            {
                Id = _nextId++,
                Title = title,
                Species = species,
                RequiredCount = qty,
                DeliveredCount = 0,
                RewardGold = gold,
                ReputationReward = rep,
                TimeLimitTicks = timeTicks,
                RemainingTicks = timeTicks,
                Status = TradeOrderStatus.Waiting,
                TimerStarted = false
            };
        }

        public static string DisplayName(CropSpecies species) => species switch
        {
            CropSpecies.Cotton => "면화",
            CropSpecies.Spiceleaf => "향료초",
            _ => "벼"
        };

        public static string FormatRemaining(int ticks)
        {
            int sec = Mathf.Max(0, ticks) / GameTicks.PerSecond;
            int m = sec / 60;
            int s = sec % 60;
            return $"{m}:{s:00}";
        }
    }
}
