# PrototypeBattle / PrototypeEvn handoff

## 목적
- 이번 세션에서 실제로 바뀐 사항과 판단 기준을 기록한다.
- 다음 세션이 `Battle` 단독 실행 가능성을 해치지 않는 방향으로 후속 정리를 이어갈 수 있게 한다.
- 최상위 원칙은 아래 두 줄로 고정한다.
  - `Battle`은 `Env` 없이도 최소 플레이 가능한 기본 씬이어야 한다.
  - `Env`는 씬 배치와 연출 override를 제공하는 선택적 통합 씬이다.

## 현재 상태
- 레인 cargo는 고정 스폰 플랜 기반으로 생성되며, `CargoRevealDelay`를 이용해 pallet -> rail handoff 연출과 실제 레인 표시를 분리했다.
- 팔레트 적재와 pallet -> rail 반출 연출이 추가되었다.
- `LoadingDockEnvironmentAuthoring`에 strict contract가 들어가 있으며, 필요한 앵커와 렌더러를 직접 연결해야 한다.
- `BattleEnvironmentBinder`는 `PrototypeEvn` 씬에서 authoritative `LoadingDockEnvironmentAuthoring`만 찾도록 바뀌었고, runtime 생성 fallback은 제거된 상태다.
- `BattlePresentationBridge`의 레인 cargo prefab 해석은 `Env` profile 또는 기본 `Resources`만 사용하도록 정리되었다.
- 기존 `Enemy*` 계열 전투 파일명은 `Cargo*` 기준으로 정리되었다.

## 이번 세션 판단
- 이번 세션에서는 `Env` 쪽 책임을 강하게 키우는 방향으로 정리했지만, 그 결과 `Battle` 단독 테스트성과 기본 실행 보장이 약해질 수 있다는 문제가 드러났다.
- 다음 세션부터는 아래 기준으로 다시 정렬한다.

| 영역 | 책임 |
| --- | --- |
| `Battle` | 게임플레이 로직, ECS 시스템, `BattleViewAuthoring`, 판정선/스폰선, 기본 actor prefab, 기본 cargo prefab 해석, 독립 테스트 기준 |
| `Env` | 팔레트/트럭/레일/컨베이어의 씬 배치, 앵커, 연출용 오브젝트, 선택적 override |
| `Shared/Resources` | `Battle` 단독 테스트와 최소 실행을 위한 기본 상자 prefab 안전망 |

- `Env`는 “필수 의존 씬”이 아니라 “통합 연출 씬”으로 취급한다.
- `Battle`은 `Env`가 없어도 core gameplay를 수행할 수 있어야 한다.

## 최소 단독 실행 기준
- `PrototypeBattle`만 로드해도 레인 스폰, 이동, 처리, 실패, 보상이 동작해야 한다.
- 레인 cargo는 기본 `Resources` prefab으로 표시 가능해야 한다.
- `PrototypeEvn`이 없어도 게임이 멈추거나 치명적 예외로 중단되면 안 된다.
- pallet stack, pallet -> rail transfer, conveyor 스크롤, loading dock camera 같은 연출은 `Env`가 있을 때만 활성화되어도 된다.
- `Env`가 없을 때는 hard fail 대신 “연출 비활성” 상태로 넘어가야 한다.

## 소유권 기준

| `Battle`에 남길 것 | `Env`에 둘 것 | `Shared/Resources`에 둘 것 |
| --- | --- | --- |
| ECS 시스템 | `palletStackAnchor` | 기본 상자 prefab 세트 |
| `BattleViewAuthoring` | `transientCargoRoot` | `Battle` 단독 테스트용 공용 시각 리소스 |
| 판정선 / 스폰선 | `cargoSlotAnchors` | 기본 cargo prefab 안전망 |
| 기본 actor prefab | `laneEntryAnchors` |  |
| 기본 cargo prefab 해석 | `visibleLaneRailRoots` |  |
| 독립 실행 기준 | `conveyorBeltRenderers` |  |
|  | 팔레트/트럭/레일 메시와 씬 배치 |  |
|  | `laneCargoPrefabProfile` 같은 override용 profile |  |

## 현재 코드 기준 주의사항
- 현재 `BattleEnvironmentBinder`는 `PrototypeEvn`이 없으면 바인딩 실패 로그를 남긴다.
- 현재 `LoadingDockMiniGamePresenter`와 `LoadingDockConveyorPresenter`는 bound env 전제를 강하게 가진다.
- 현재 `BattlePresentationBridge`의 레인 cargo는 `Env` profile 또는 기본 `Resources`만 사용한다.
- 현재 `PrototypeEvn.unity`에는 strict contract를 만족하는 `LoadingDockEnvironmentAuthoring`가 들어가 있다.
- 이 상태는 “현재 구현 사실”을 기록한 것이고, 최종 목표 상태와 동일하다는 뜻은 아니다.

## 다음 세션 구현 티켓

### T1. Battle-only baseline 복구
- `PrototypeBattle`에서 `PrototypeEvn`이 없어도 레인 gameplay가 정상 동작하도록 binder/presenter를 fail-open 방향으로 정리한다.
- 연출 계층이 빠져도 core gameplay는 유지되도록 책임을 다시 나눈다.

### T2. Env strict contract 범위 축소
- strict contract는 `Env integration` 모드에서만 강제한다.
- `Battle-only` 실행에서는 필수 조건이 아니게 바꾼다.

### T3. 레인 cargo 기본 리소스 책임 명문화
- 기본 cargo prefab은 `Battle/Resources` 안전망으로 유지한다.
- `Env`의 `laneCargoPrefabProfile`은 override로 한정한다.

### T4. 테스트 계층 분리
- `Battle-only` PlayMode 테스트와 `Battle+Env` 통합 테스트를 분리한다.
- `Env`가 없을 때의 정상 동작과 `Env`가 있을 때의 연출 동작을 각각 검증한다.

## 검증 체크리스트

### Battle-only
- `PrototypeBattle` 단독 실행
- cargo 표시 정상
- gameplay 루프 정상
- `Env` 부재 시 치명적 예외 없음

### Battle+Env
- pallet 적재/반출 연출 정상
- lane entry anchor와 logical lane count 일치
- conveyor 스크롤 정상
- loading dock 슬롯 표시 정상

## 다음 세션 기본 가정
- 이 문서는 이번 세션 변경을 정당화하는 문서가 아니라, 다음 세션이 판단을 이어가기 위한 handoff 문서다.
- 현재 strict `Env` 중심 구현은 “현 상태 기록”으로 남기되, 목표 상태는 `Battle` 독립 실행 보장으로 다시 정렬한다.
- 기본 cargo prefab 안전망은 유지하는 방향을 기본값으로 둔다.
