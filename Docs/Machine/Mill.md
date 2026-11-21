# Mill (제분기)

| 항목 | 내용 |
|------|------|
| 구매 방식 | 상점 |
| 구매 가격 | 100골드 |
| 차지하는 칸 수 | 1×1 |
| 기능 설명 | 작물을 빻아 비료를 생성. 비료 효과는 `01_System.md` §5.5 |

시간 단위: **1초 = 20틱**.

레퍼런스 메커니즘: DungeonFront `Machine` / `SmelterMachine`  
(인풋·아웃풋 포트, `recipeTime` vs `workSpeed`, Complete→Start 틱 페이즈).

### 동작 개요

```
배치(상점 Mill) → 클릭으로 벼를 inputPort에 투입
  → TickStart: 인풋 소비, WIP 시작
  → 매 틱 TickComplete: progress += workSpeed
  → progress ≥ recipeTime 이면 outputPort에 비료
  → 클릭으로 비료 수령
```

| 항목 | 값 |
|------|-----|
| workSpeed | **1** / 틱 |
| recipeTime | **100** |
| 틱 레이트 | 20틱/초 |
| 체감 시간 | 100 ÷ (20×1) = **5초** |

### 레시피

#### 작물 → 비료 (`mill_crop_to_fertilizer`)

| 구분 | 내용 |
|------|------|
| 인풋 | 작물(벼 산출물) × 1 |
| 아웃풋 | 비료 × 1 |
| 가공 시간 | recipeTime **100** (≈5초) |

### 코드

- `MachineBase` — Complete/Start 페이즈
- `MillMachine` — 위 레시피 고정
- `FarmGrid.Tick` — 정수 틱마다 기계 Complete → Start
