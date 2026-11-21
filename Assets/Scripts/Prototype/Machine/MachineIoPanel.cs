using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuideToNonstopHarvest.Prototype.Machine
{
    /// <summary>
    /// 기계 클릭 시 인풋/아웃풋·인벤 이송 UI.
    /// DungeonFront MachineRecipeUI 포트 모델 참고.
    /// 좌클릭 드래그로 이동, Shift+우클릭으로 인벤→인풋 자동 배치.
    /// 아웃풋에는 투입 불가.
    /// </summary>
    public sealed class MachineIoPanel
    {
        public enum SlotKind
        {
            Inventory,
            Input,
            Output
        }

        const float UiScale = 0.7f;

        readonly Transform _parentCanvas;
        readonly Func<InventoryState> _getInv;
        readonly Action<string> _setStatus;
        readonly Action _onChanged;
        readonly Font _font;
        readonly Sprite _pixel;

        GameObject _root;
        Transform _content;
        Text _title;
        Text _progress;
        RectTransform _ghostRt;
        Text _ghostLabel;

        MachineBase _machine;
        Vector2Int _cell;
        bool _open;

        // 드래그 페이로드
        bool _dragging;
        SlotKind _dragFrom;
        MachineItemId _dragId;
        int _dragCount;

        readonly List<MachineItemStack> _portScratch = new List<MachineItemStack>(4);

        public bool IsOpen => _open;
        public Vector2Int Cell => _cell;
        public MachineBase Machine => _machine;

        public MachineIoPanel(
            Transform parentCanvas,
            Sprite pixelSprite,
            Font font,
            Func<InventoryState> getInv,
            Action<string> setStatus,
            Action onChanged)
        {
            _parentCanvas = parentCanvas;
            _pixel = pixelSprite;
            _font = font;
            _getInv = getInv;
            _setStatus = setStatus;
            _onChanged = onChanged;
            BuildShell();
            Close();
        }

        void BuildShell()
        {
            _root = new GameObject("MachineIoPanel");
            _root.transform.SetParent(_parentCanvas, false);
            var rt = _root.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 36f);
            rt.sizeDelta = new Vector2(420f * UiScale, 460f * UiScale);
            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.1f, 0.14f, 0.96f);

            _content = _root.transform;
            _title = MakeText(_content, "Title", Ui(12, -10), 16);
            _progress = MakeText(_content, "Progress", Ui(12, -36), 13);

            var ghostGo = new GameObject("DragGhost");
            ghostGo.transform.SetParent(_parentCanvas, false);
            _ghostRt = ghostGo.AddComponent<RectTransform>();
            _ghostRt.anchorMin = _ghostRt.anchorMax = new Vector2(0, 0);
            _ghostRt.pivot = new Vector2(0.5f, 0.5f);
            _ghostRt.sizeDelta = new Vector2(110f * UiScale, 36f * UiScale);
            var gImg = ghostGo.AddComponent<Image>();
            gImg.color = new Color(0.15f, 0.2f, 0.28f, 0.9f);
            gImg.raycastTarget = false;
            _ghostLabel = MakeText(ghostGo.transform, "G", Vector2.zero, 12);
            var glRt = _ghostLabel.GetComponent<RectTransform>();
            glRt.anchorMin = Vector2.zero;
            glRt.anchorMax = Vector2.one;
            glRt.offsetMin = glRt.offsetMax = Vector2.zero;
            _ghostLabel.alignment = TextAnchor.MiddleCenter;
            ghostGo.SetActive(false);
        }

        public void Open(MachineBase machine, Vector2Int cell)
        {
            if (machine == null) return;
            _machine = machine;
            _cell = cell;
            _open = true;
            _root.SetActive(true);
            CancelDrag();
            Rebuild();
            _setStatus?.Invoke($"Mill ({cell.x},{cell.y}) UI");
        }

        public void Close()
        {
            _open = false;
            _machine = null;
            CancelDrag();
            if (_root != null) _root.SetActive(false);
        }

        public void Tick()
        {
            if (!_open || _machine == null) return;
            UpdateProgressLabel();
            UpdateGhostFollow();
            // Shift+우클릭은 슬롯 핸들러에서 처리. 패널 열린 채 타이머만 갱신.
        }

        public void Rebuild()
        {
            if (!_open || _machine == null) return;

            // 셸(Title/Progress) 제외 동적 자식 제거
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                Transform c = _content.GetChild(i);
                if (c.name == "Title" || c.name == "Progress") continue;
                UnityEngine.Object.Destroy(c.gameObject);
            }

            _title.text = $"Mill ({_cell.x},{_cell.y})";
            UpdateProgressLabel();

            float y = -64f;
            MakeText(_content, "InH", Ui(12, y), 14).text = "—— 인풋 ——";
            y -= 28f;
            BuildPortRow(SlotKind.Input, _machine.InputPort, y);
            y -= 48f;

            MakeText(_content, "OutH", Ui(12, y), 14).text = "—— 아웃풋 (투입 불가) ——";
            y -= 28f;
            BuildPortRow(SlotKind.Output, _machine.OutputPort, y);
            y -= 48f;

            MakeText(_content, "InvH", Ui(12, y), 14).text =
                "—— 인벤 (드래그→인풋 / Shift+우클릭 자동) ——";
            y -= 28f;

            InventoryState inv = _getInv();
            float x = 12f;
            // 포트→인벤 드롭 존 (항상)
            MakeItemSlot(_content, SlotKind.Inventory, MachineItemId.None, 0, Ui(x, y));
            x += 120f;

            // 벼(작물) → CropProduce
            int rice = inv != null ? inv.GetCropCount(CropSpecies.Rice) : 0;
            if (rice > 0)
            {
                MakeItemSlot(_content, SlotKind.Inventory, MachineItemId.CropProduce, rice, Ui(x, y));
                x += 120f;
            }

            int fert = inv != null ? inv.Fertilizer : 0;
            if (fert > 0)
            {
                // 비료는 Mill 인풋 레시피가 아님 — 인벤 표시·아웃풋 회수 대상
                MakeItemSlot(_content, SlotKind.Inventory, MachineItemId.Fertilizer, fert, Ui(x, y));
            }

            y -= 56f;
            MakeButton(_content, "닫기", Ui(140, y), Close, 120f * UiScale);
            MakeText(_content, "Hint", Ui(12, y - 36f), 11).text =
                "LMB 드래그 이동 · Shift+RMB 인벤→인풋 · 아웃풋엔 드롭 불가";
        }

        void BuildPortRow(SlotKind kind, MachinePort port, float y)
        {
            _portScratch.Clear();
            port.CopyEntries(_portScratch);
            if (_portScratch.Count == 0)
            {
                // 빈 슬롯 하나 (드롭 타겟)
                MakeItemSlot(_content, kind, MachineItemId.None, 0, Ui(12, y));
                return;
            }

            float x = 12f;
            for (int i = 0; i < _portScratch.Count; i++)
            {
                MachineItemStack e = _portScratch[i];
                MakeItemSlot(_content, kind, e.Id, e.Count, Ui(x, y));
                x += 120f;
            }

            // 인풋 빈 드롭 존 추가 (이미 아이템이 있어도 추가 투입용)
            if (kind == SlotKind.Input)
                MakeItemSlot(_content, kind, MachineItemId.None, 0, Ui(x, y));
        }

        void MakeItemSlot(Transform parent, SlotKind kind, MachineItemId id, int count, Vector2 pos)
        {
            var go = new GameObject($"Slot_{kind}_{id}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(110f * UiScale, 40f * UiScale);

            var img = go.AddComponent<Image>();
            img.color = kind switch
            {
                SlotKind.Input => new Color(0.18f, 0.28f, 0.22f, 0.95f),
                SlotKind.Output => new Color(0.28f, 0.22f, 0.18f, 0.95f),
                _ => new Color(0.2f, 0.25f, 0.32f, 0.95f)
            };

            string label = id == MachineItemId.None
                ? (kind == SlotKind.Input ? "[빈 인풋]"
                    : kind == SlotKind.Output ? "[빈 아웃]"
                    : "[인벤으로]")
                : $"{MachineItemNames.Display(id)} ×{count}";
            var text = MakeText(go.transform, "L", Vector2.zero, 11);
            var trt = text.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(4f, 2f);
            trt.offsetMax = new Vector2(-4f, -2f);
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            text.raycastTarget = false;

            var handler = go.AddComponent<MachineSlotHandler>();
            handler.Bind(this, kind, id, count);
        }

        void UpdateProgressLabel()
        {
            if (_progress == null || _machine == null) return;
            if (_machine.HasActiveWip)
                _progress.text = $"가공 중 {Mathf.FloorToInt(_machine.Progress01 * 100f)}%";
            else if (_machine.OutputReady)
                _progress.text = "아웃풋 대기";
            else
                _progress.text = "대기 (인풋 투입 시 자동 가공)";
        }

        void UpdateGhostFollow()
        {
            if (!_dragging || _ghostRt == null) return;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();
            _ghostRt.position = screen;
        }

        // —— 슬롯 콜백 ——

        public void OnSlotBeginDrag(SlotKind kind, MachineItemId id, int count)
        {
            if (id == MachineItemId.None || count <= 0) return;
            // 인벤의 비료는 인풋으로 못 넣지만, 드래그 시작은 허용(드롭 시 거절)
            _dragging = true;
            _dragFrom = kind;
            _dragId = id;
            _dragCount = 1; // 1개 단위 이동 (DungeonFront TryDepositOne과 유사)
            if (_ghostRt != null)
            {
                _ghostRt.gameObject.SetActive(true);
                _ghostLabel.text = $"{MachineItemNames.Display(id)} ×1";
            }
        }

        public void OnSlotEndDrag()
        {
            // Drop이 먼저 올 수 있음. 고스트만 정리.
            if (_ghostRt != null) _ghostRt.gameObject.SetActive(false);
            _dragging = false;
        }

        public void OnSlotDrop(SlotKind toKind)
        {
            if (!_dragging) return;
            bool ok = TryTransfer(_dragFrom, toKind, _dragId, _dragCount, out string msg);
            _setStatus?.Invoke(msg);
            CancelDrag();
            if (ok)
            {
                Rebuild();
                _onChanged?.Invoke();
            }
        }

        public void OnSlotShiftRightClick(SlotKind kind, MachineItemId id, int count)
        {
            // 자동 배치: 인벤 → 인풋 (해당 아이템 전부)
            if (kind != SlotKind.Inventory || id == MachineItemId.None || count <= 0) return;
            if (id == MachineItemId.Fertilizer)
            {
                _setStatus?.Invoke("비료는 Mill 인풋 불가");
                return;
            }

            int moved = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TryTransfer(SlotKind.Inventory, SlotKind.Input, id, 1, out _))
                    break;
                moved++;
            }

            _setStatus?.Invoke(moved > 0
                ? $"자동 배치 {MachineItemNames.Display(id)} ×{moved}"
                : "자동 배치 실패");
            if (moved > 0)
            {
                Rebuild();
                _onChanged?.Invoke();
            }
        }

        public void OnSlotClick(SlotKind kind, MachineItemId id, int count)
        {
            // 아웃풋 LMB: 전부 인벤으로 (편의)
            if (kind == SlotKind.Output && id != MachineItemId.None && count > 0)
            {
                if (TryTransfer(SlotKind.Output, SlotKind.Inventory, id, count, out string msg))
                {
                    _setStatus?.Invoke(msg);
                    Rebuild();
                    _onChanged?.Invoke();
                }
                else _setStatus?.Invoke(msg);
            }
        }

        bool TryTransfer(SlotKind from, SlotKind to, MachineItemId id, int count, out string message)
        {
            message = "실패";
            if (_machine == null || id == MachineItemId.None || count <= 0)
                return false;

            // 아웃풋에는 투입 불가
            if (to == SlotKind.Output)
            {
                message = "아웃풋에는 넣을 수 없음";
                return false;
            }

            if (from == to)
            {
                message = "같은 칸";
                return false;
            }

            InventoryState inv = _getInv();
            if (inv == null) return false;

            // Inventory → Input
            if (from == SlotKind.Inventory && to == SlotKind.Input)
            {
                if (id == MachineItemId.Fertilizer)
                {
                    message = "비료는 인풋 불가";
                    return false;
                }

                if (id != MachineItemId.CropProduce)
                {
                    message = "이 아이템은 Mill 인풋 아님";
                    return false;
                }

                if (!inv.TryConsumeCrop(CropSpecies.Rice, count))
                {
                    message = "벼(작물) 부족";
                    return false;
                }

                if (!_machine.TryInsert(id, count))
                {
                    inv.AddCrop(CropSpecies.Rice, count);
                    message = "인풋 투입 실패";
                    return false;
                }

                message = $"인풋 ← 벼 ×{count}";
                return true;
            }

            // Input → Inventory
            if (from == SlotKind.Input && to == SlotKind.Inventory)
            {
                if (!_machine.TryTakeInput(id, count, out int taken) || taken <= 0)
                {
                    message = "인풋 회수 실패";
                    return false;
                }

                if (id == MachineItemId.CropProduce)
                    inv.AddCrop(CropSpecies.Rice, taken);
                else if (id == MachineItemId.Fertilizer)
                    inv.AddFertilizer(taken);
                else
                {
                    // 알 수 없으면 되돌림
                    _machine.TryInsert(id, taken);
                    message = "알 수 없는 아이템";
                    return false;
                }

                message = $"인벤 ← {MachineItemNames.Display(id)} ×{taken}";
                return true;
            }

            // Output → Inventory
            if (from == SlotKind.Output && to == SlotKind.Inventory)
            {
                if (!_machine.TryTakeOutput(id, count, out int taken) || taken <= 0)
                {
                    message = "아웃풋 회수 실패";
                    return false;
                }

                if (id == MachineItemId.Fertilizer)
                    inv.AddFertilizer(taken);
                else if (id == MachineItemId.CropProduce)
                    inv.AddCrop(CropSpecies.Rice, taken);
                else
                {
                    _machine.OutputPort.TryAdd(id, taken);
                    message = "알 수 없는 아이템";
                    return false;
                }

                message = $"인벤 ← {MachineItemNames.Display(id)} ×{taken}";
                return true;
            }

            // Input → Output / Inventory → Output already blocked
            // Output → Input: 허용? 보통 불가. 차단.
            if (from == SlotKind.Output && to == SlotKind.Input)
            {
                message = "아웃풋→인풋 직접 이동 불가";
                return false;
            }

            message = "지원하지 않는 이동";
            return false;
        }

        void CancelDrag()
        {
            _dragging = false;
            _dragId = MachineItemId.None;
            _dragCount = 0;
            if (_ghostRt != null) _ghostRt.gameObject.SetActive(false);
        }

        static Vector2 Ui(float x, float y) => new Vector2(x, y) * UiScale;

        Text MakeText(Transform parent, string name, Vector2 anchoredPos, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(400f * UiScale, 40f * UiScale);
            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = Mathf.Max(1, Mathf.RoundToInt(fontSize * UiScale));
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        Button MakeButton(Transform parent, string label, Vector2 anchoredPos,
            Action onClick, float width)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(width, 34f * UiScale);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.25f, 0.32f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.font = _font;
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = Mathf.Max(1, Mathf.RoundToInt(13 * UiScale));
            text.raycastTarget = false;
            return btn;
        }
    }

    /// <summary>슬롯 드래그·클릭·Shift+우클릭 핸들러.</summary>
    public sealed class MachineSlotHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
    {
        MachineIoPanel _panel;
        MachineIoPanel.SlotKind _kind;
        MachineItemId _id;
        int _count;

        public void Bind(MachineIoPanel panel, MachineIoPanel.SlotKind kind, MachineItemId id, int count)
        {
            _panel = panel;
            _kind = kind;
            _id = id;
            _count = count;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _panel?.OnSlotBeginDrag(_kind, _id, _count);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // 고스트는 Panel.Tick에서 마우스 추적
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _panel?.OnSlotEndDrag();
        }

        public void OnDrop(PointerEventData eventData)
        {
            _panel?.OnSlotDrop(_kind);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_panel == null) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                Keyboard kb = Keyboard.current;
                bool shift = kb != null
                    && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                if (shift)
                {
                    _panel.OnSlotShiftRightClick(_kind, _id, _count);
                    return;
                }
            }

            if (eventData.button == PointerEventData.InputButton.Left)
                _panel.OnSlotClick(_kind, _id, _count);
        }
    }
}
