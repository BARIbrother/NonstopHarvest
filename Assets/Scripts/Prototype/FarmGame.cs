using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GuideToNonstopHarvest.Prototype
{
    /// <summary>
    /// 프로토타입 진입점. 밭·퀵슬롯·Mill·비료 뿌리개 UI/입력을 런타임 조립.
    /// </summary>
    public sealed class FarmGame : MonoBehaviour
    {
        const float CellWorldSize = 1f;
        const float Gap = 0.05f;
        const float UiScale = 0.7f;
        const float InspectHoldTicks = 20f;
        const int QuickSlotCount = 5;

        enum QuickTool
        {
            Plant = 0,
            Breed = 1,
            Spray = 2,
            Mill = 3,
            None = 4
        }

        FarmGrid _grid;
        InventoryState _inv;
        QuickTool _tool = QuickTool.Plant;
        int _selectedSeedIndex;
        string _status = "퀵슬롯으로 도구 선택 | 우클릭 1초: 스탯 공개";

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

        Text _hudGold;
        Text _hudInv;
        Text _hudStatus;
        Text _hudTool;
        Transform _seedButtonRoot;
        Transform _quickSlotRoot;
        readonly Button[] _quickButtons = new Button[QuickSlotCount];
        readonly Text[] _quickLabels = new Text[QuickSlotCount];
        Button _buySprayerBtn;

        Sprite _pixelSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindFirstObjectByType<FarmGame>() != null) return;
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
            RefreshSeedButtons();
            RefreshQuickSlots();
            RefreshHud();
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
            if (FindFirstObjectByType<EventSystem>() != null) return;
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
                tm.text = "";

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
            _hudInv = MakeText(canvasGo.transform, "Inv", Ui(10, -40), 16);
            _hudTool = MakeText(canvasGo.transform, "Tool", Ui(10, -130), 16);
            _hudStatus = MakeText(canvasGo.transform, "Status", Ui(10, -160), 15);
            _hudStatus.color = new Color(0.85f, 0.9f, 0.7f);

            MakeButton(canvasGo.transform, "벼 판매", Ui(10, -210), () =>
            {
                int g = _inv.SellAllRice();
                _status = g > 0 ? $"벼 판매 +{g}G" : "팔 벼 없음";
            });
            MakeButton(canvasGo.transform, "지지대(5G)", Ui(110, -210), () =>
            {
                _status = _inv.TryBuySupport() ? "지지대 +1" : "골드 부족";
            });
            MakeButton(canvasGo.transform, "Mill(100G)", Ui(220, -210), () =>
            {
                if (_inv.TryBuyMill())
                {
                    _status = "Mill 획득. 퀵슬롯 Mill로 빈 칸에 배치";
                    _tool = QuickTool.Mill;
                }
                else _status = "골드 부족";
            }, 120f * UiScale);

            _buySprayerBtn = MakeButton(canvasGo.transform, "뿌리개(0G)", Ui(10, -250), () =>
            {
                if (_inv.TryBuySprayer())
                {
                    _status = "비료 뿌리개 획득";
                    _tool = QuickTool.Spray;
                }
                else if (!_inv.SprayerUnlocked) _status = "첫 비료 후 해금";
                else if (_inv.OwnsSprayer) _status = "이미 보유";
                else _status = "구매 실패";
                RefreshQuickSlots();
            });

            var seedRootGo = new GameObject("SeedButtons");
            seedRootGo.transform.SetParent(canvasGo.transform, false);
            var srt = seedRootGo.AddComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(1, 1);
            srt.anchoredPosition = Ui(-10, -10);
            srt.sizeDelta = Ui(280, 400);
            _seedButtonRoot = seedRootGo.transform;
            MakeText(seedRootGo.transform, "SeedTitle", Vector2.zero, 16).text = "씨앗 목록";

            var qsGo = new GameObject("QuickSlots");
            qsGo.transform.SetParent(canvasGo.transform, false);
            var qrt = qsGo.AddComponent<RectTransform>();
            qrt.anchorMin = new Vector2(0.5f, 0f);
            qrt.anchorMax = new Vector2(0.5f, 0f);
            qrt.pivot = new Vector2(0.5f, 0f);
            qrt.anchoredPosition = new Vector2(0f, 12f);
            qrt.sizeDelta = new Vector2(520f * UiScale, 50f * UiScale);
            _quickSlotRoot = qsGo.transform;

            string[] names = { "심기", "교접", "뿌리개", "Mill", "-" };
            float slotW = 90f * UiScale;
            float startX = -2.5f * slotW;
            for (int i = 0; i < QuickSlotCount; i++)
            {
                int idx = i;
                var btn = MakeButton(_quickSlotRoot, names[i], new Vector2(startX + i * (slotW + 6f), 0f),
                    () => SelectTool((QuickTool)idx), slotW);
                // MakeButton uses top-left anchors; re-anchor to bottom center parent
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(startX + i * (slotW + 6f) + slotW * 0.5f, 0f);
                _quickButtons[i] = btn;
                _quickLabels[i] = btn.GetComponentInChildren<Text>();
            }
        }

        void SelectTool(QuickTool tool)
        {
            if (tool == QuickTool.Spray && !_inv.OwnsSprayer)
            {
                _status = _inv.SprayerUnlocked ? "상점에서 뿌리개(0G) 구매" : "첫 비료 후 뿌리개 해금";
                return;
            }

            if (tool == QuickTool.Mill && _inv.MillsInInventory <= 0)
            {
                _status = "Mill 없음 (100G 구매)";
                return;
            }

            if (tool == QuickTool.None) return;
            _tool = tool;
            CancelSpray();
            _status = ToolStatus(_tool);
        }

        static string ToolStatus(QuickTool t) => t switch
        {
            QuickTool.Plant => "도구: 심기/수확",
            QuickTool.Breed => "도구: 교접 지지대",
            QuickTool.Spray => "도구: 비료 뿌리개 (좌클릭 호버 0.5초)",
            QuickTool.Mill => "도구: Mill 배치",
            _ => "도구: -"
        };

        void RefreshQuickSlots()
        {
            if (_quickLabels[0] == null) return;
            _quickLabels[0].text = Mark(QuickTool.Plant, "심기");
            _quickLabels[1].text = Mark(QuickTool.Breed, "교접");
            _quickLabels[2].text = _inv.OwnsSprayer
                ? Mark(QuickTool.Spray, "뿌리개")
                : (_inv.SprayerUnlocked ? "뿌리개?" : "잠김");
            _quickLabels[3].text = _inv.MillsInInventory > 0
                ? Mark(QuickTool.Mill, $"Mill×{_inv.MillsInInventory}")
                : "Mill";
            _quickLabels[4].text = "-";

            if (_buySprayerBtn != null)
                _buySprayerBtn.gameObject.SetActive(_inv.SprayerUnlocked && !_inv.OwnsSprayer);
        }

        string Mark(QuickTool t, string label) => _tool == t ? $"> {label}" : label;

        void RefreshSeedButtons()
        {
            for (int i = _seedButtonRoot.childCount - 1; i >= 0; i--)
            {
                Transform c = _seedButtonRoot.GetChild(i);
                if (c.name == "SeedTitle") continue;
                Destroy(c.gameObject);
            }

            var seeds = _inv.Seeds;
            if (seeds.Count == 0)
            {
                _selectedSeedIndex = -1;
                return;
            }

            if (_selectedSeedIndex < 0 || _selectedSeedIndex >= seeds.Count)
                _selectedSeedIndex = 0;

            for (int i = 0; i < seeds.Count; i++)
            {
                int index = i;
                SeedStack stack = seeds[i];
                string mark = index == _selectedSeedIndex ? ">" : " ";
                MakeButton(_seedButtonRoot, $"{mark}씨앗 {stack.Stats} x{stack.Count}",
                    Ui(0, -30 - 40 * i), () =>
                    {
                        _selectedSeedIndex = index;
                        _tool = QuickTool.Plant;
                        _status = $"씨앗 선택: {stack.Stats}";
                        RefreshSeedButtons();
                    }, 270f * UiScale);
            }
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

            if (_tool == QuickTool.Spray)
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
            if (mouse.rightButton.wasPressedThisFrame || mouse.rightButton.isPressed)
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
                _status = "작물 칸에만 비료 가능";
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
            _status = $"비료 살포 중… {Mathf.FloorToInt(t * 100f)}%";

            if (_sprayHoldTicks < FertilizerRules.SprayHoverTicks) return;

            if (_grid.TrySprayFertilizer(x, y, _inv))
                _status = $"비료 살포 완료 ({x},{y})";
            else
                _status = _inv.Fertilizer <= 0 ? "비료 없음 (Mill로 생산)" : "살포 실패";

            CancelSpray();
        }

        void HandleCellClick(int x, int y)
        {
            FarmCell cell = _grid.Get(x, y);

            if (cell.Kind == CellKind.Mill)
            {
                if (_grid.TryCollectMillOutput(x, y, _inv))
                {
                    _status = "비료 +1";
                    RefreshQuickSlots();
                    return;
                }

                if (_grid.TryStartMill(x, y, _inv))
                    _status = "Mill 가공 시작 (5초)";
                else if (cell.MillProcessing)
                    _status = $"Mill 가공 중 {Mathf.FloorToInt(cell.MillProgress * 100f)}%";
                else if (cell.MillOutputReady)
                    _status = "클릭해서 비료 수령";
                else
                    _status = "벼(작물) 필요";
                return;
            }

            if (cell.IsMature && _tool == QuickTool.Plant)
            {
                if (_grid.TryHarvest(x, y, _inv))
                    _status = $"수확 ({x},{y})";
                return;
            }

            if (!cell.IsEmpty)
            {
                string fert = cell.Fertilizer.AnyActive ? " [비료]" : "";
                string stats = cell.StatsRevealed ? cell.Stats.ToString() : "???";
                _status = $"단계{cell.Stage} {stats}{fert} {Mathf.FloorToInt(cell.Progress * 100f)}%";
                return;
            }

            if (_tool == QuickTool.Plant)
            {
                if (_selectedSeedIndex < 0 || _selectedSeedIndex >= _inv.Seeds.Count)
                {
                    _status = "심을 씨앗 없음";
                    return;
                }

                RiceStats stats = _inv.Seeds[_selectedSeedIndex].Stats;
                if (_grid.TryPlant(x, y, stats, _inv))
                {
                    _status = $"심기 {stats}";
                    RefreshSeedButtons();
                }
                else _status = "심기 실패";
                return;
            }

            if (_tool == QuickTool.Breed)
            {
                if (_grid.TryPlaceBreedingSupport(x, y, _inv))
                    _status = "교접 성공 → 우클릭 1초로 스탯 공개";
                else
                    _status = _inv.Supports <= 0 ? "지지대 없음" : "부모(상하좌우) ≥1 필요";
                return;
            }

            if (_tool == QuickTool.Mill)
            {
                if (_grid.TryPlaceMill(x, y, _inv))
                {
                    _status = "Mill 배치";
                    if (_inv.MillsInInventory <= 0) _tool = QuickTool.Plant;
                    RefreshQuickSlots();
                }
                else _status = "Mill 배치 실패";
            }
        }

        void HandleRightInput(float tickDelta)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                CancelInspect();
                return;
            }

            // 뿌리개 살포 중 우클릭은 중단만 (스탯 개봉 안 함)
            if (_tool == QuickTool.Spray && (_sprayHolding || mouse.leftButton.isPressed))
            {
                CancelInspect();
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                CancelInspect();
                return;
            }

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
            _status = $"스탯 개봉 중… {Mathf.FloorToInt(t * 100f)}%";
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

                if (cell.Kind == CellKind.Mill)
                {
                    sr.color = new Color(0.35f, 0.4f, 0.55f);
                    if (cell.MillOutputReady) label.text = "Mill\n완료!";
                    else if (cell.MillProcessing)
                        label.text = $"Mill\n{Mathf.FloorToInt(cell.MillProgress * 100f)}%";
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
            _hudInv.text =
                $"벼:{_inv.RiceCrops}  지지대:{_inv.Supports}  비료:{_inv.Fertilizer}\n" +
                $"Mill인벤:{_inv.MillsInInventory}  씨앗종류:{_inv.Seeds.Count}";
            _hudTool.text = ToolStatus(_tool);
            _hudStatus.text = _status;
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
            rt.sizeDelta = new Vector2(700f * UiScale, 80f * UiScale);
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
            text.fontSize = Mathf.Max(1, Mathf.RoundToInt(14 * UiScale));
            text.raycastTarget = false;
            return btn;
        }
    }
}
