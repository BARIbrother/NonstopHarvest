using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GuideToNonstopHarvest.Prototype
{
    /// <summary>
    /// 퀵슬롯: 맨손/배치/획득도구.
    /// 맨손 우클릭(짧게)=땅 메뉴(심기·교접·파내기), 길게=스탯 개봉.
    /// </summary>
    public sealed class FarmGame : MonoBehaviour
    {
        const float CellWorldSize = 1f;
        const float Gap = 0.05f;
        const float UiScale = 0.7f;
        const float InspectHoldTicks = 20f;
        const float GroundMenuClickMaxTicks = 5f;
        const int QuickSlotCount = 3;

        enum ShopTab { Buy, Sell }
        enum PlaceBuilding { None, Mill }
        enum GroundMenuMode { Closed, Actions, PickSeed }

        /// <summary>0맨손 1배치 2획득(뿌리개)</summary>
        enum SlotId
        {
            Hand = 0,
            Place = 1,
            Obtained0 = 2
        }

        FarmGrid _grid;
        InventoryState _inv;

        SlotId _activeSlot = SlotId.Hand;
        PlaceBuilding _placeBuilding = PlaceBuilding.None;
        ShopTab _shopTab = ShopTab.Buy;
        bool _inventoryOpen;
        string _status = "맨손:수확·기계 | 우클릭:땅메뉴/스탯 | 인벤:중앙하단";

        GroundMenuMode _groundMenuMode = GroundMenuMode.Closed;
        Vector2Int _groundMenuCell;
        GameObject _groundMenuRoot;
        Transform _groundMenuContent;

        Camera _cam;
        readonly Dictionary<Vector2Int, SpriteRenderer> _cellViews = new Dictionary<Vector2Int, SpriteRenderer>();
        readonly Dictionary<Vector2Int, TextMesh> _cellLabels = new Dictionary<Vector2Int, TextMesh>();
        readonly Dictionary<Vector2Int, float> _cellBaseScale = new Dictionary<Vector2Int, float>();

        bool _inspectHolding;
        Vector2Int _inspectCell;
        float _inspectHoldTicks;
        float _revealPunchTicks;
        Vector2Int _revealPunchCell;
        bool _sprayHolding;
        Vector2Int _sprayCell;
        float _sprayHoldTicks;
        bool _handRightPending;
        Vector2Int _handRightCell;
        float _handRightHoldTicks;

        Text _hudGold;
        Text _hudStatus;
        Text _hudTool;
        Transform _shopPanel;
        Transform _contextPanel;
        GameObject _inventoryRoot;
        Transform _inventoryContent;
        readonly Button[] _quickButtons = new Button[QuickSlotCount];
        readonly Text[] _quickLabels = new Text[QuickSlotCount];
        Text _invButtonLabel;

        Sprite _pixelSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindAnyObjectByType<FarmGame>() != null) return;
            new GameObject("FarmGame").AddComponent<FarmGame>();
        }

        void Awake()
        {
            _grid = new FarmGrid();
            _inv = new InventoryState();
            _pixelSprite = CreatePixelSprite();
            EnsureCamera();
            EnsureEventSystem();
            BuildBoard();
            BuildUi();
            RefreshAllPanels();
            RefreshBoardVisuals();
        }

        void Update()
        {
            float tickDelta = GameTicks.Delta(Time.deltaTime);
            _grid.Tick(Time.deltaTime);
            HandleLeftInput(tickDelta);
            HandleRightInput(tickDelta);
            if (_revealPunchTicks > 0f)
                _revealPunchTicks = Mathf.Max(0f, _revealPunchTicks - tickDelta);
            RefreshBoardVisuals();
            RefreshHud();
            RefreshQuickSlots();
        }

        void EnsureCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                _cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            _cam.orthographic = true;
            _cam.orthographicSize = 4.2f;
            _cam.transform.position = new Vector3(2f, 2f, -10f);
            _cam.backgroundColor = new Color(0.12f, 0.14f, 0.16f);
            _cam.clearFlags = CameraClearFlags.SolidColor;
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        void BuildBoard()
        {
            var boardRoot = new GameObject("Board").transform;
            boardRoot.SetParent(transform, false);
            for (int y = 0; y < FarmGrid.Size; y++)
            for (int x = 0; x < FarmGrid.Size; x++)
            {
                var cellGo = new GameObject($"Cell_{x}_{y}");
                cellGo.transform.SetParent(boardRoot, false);
                cellGo.transform.position = new Vector3(x, y, 0f);
                var sr = cellGo.AddComponent<SpriteRenderer>();
                sr.sprite = _pixelSprite;
                float scale = CellWorldSize - Gap;
                cellGo.transform.localScale = new Vector3(scale, scale, 1f);
                cellGo.AddComponent<BoxCollider2D>().size = Vector2.one;

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(cellGo.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                labelGo.transform.localScale = new Vector3(0.12f / scale, 0.12f / scale, 1f);
                var tm = labelGo.AddComponent<TextMesh>();
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.characterSize = 0.35f;
                tm.fontSize = 32;
                tm.color = Color.white;

                var key = new Vector2Int(x, y);
                _cellViews[key] = sr;
                _cellLabels[key] = tm;
                _cellBaseScale[key] = scale;
            }
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("UI");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            _hudGold = MakeText(canvasGo.transform, "Gold", Ui(10, -10), 22);
            _hudTool = MakeText(canvasGo.transform, "Tool", Ui(10, -40), 15);
            _hudStatus = MakeText(canvasGo.transform, "Status", Ui(10, -70), 14);
            _hudStatus.color = new Color(0.85f, 0.9f, 0.7f);

            var shopGo = new GameObject("Shop");
            shopGo.transform.SetParent(canvasGo.transform, false);
            var shopRt = shopGo.AddComponent<RectTransform>();
            shopRt.anchorMin = shopRt.anchorMax = new Vector2(0, 1);
            shopRt.pivot = new Vector2(0, 1);
            shopRt.anchoredPosition = Ui(10, -120);
            shopRt.sizeDelta = Ui(340, 260);
            _shopPanel = shopGo.transform;

            var ctxGo = new GameObject("Context");
            ctxGo.transform.SetParent(canvasGo.transform, false);
            var ctxRt = ctxGo.AddComponent<RectTransform>();
            ctxRt.anchorMin = ctxRt.anchorMax = ctxRt.pivot = new Vector2(1, 1);
            ctxRt.anchoredPosition = Ui(-10, -10);
            ctxRt.sizeDelta = Ui(300, 400);
            _contextPanel = ctxGo.transform;

            // 중앙 하단: 인벤 트리거 + 퀵슬롯
            var bottom = new GameObject("BottomBar");
            bottom.transform.SetParent(canvasGo.transform, false);
            var brt = bottom.AddComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 8f);
            brt.sizeDelta = new Vector2(560f * UiScale, 100f * UiScale);

            var invBtn = MakeButton(bottom.transform, "인벤토리", new Vector2(0, 0), ToggleInventory, 120f * UiScale);
            var invRt = invBtn.GetComponent<RectTransform>();
            invRt.anchorMin = invRt.anchorMax = new Vector2(0.5f, 0f);
            invRt.pivot = new Vector2(0.5f, 0f);
            invRt.anchoredPosition = new Vector2(0f, 52f * UiScale);
            _invButtonLabel = invBtn.GetComponentInChildren<Text>();

            float slotW = 88f * UiScale;
            float startX = -1.5f * slotW;
            for (int i = 0; i < QuickSlotCount; i++)
            {
                int idx = i;
                var btn = MakeButton(bottom.transform, $"S{i}", Vector2.zero, () => SelectSlot((SlotId)idx), slotW);
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(startX + i * (slotW + 6f) + slotW * 0.5f, 4f);
                _quickButtons[i] = btn;
                _quickLabels[i] = btn.GetComponentInChildren<Text>();
            }

            // 인벤토리 오버레이 (중앙)
            _inventoryRoot = new GameObject("InventoryPanel");
            _inventoryRoot.transform.SetParent(canvasGo.transform, false);
            var irt = _inventoryRoot.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.5f, 0.5f);
            irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = new Vector2(0f, 40f);
            irt.sizeDelta = new Vector2(420f * UiScale, 380f * UiScale);
            var bg = _inventoryRoot.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.1f, 0.14f, 0.94f);
            _inventoryContent = _inventoryRoot.transform;
            _inventoryRoot.SetActive(false);

            // 땅 액션 메뉴 (화면 중앙)
            _groundMenuRoot = new GameObject("GroundMenu");
            _groundMenuRoot.transform.SetParent(canvasGo.transform, false);
            var grt = _groundMenuRoot.AddComponent<RectTransform>();
            grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f);
            grt.anchoredPosition = new Vector2(0f, 20f);
            grt.sizeDelta = new Vector2(300f * UiScale, 260f * UiScale);
            var gbg = _groundMenuRoot.AddComponent<Image>();
            gbg.color = new Color(0.1f, 0.12f, 0.16f, 0.96f);
            _groundMenuContent = _groundMenuRoot.transform;
            _groundMenuRoot.SetActive(false);
        }

        void ToggleInventory()
        {
            _inventoryOpen = !_inventoryOpen;
            _inventoryRoot.SetActive(_inventoryOpen);
            if (_inventoryOpen) RefreshInventoryPanel();
            if (_invButtonLabel != null)
                _invButtonLabel.text = _inventoryOpen ? "인벤 닫기" : "인벤토리";
        }

        void SelectSlot(SlotId slot)
        {
            if (slot == SlotId.Obtained0 && !_inv.OwnsSprayer)
            {
                _status = _inv.SprayerUnlocked ? "상점에서 뿌리개 구매" : "첫 비료 후 뿌리개 해금";
                return;
            }

            _activeSlot = slot;
            CancelSpray();
            CancelHandRight();
            CloseGroundMenu();
            if (slot != SlotId.Place) _placeBuilding = PlaceBuilding.None;
            RefreshAllPanels();
            _status = SlotStatus();
        }

        string SlotStatus() => _activeSlot switch
        {
            SlotId.Hand => "맨손: 수확·기계 | 우클릭 짧게=땅메뉴 / 길게=스탯",
            SlotId.Place => _placeBuilding == PlaceBuilding.Mill
                ? "배치: Mill (그리드 1×1)"
                : "배치: 건물 선택",
            SlotId.Obtained0 => "뿌리개: 작물 위 좌클릭 0.5초",
            _ => ""
        };

        void RefreshQuickSlots()
        {
            if (_quickLabels[0] == null) return;
            _quickLabels[0].text = Mark(SlotId.Hand, "맨손");
            _quickLabels[1].text = Mark(SlotId.Place, "배치");
            _quickLabels[2].text = _inv.OwnsSprayer
                ? Mark(SlotId.Obtained0, "뿌리개")
                : "잠김";
        }

        string Mark(SlotId slot, string label) =>
            _activeSlot == slot ? $"> {label}" : label;

        void RefreshAllPanels()
        {
            RefreshShopPanel();
            RefreshContextPanel();
            RefreshQuickSlots();
            if (_inventoryOpen) RefreshInventoryPanel();
            if (_groundMenuMode != GroundMenuMode.Closed) RefreshGroundMenu();
            RefreshHud();
        }

        void CloseGroundMenu()
        {
            _groundMenuMode = GroundMenuMode.Closed;
            if (_groundMenuRoot != null) _groundMenuRoot.SetActive(false);
        }

        void OpenGroundMenu(int x, int y)
        {
            _groundMenuCell = new Vector2Int(x, y);
            _groundMenuMode = GroundMenuMode.Actions;
            _groundMenuRoot.SetActive(true);
            RefreshGroundMenu();
            _status = $"땅 메뉴 ({x},{y})";
        }

        void RefreshGroundMenu()
        {
            if (_groundMenuRoot == null || _groundMenuMode == GroundMenuMode.Closed) return;
            ClearChildren(_groundMenuContent);
            var bg = _groundMenuRoot.GetComponent<Image>();
            if (bg == null)
            {
                bg = _groundMenuRoot.AddComponent<Image>();
                bg.color = new Color(0.1f, 0.12f, 0.16f, 0.96f);
            }

            int x = _groundMenuCell.x;
            int y = _groundMenuCell.y;
            FarmCell cell = _grid.Get(x, y);

            if (_groundMenuMode == GroundMenuMode.PickSeed)
            {
                MakeText(_groundMenuContent, "T", Ui(12, -10), 16).text = $"씨앗 선택 ({x},{y})";
                var seeds = _inv.Seeds;
                if (seeds.Count == 0)
                {
                    MakeText(_groundMenuContent, "E", Ui(12, -44), 13).text = "씨앗 없음";
                    MakeButton(_groundMenuContent, "뒤로", Ui(12, -90), () =>
                    {
                        _groundMenuMode = GroundMenuMode.Actions;
                        RefreshGroundMenu();
                    }, 260f * UiScale);
                    return;
                }

                for (int i = 0; i < seeds.Count; i++)
                {
                    int index = i;
                    SeedStack s = seeds[i];
                    string label = $"성장{s.Stats.GrowthSpeed} 드랍{s.Stats.DropRate}  ×{s.Count}";
                    MakeButton(_groundMenuContent, label, Ui(12, -44 - 38 * i), () =>
                    {
                        if (_grid.TryPlant(x, y, s.Stats, _inv))
                        {
                            _status = $"심기 성장{s.Stats.GrowthSpeed}/드랍{s.Stats.DropRate}";
                            CloseGroundMenu();
                            RefreshAllPanels();
                        }
                        else
                        {
                            _status = cell.IsEmpty ? "심기 실패" : "빈 칸에만 심기";
                            RefreshGroundMenu();
                        }
                    }, 260f * UiScale);
                }

                MakeButton(_groundMenuContent, "뒤로", Ui(12, -44 - 38 * seeds.Count), () =>
                {
                    _groundMenuMode = GroundMenuMode.Actions;
                    RefreshGroundMenu();
                }, 260f * UiScale);
                return;
            }

            MakeText(_groundMenuContent, "T", Ui(12, -10), 16).text = $"땅 ({x},{y})";
            MakeButton(_groundMenuContent, "씨앗 심기", Ui(12, -48), () =>
            {
                _groundMenuMode = GroundMenuMode.PickSeed;
                RefreshGroundMenu();
            }, 260f * UiScale);
            MakeButton(_groundMenuContent, "교접 지지대", Ui(12, -90), () =>
            {
                if (_grid.TryPlaceBreedingSupport(x, y, _inv))
                {
                    _status = "교접 성공";
                    CloseGroundMenu();
                    RefreshAllPanels();
                }
                else
                {
                    _status = _inv.Supports <= 0 ? "지지대 없음" : "빈칸+부모≥1 필요";
                    RefreshGroundMenu();
                }
            }, 260f * UiScale);
            MakeButton(_groundMenuContent, "파내기", Ui(12, -132), () =>
            {
                if (_grid.TryDigOut(x, y))
                {
                    _status = $"파냄 ({x},{y})";
                    CloseGroundMenu();
                    RefreshAllPanels();
                }
                else
                {
                    _status = "작물 칸만 파내기";
                    RefreshGroundMenu();
                }
            }, 260f * UiScale);
            MakeButton(_groundMenuContent, "닫기", Ui(12, -180), CloseGroundMenu, 260f * UiScale);
        }

        void RefreshInventoryPanel()
        {
            ClearChildren(_inventoryContent);
            // 배경 Image는 ClearChildren이 지우므로 다시
            var bg = _inventoryRoot.GetComponent<Image>();
            if (bg == null)
            {
                bg = _inventoryRoot.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.1f, 0.14f, 0.94f);
            }

            float y = -12f;
            MakeText(_inventoryContent, "InvTitle", Ui(16, y), 18).text = "인벤토리";
            y -= 36f;

            MakeText(_inventoryContent, "SecSeed", Ui(16, y), 15).text = "—— 씨앗 ——";
            y -= 28f;

            var seeds = _inv.Seeds;
            if (seeds.Count == 0)
            {
                MakeText(_inventoryContent, "NoSeed", Ui(24, y), 14).text = "(없음)";
                y -= 28f;
            }
            else
            {
                for (int i = 0; i < seeds.Count; i++)
                {
                    SeedStack s = seeds[i];
                    // 직관적 한 줄: 아이콘 대용 박스 + 스탯 + 수량
                    string line =
                        $"벼씨앗  성장 {s.Stats.GrowthSpeed}  드랍 {s.Stats.DropRate}   × {s.Count}";
                    var row = MakeText(_inventoryContent, $"Seed{i}", Ui(20, y), 14);
                    row.text = line;
                    row.color = new Color(0.85f, 0.95f, 0.75f);
                    y -= 26f;
                }
            }

            y -= 8f;
            MakeText(_inventoryContent, "SecItem", Ui(16, y), 15).text = "—— 아이템 ——";
            y -= 28f;
            MakeText(_inventoryContent, "Rice", Ui(20, y), 14).text = $"벼(작물)     × {_inv.RiceCrops}";
            y -= 24f;
            MakeText(_inventoryContent, "Sup", Ui(20, y), 14).text = $"교접 지지대  × {_inv.Supports}";
            y -= 24f;
            MakeText(_inventoryContent, "Fert", Ui(20, y), 14).text = $"비료         × {_inv.Fertilizer}";
            y -= 32f;

            MakeText(_inventoryContent, "SecBld", Ui(16, y), 15).text = "—— 건물 / 도구 ——";
            y -= 28f;
            MakeText(_inventoryContent, "Mill", Ui(20, y), 14).text = $"Mill(미배치) × {_inv.MillsInInventory}";
            y -= 24f;
            MakeText(_inventoryContent, "Tool", Ui(20, y), 14).text =
                _inv.OwnsSprayer ? "비료 뿌리개  보유" :
                _inv.SprayerUnlocked ? "비료 뿌리개  해금됨(미구매)" : "비료 뿌리개  미해금";
            y -= 36f;

            MakeButton(_inventoryContent, "닫기", Ui(140, y), () =>
            {
                _inventoryOpen = false;
                _inventoryRoot.SetActive(false);
                if (_invButtonLabel != null) _invButtonLabel.text = "인벤토리";
            }, 100f * UiScale);
        }

        void RefreshShopPanel()
        {
            ClearChildren(_shopPanel);
            MakeText(_shopPanel, "ShopTitle", Vector2.zero, 16).text = "상점";
            MakeButton(_shopPanel, _shopTab == ShopTab.Buy ? ">구매" : "구매", Ui(0, -28), () =>
            {
                _shopTab = ShopTab.Buy;
                RefreshShopPanel();
            }, 80f * UiScale);
            MakeButton(_shopPanel, _shopTab == ShopTab.Sell ? ">판매" : "판매", Ui(90, -28), () =>
            {
                _shopTab = ShopTab.Sell;
                RefreshShopPanel();
            }, 80f * UiScale);

            if (_shopTab == ShopTab.Buy)
            {
                float y = -70f;
                MakeButton(_shopPanel, "벼씨앗 0/0 · 20G", Ui(0, y), () =>
                {
                    _status = _inv.TryBuyStarterRiceSeed(1) ? "씨앗 0/0 +1" : "골드 부족";
                    RefreshAllPanels();
                }, 280f * UiScale);
                y -= 38f;
                MakeButton(_shopPanel, $"지지대 5G", Ui(0, y), () =>
                {
                    _status = _inv.TryBuySupport() ? "지지대 +1" : "골드 부족";
                    RefreshAllPanels();
                }, 280f * UiScale);
                y -= 38f;
                MakeButton(_shopPanel, "Mill 100G", Ui(0, y), () =>
                {
                    _status = _inv.TryBuyMill() ? "Mill +1" : "골드 부족";
                    RefreshAllPanels();
                }, 280f * UiScale);
                y -= 38f;
                if (_inv.SprayerUnlocked && !_inv.OwnsSprayer)
                {
                    MakeButton(_shopPanel, "뿌리개 0G", Ui(0, y), () =>
                    {
                        _status = _inv.TryBuySprayer() ? "뿌리개 획득" : "실패";
                        RefreshAllPanels();
                    }, 280f * UiScale);
                }
                else if (!_inv.SprayerUnlocked)
                    MakeText(_shopPanel, "Hint", Ui(0, y), 12).text = "뿌리개: 첫 비료 후 해금";
            }
            else
            {
                MakeButton(_shopPanel, $"벼 판매 ({_inv.RiceCrops}×10G)", Ui(0, -70), () =>
                {
                    int g = _inv.SellAllRice();
                    _status = g > 0 ? $"+{g}G" : "팔 벼 없음";
                    RefreshAllPanels();
                }, 280f * UiScale);
                MakeText(_shopPanel, "SellHint", Ui(0, -110), 12).text = "씨앗 판매 불가";
            }
        }

        void RefreshContextPanel()
        {
            ClearChildren(_contextPanel);
            switch (_activeSlot)
            {
                case SlotId.Hand:
                    MakeText(_contextPanel, "T", Vector2.zero, 16).text = "맨손";
                    MakeText(_contextPanel, "H", Ui(0, -28), 13).text =
                        "좌클릭: Mature 수확 · Mill\n우클릭 짧게: 심기/교접/파내기\n우클릭 길게: 스탯 개봉";
                    break;
                case SlotId.Place:
                    BuildPlaceContext();
                    break;
                case SlotId.Obtained0:
                    MakeText(_contextPanel, "T", Vector2.zero, 16).text = "비료 뿌리개";
                    MakeText(_contextPanel, "S", Ui(0, -28), 13).text =
                        $"비료 수량은 인벤 참고\n작물 위 좌클릭 0.5초";
                    break;
            }
        }

        void BuildPlaceContext()
        {
            MakeText(_contextPanel, "T", Vector2.zero, 16).text = "배치 (그리드)";
            string mill = _placeBuilding == PlaceBuilding.Mill
                ? $"> Mill ×{_inv.MillsInInventory}"
                : $"Mill ×{_inv.MillsInInventory}";
            MakeButton(_contextPanel, mill, Ui(0, -36), () =>
            {
                if (_inv.MillsInInventory <= 0)
                {
                    _status = "Mill 없음";
                    return;
                }

                _placeBuilding = PlaceBuilding.Mill;
                _status = "Mill 배치 모드";
                RefreshContextPanel();
            }, 280f * UiScale);
            if (_inv.MillsInInventory <= 0)
                MakeText(_contextPanel, "E", Ui(0, -80), 13).text = "보유 건물 없음";
        }

        void HandleLeftInput(float tickDelta)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                CancelSpray();
                return;
            }

            if (_activeSlot == SlotId.Obtained0 && _inv.OwnsSprayer)
            {
                HandleSprayHover(mouse, tickDelta);
                return;
            }

            CancelSpray();
            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (!TryGetCellUnderMouse(out int x, out int y)) return;
            HandleCellClick(x, y);
        }

        void HandleSprayHover(Mouse mouse, float tickDelta)
        {
            if (mouse.rightButton.isPressed)
            {
                if (_sprayHolding)
                {
                    CancelSpray();
                    _status = "살포 중단";
                }

                return;
            }

            if (!mouse.leftButton.isPressed)
            {
                CancelSpray();
                return;
            }

            if (!TryGetCellUnderMouse(out int x, out int y))
            {
                CancelSpray();
                return;
            }

            FarmCell cell = _grid.Get(x, y);
            if (cell.Kind != CellKind.Crop)
            {
                CancelSpray();
                _status = "작물 칸만";
                return;
            }

            var key = new Vector2Int(x, y);
            if (!_sprayHolding || _sprayCell != key)
            {
                _sprayHolding = true;
                _sprayCell = key;
                _sprayHoldTicks = 0f;
            }

            _sprayHoldTicks += tickDelta;
            float t = Mathf.Clamp01(_sprayHoldTicks / FertilizerRules.SprayHoverTicks);
            _status = $"살포… {Mathf.FloorToInt(t * 100f)}%";
            if (_sprayHoldTicks < FertilizerRules.SprayHoverTicks) return;

            _status = _grid.TrySprayFertilizer(x, y, _inv)
                ? $"살포 완료 ({x},{y})"
                : (_inv.Fertilizer <= 0 ? "비료 없음" : "실패");
            CancelSpray();
            RefreshAllPanels();
        }

        void HandleCellClick(int x, int y)
        {
            FarmCell cell = _grid.Get(x, y);

            if (_activeSlot == SlotId.Place)
            {
                if (_placeBuilding == PlaceBuilding.Mill)
                {
                    if (_grid.TryPlaceMill(x, y, _inv))
                    {
                        _status = $"Mill ({x},{y})";
                        if (_inv.MillsInInventory <= 0) _placeBuilding = PlaceBuilding.None;
                        RefreshAllPanels();
                    }
                    else _status = "빈 칸만 배치";
                }
                else _status = "건물 선택";
                return;
            }

            // 맨손
            if (_activeSlot == SlotId.Hand)
            {
                if (cell.Kind == CellKind.Machine)
                {
                    if (_grid.TryInteractMachine(x, y, _inv, out string msg))
                    {
                        _status = msg;
                        RefreshAllPanels();
                    }
                    else _status = msg ?? "실패";
                    return;
                }

                if (cell.IsMature)
                {
                    if (_grid.TryHarvest(x, y, _inv))
                    {
                        _status = $"수확 ({x},{y})";
                        RefreshAllPanels();
                    }

                    return;
                }

                if (!cell.IsEmpty)
                {
                    string fert = cell.Fertilizer.AnyActive ? " [비료]" : "";
                    string stats = cell.StatsRevealed ? cell.Stats.ToString() : "???";
                    _status = $"단계{cell.Stage} {stats}{fert}";
                }
            }
        }

        void HandleRightInput(float tickDelta)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                CancelInspect();
                CancelHandRight();
                return;
            }

            bool spraying = _activeSlot == SlotId.Obtained0 && _inv.OwnsSprayer
                            && (_sprayHolding || mouse.leftButton.isPressed);
            if (spraying)
            {
                CancelInspect();
                CancelHandRight();
                return;
            }

            // UI 위에서는 땅 입력 무시 (메뉴 버튼 클릭은 허용)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                if (!mouse.rightButton.isPressed)
                {
                    CancelInspect();
                    CancelHandRight();
                }

                return;
            }

            if (_activeSlot == SlotId.Hand)
            {
                HandleHandRight(mouse, tickDelta);
                return;
            }

            CancelHandRight();
            HandleInspectHold(mouse, tickDelta);
        }

        void HandleHandRight(Mouse mouse, float tickDelta)
        {
            if (!mouse.rightButton.isPressed)
            {
                if (_handRightPending)
                {
                    // 짧게 뗐으면 땅 메뉴 (길게 개봉 완료 시 pending은 이미 해제됨)
                    if (_handRightHoldTicks < GroundMenuClickMaxTicks)
                        OpenGroundMenu(_handRightCell.x, _handRightCell.y);
                    CancelHandRight();
                }

                CancelInspect();
                return;
            }

            if (!TryGetCellUnderMouse(out int x, out int y))
            {
                CancelHandRight();
                CancelInspect();
                return;
            }

            var key = new Vector2Int(x, y);
            if (!_handRightPending || _handRightCell != key)
            {
                _handRightPending = true;
                _handRightCell = key;
                _handRightHoldTicks = 0f;
                CancelInspect();
            }

            _handRightHoldTicks += tickDelta;

            // 길게: 작물 스탯 개봉 (짧게 클릭 임계 이후부터 진행 표시)
            FarmCell cell = _grid.Get(x, y);
            if (cell.Kind != CellKind.Crop)
            {
                CancelInspect();
                return;
            }

            if (cell.StatsRevealed)
            {
                if (_handRightHoldTicks >= GroundMenuClickMaxTicks)
                    _status = $"스탯: {cell.Stats}";
                CancelInspect();
                return;
            }

            _inspectHolding = true;
            _inspectCell = key;
            _inspectHoldTicks = _handRightHoldTicks;
            float t = Mathf.Clamp01(_handRightHoldTicks / InspectHoldTicks);
            if (_handRightHoldTicks >= GroundMenuClickMaxTicks)
                _status = $"스탯 개봉… {Mathf.FloorToInt(t * 100f)}%";

            if (_handRightHoldTicks < InspectHoldTicks) return;

            cell.StatsRevealed = true;
            _revealPunchCell = key;
            _revealPunchTicks = GameTicks.FromSeconds(0.35f);
            _status = $"개봉! {cell.Stats}";
            CancelInspect();
            CancelHandRight();
        }

        void HandleInspectHold(Mouse mouse, float tickDelta)
        {
            if (!mouse.rightButton.isPressed)
            {
                CancelInspect();
                return;
            }

            if (!TryGetCellUnderMouse(out int x, out int y))
            {
                CancelInspect();
                return;
            }

            FarmCell cell = _grid.Get(x, y);
            if (cell.Kind != CellKind.Crop)
            {
                CancelInspect();
                return;
            }

            var key = new Vector2Int(x, y);
            if (cell.StatsRevealed)
            {
                _status = $"스탯: {cell.Stats}";
                CancelInspect();
                return;
            }

            if (!_inspectHolding || _inspectCell != key)
            {
                _inspectHolding = true;
                _inspectCell = key;
                _inspectHoldTicks = 0f;
            }

            _inspectHoldTicks += tickDelta;
            float t = Mathf.Clamp01(_inspectHoldTicks / InspectHoldTicks);
            _status = $"스탯 개봉… {Mathf.FloorToInt(t * 100f)}%";
            if (_inspectHoldTicks < InspectHoldTicks) return;

            cell.StatsRevealed = true;
            _revealPunchCell = key;
            _revealPunchTicks = GameTicks.FromSeconds(0.35f);
            _status = $"개봉! {cell.Stats}";
            CancelInspect();
        }

        void CancelInspect()
        {
            _inspectHolding = false;
            _inspectHoldTicks = 0f;
        }

        void CancelHandRight()
        {
            _handRightPending = false;
            _handRightHoldTicks = 0f;
        }

        void CancelSpray()
        {
            _sprayHolding = false;
            _sprayHoldTicks = 0f;
        }

        bool TryGetCellUnderMouse(out int x, out int y)
        {
            x = y = -1;
            Mouse mouse = Mouse.current;
            if (mouse == null || _cam == null) return false;
            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_cam.transform.position.z));
            x = Mathf.FloorToInt(world.x + 0.5f);
            y = Mathf.FloorToInt(world.y + 0.5f);
            return _grid.InBounds(x, y);
        }

        void RefreshBoardVisuals()
        {
            for (int y = 0; y < FarmGrid.Size; y++)
            for (int x = 0; x < FarmGrid.Size; x++)
            {
                var key = new Vector2Int(x, y);
                FarmCell cell = _grid.Get(x, y);
                SpriteRenderer sr = _cellViews[key];
                TextMesh label = _cellLabels[key];
                float baseScale = _cellBaseScale[key];
                Transform cellTf = sr.transform;

                if (cell.Kind == CellKind.Empty)
                {
                    sr.color = new Color(0.45f, 0.32f, 0.18f);
                    label.text = "";
                    cellTf.localScale = new Vector3(baseScale, baseScale, 1f);
                    continue;
                }

                if (cell.Kind == CellKind.Machine)
                {
                    var m = cell.Machine;
                    sr.color = new Color(0.35f, 0.4f, 0.55f);
                    if (m != null && m.OutputReady) label.text = "Mill\n완료!";
                    else if (m != null && m.HasActiveWip)
                        label.text = $"Mill\n{Mathf.FloorToInt(m.Progress01 * 100f)}%";
                    else label.text = "Mill";
                    label.color = Color.white;
                    cellTf.localScale = new Vector3(baseScale, baseScale, 1f);
                    continue;
                }

                switch (cell.Stage)
                {
                    case 0: sr.color = new Color(0.55f, 0.75f, 0.35f); break;
                    case 1: sr.color = new Color(0.25f, 0.65f, 0.25f); break;
                    default: sr.color = new Color(0.95f, 0.82f, 0.25f); break;
                }

                if (cell.Fertilizer.AnyActive)
                    sr.color = Color.Lerp(sr.color, new Color(0.6f, 0.4f, 0.9f), 0.35f);

                bool inspecting = _inspectHolding && _inspectCell == key && !cell.StatsRevealed;
                bool spraying = _sprayHolding && _sprayCell == key;

                if (cell.StatsRevealed)
                {
                    string f = cell.Fertilizer.AnyActive ? "F" : "";
                    label.text = $"S{cell.Stage}{f}\n{cell.Stats.GrowthSpeed}/{cell.Stats.DropRate}";
                    label.color = cell.IsMature ? Color.black : Color.white;
                }
                else if (inspecting || spraying)
                {
                    float t = spraying
                        ? Mathf.Clamp01(_sprayHoldTicks / FertilizerRules.SprayHoverTicks)
                        : Mathf.Clamp01(_inspectHoldTicks / InspectHoldTicks);
                    int fill = Mathf.Clamp(Mathf.RoundToInt(t * 3f), 0, 3);
                    label.text = $"S{cell.Stage}\n[{new string('■', fill)}{new string('□', 3 - fill)}]";
                    label.color = spraying ? new Color(0.7f, 0.5f, 1f) : Color.yellow;
                }
                else
                {
                    label.text = cell.Fertilizer.AnyActive ? $"S{cell.Stage}\n?F" : $"S{cell.Stage}\n?";
                    label.color = new Color(1f, 0.92f, 0.4f);
                }

                float scaleMul = 1f;
                if (inspecting || spraying)
                {
                    float t = spraying
                        ? Mathf.Clamp01(_sprayHoldTicks / FertilizerRules.SprayHoverTicks)
                        : Mathf.Clamp01(_inspectHoldTicks / InspectHoldTicks);
                    scaleMul = 1f + 0.06f * Mathf.Sin(Time.time * (12f + 20f * t)) * (0.4f + t);
                }
                else if (_revealPunchTicks > 0f && _revealPunchCell == key)
                {
                    float punchMax = GameTicks.FromSeconds(0.35f);
                    scaleMul = 1f + 0.25f * (_revealPunchTicks / punchMax);
                }

                cellTf.localScale = new Vector3(baseScale * scaleMul, baseScale * scaleMul, 1f);
            }
        }

        void RefreshHud()
        {
            _hudGold.text = $"골드: {_inv.Gold}";
            _hudTool.text = SlotStatus();
            _hudStatus.text = _status;
            if (_invButtonLabel != null && !_inventoryOpen)
                _invButtonLabel.text = "인벤토리";
        }

        static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Object.Destroy(root.GetChild(i).gameObject);
        }

        static Vector2 Ui(float x, float y) => new Vector2(x, y) * UiScale;

        static Sprite CreatePixelSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        static Text MakeText(Transform parent, string name, Vector2 anchoredPos, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(700f * UiScale, 100f * UiScale);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = Mathf.Max(1, Mathf.RoundToInt(fontSize * UiScale));
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Button MakeButton(Transform parent, string label, Vector2 anchoredPos,
            UnityEngine.Events.UnityAction onClick, float width = -1f)
        {
            if (width < 0f) width = 140f * UiScale;
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
            btn.onClick.AddListener(onClick);
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = Mathf.Max(1, Mathf.RoundToInt(13 * UiScale));
            text.raycastTarget = false;
            return btn;
        }
    }
}
