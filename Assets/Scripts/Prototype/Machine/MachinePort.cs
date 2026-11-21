using System.Collections.Generic;

namespace GuideToNonstopHarvest.Prototype.Machine
{
    /// <summary>
    /// 기계 입/출력 포트. DungeonFront ItemEntryList의 단순 버전.
    /// </summary>
    public sealed class MachinePort
    {
        readonly Dictionary<MachineItemId, int> _counts = new Dictionary<MachineItemId, int>();

        public int GetCount(MachineItemId id) =>
            _counts.TryGetValue(id, out int c) ? c : 0;

        public bool MatchesRecipe(MachineItemStack[] required)
        {
            if (required == null) return true;
            for (int i = 0; i < required.Length; i++)
            {
                if (GetCount(required[i].Id) < required[i].Count)
                    return false;
            }

            return true;
        }

        public bool CanFit(MachineItemStack[] outputs)
        {
            // 프로토: 슬롯 용량 제한 없음
            return outputs != null;
        }

        public bool TryConsume(MachineItemStack[] required)
        {
            if (!MatchesRecipe(required)) return false;
            for (int i = 0; i < required.Length; i++)
            {
                MachineItemId id = required[i].Id;
                _counts[id] = GetCount(id) - required[i].Count;
                if (_counts[id] <= 0) _counts.Remove(id);
            }

            return true;
        }

        public bool TryAdd(MachineItemId id, int count)
        {
            if (count <= 0 || id == MachineItemId.None) return false;
            _counts[id] = GetCount(id) + count;
            return true;
        }

        public bool TryAddOutputs(MachineItemStack[] outputs)
        {
            if (outputs == null || !CanFit(outputs)) return false;
            for (int i = 0; i < outputs.Length; i++)
                TryAdd(outputs[i].Id, outputs[i].Count);
            return true;
        }

        public bool TryTake(MachineItemId id, int count, out int taken)
        {
            taken = 0;
            int have = GetCount(id);
            if (have <= 0 || count <= 0) return false;
            taken = count < have ? count : have;
            int left = have - taken;
            if (left <= 0) _counts.Remove(id);
            else _counts[id] = left;
            return true;
        }

        public bool TryTakeFirst(out MachineItemId id, out int count)
        {
            id = MachineItemId.None;
            count = 0;
            MachineItemId found = MachineItemId.None;
            int foundCount = 0;
            foreach (var kv in _counts)
            {
                if (kv.Value <= 0) continue;
                found = kv.Key;
                foundCount = kv.Value;
                break;
            }

            if (found == MachineItemId.None) return false;
            _counts.Remove(found);
            id = found;
            count = foundCount;
            return true;
        }

        public void Clear() => _counts.Clear();
    }
}
