# 적재장 구현 문서

## 목적
- 이 문서는 상하차 구역의 구현 방식과 현재 적재장 미니게임 구조를 정리한다.
- 기준 구현은 `PrototypeSessionRuntime`의 도크 큐와 `LoadingDockMiniGamePresenter`의 3D 뷰 동기화다.

## 적재장 역할
- 레인에서 성공 처리한 물류를 따로 모아 두는 후속 작업 공간이다.
- 플레이어는 `Q`로 레인과 적재장 사이를 왕복할 수 있다.
- 적재장 미니게임은 큐에 쌓인 물류를 클릭해 `delivered` 상태로 보내는 방식이다.

## 공간 구성
- 적재장 구역은 `CargoBayRoot`와 `TruckBayRoot`로 분리된다.
- `cargoSlotAnchors` 5개가 고정 적재 슬롯 위치를 정의한다.
- 같은 슬롯은 항상 같은 위치를 사용하며, 물류가 다른 자리로 재정렬되지 않는다.

## 큐 구조
- 큐는 두 층으로 구성된다.
  - 활성 슬롯 5칸
  - backlog FIFO 대기열
- 레인에서 물류를 성공 처리하면 즉시 도크 큐에 들어간다.
- 빈 슬롯이 있으면 가장 낮은 index 슬롯부터 채운다.
- 슬롯이 모두 차 있으면 backlog로 간다.

## 고정 슬롯 규칙
- 슬롯 위치는 고정이다.
- delivered가 발생하면 해당 슬롯만 비워진다.
- backlog가 있으면 그 비워진 같은 슬롯에 다음 물류가 들어간다.
- 기존에 다른 슬롯에 있던 물류는 앞으로 당겨지지 않는다.

## 미니게임 구조
- 렌더
  - 적재장 3D 물류 뷰는 현재 구역과 상관없이 항상 세션 큐를 반영한다.
  - 즉 레인에 있을 때도 적재장 자체에는 물류가 계속 쌓인다.
- 입력
  - 클릭 delivered 입력은 `ActiveInLoadingDock` 상태에서만 허용한다.
  - 레인 상태에서는 적재장 뷰가 보여도 상호작용은 잠겨 있다.
- 결과
  - 클릭된 cargo는 delivered 처리된다.
  - 화면에서는 즉시 사라지고, 실제 구현은 종류별 오브젝트 풀로 반환한다.
  - backlog가 있으면 같은 slot에 새 cargo가 바로 보충된다.

## 주요 타입
- `LoadingDockCargoKind`
  - `Standard`, `Fragile`, `Heavy`
- `LoadingDockCargoQueueEntry`
  - 큐 엔트리의 `EntryId`, `Kind`
- `LoadingDockActiveCargoSlotSnapshot`
  - 현재 활성 slot의 `SlotIndex`, `EntryId`, `Kind`
- `LoadingDockQueueSnapshot`
  - `BacklogCount`, `ActiveSlotCount`, `TotalCount`

## 주요 구현 책임
- `PrototypeSessionRuntime`
  - 도크 큐, active slot, backlog, delivered 처리 책임
- `LoadingDockMiniGamePresenter`
  - slot snapshot을 읽어 적재장 3D 오브젝트를 유지
  - 도크 active 상태에서만 클릭 입력 처리
- `LoadingDockCargoViewPool`
  - 타입별 공유 프리팹을 풀링해 재사용

## 시각 표현
- 적재장 물류는 레인 물류와 같은 공유 프리팹 세트를 사용한다.
- `Standard`, `Fragile`, `Heavy`는 동일한 타입이면 레인과 적재장에서 같은 외형을 가진다.
- 현재 기본 프리팹 색상은 아래와 같다.
  - `Standard`: 주황
  - `Fragile`: 파랑
  - `Heavy`: 회색

## 현재 주의점
- 적재장 뷰는 상시 동기화되지만, 레인 카메라에서 실제로 얼마나 잘 보이는지는 카메라 구도에 따라 달라진다.
- 적재장 미니게임은 현재 클릭 delivered만 구현되어 있고, 드래그/연타 규칙은 후속 작업 대상이다.
- 큐는 배틀 세션 전용 데이터이며 허브 저장 대상이 아니다.
