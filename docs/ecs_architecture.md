# ECS 아키텍처 문서

## ECS 적용 범위

### 원칙
- **실시간 전투 런타임은 ECS(DOTS) 중심으로 설계한다.**
- 메타 화면과 메뉴 UI는 ECS를 무리하게 확장하지 않고, **전투에 주입되는 데이터 구조와 저장 모델을 ECS 친화적으로 정리**한다.
- 즉, “전투는 ECS”, “메타는 전투 초기값을 생산하는 데이터 계층”, “3D 표현은 ECS 상태를 따라가는 프레젠테이션 계층”으로 본다.

## Entity 분류
| 분류 | 설명 | 예시 |
| --- | --- | --- |
| Core Singleton | 전투 전역 상태를 가지는 단일 엔티티 | `BattleTimer`, `BattleState`, `SpawnState` |
| Player Entity | 플레이어 본체와 그 상태 | 플레이어 레인, 라이프, 콤보, 스킬 로드아웃 |
| Enemy Entity | 개별 적 개체 | 저체력 고속형, 중간형, 고체력 저속형 |
| Lane Utility Entity | 레인별 상태/이펙트/점유 정보 | 레인 하이라이트, 위험 표시 |
| Event Entity | 짧게 생성/소멸되는 전투 이벤트 | 적 처치, 라이프 손실, 보상 적립 이벤트 |
| Spawn Directive Entity | 생성 예약과 스폰 상태 | 적 스폰 명령, 랜덤 스폰 예약 |
| Meta Injection Entity | 전투 시작 시 메타 결과를 전달하는 데이터 캐리어 | 활성 노드 효과, 경제 보정값 |
| Presentation Proxy Entity | 3D 모델과 이펙트를 ECS 상태에 연결하는 표현 프록시 | 플레이어 뷰, 적 뷰, 레인 하이라이트 |

## Component 설계

### 핵심 전투 컴포넌트
| 컴포넌트 | 타입 성격 | 목적 |
| --- | --- | --- |
| `LaneIndex` | `IComponentData` | 현재 레인 식별 |
| `LaneMoveState` | `IComponentData` | 이동 중 여부, 목표 레인, 진행 상태 관리 |
| `MoveSpeed` | `IComponentData` | 플레이어/적 이동 속도 |
| `VerticalPosition` | `IComponentData` | 적의 상단-하단 진행도 표현 |
| `EnemyHealth` | `IComponentData` | 적 체력 관리 |
| `AttackCooldown` | `IComponentData` | 플레이어 자동공격 주기 관리 |
| `AutoAttackProfile` | `IComponentData` | 공격 속도, 기본 타수, 확장 훅 저장 |
| `TargetSelectionState` | `IComponentData` | 현재 레인의 가장 앞 적 타겟 관리 |
| `LifeState` | `IComponentData` | 플레이어 라이프 관리 |
| `ComboState` | `IComponentData` | 콤보 수와 종료 상태 저장 |
| `BattleRewardState` | `IComponentData` | 전투 중 누적 보상 메모 |
| `SkillLoadoutState` | `IComponentData` | 메타 성장 적용값 |
| `StageProgressState` | `IComponentData` | 남은 시간, 잔여 적 수, 종료 여부 관리 |
| `SpawnTimerState` | `IComponentData` | 랜덤 스폰 간격과 다음 스폰 시점 관리 |
| `PresentationAnchor` | `IComponentData` | 3D 월드 위치와 레인 기준 앵커 동기화 |
| `LaneVisualState` | `IComponentData` | 레인 강조, 위협 표시, 하단 방어선 상태 전달 |

### 버퍼/이벤트성 컴포넌트
| 컴포넌트 | 타입 성격 | 목적 |
| --- | --- | --- |
| `LaneMoveCommandBufferElement` | `IBufferElementData` | 좌우 이동 입력 예약 저장 |
| `PendingSpawnBufferElement` | `IBufferElementData` | 스폰 예약 목록 저장 |
| `ActiveModifierBufferElement` | `IBufferElementData` | 메타 노드/버프 목록 |
| `EnemyDefeatedEvent` | Event Entity 또는 ECB 생성 데이터 | 적 처치 결과 전파 |
| `LifeLostEvent` | Event Entity | 하단 돌파로 인한 라이프 손실 처리 |
| `RewardEvent` | Event Entity | 보상 적립 처리 |

## 3D 표현 계층 원칙

### 표현 분리 기준
| 항목 | 기준 |
| --- | --- |
| 시뮬레이션 | 레인, 이동, 타겟, 공격, 라이프는 ECS 상태가 기준 |
| 프레젠테이션 | 3D 모델, 애니메이션, VFX, 머티리얼 강조는 표현 계층에서 처리 |
| 카메라 | 프로토타입에서는 고정 카메라, ECS 추적 대상 없음 |
| 좌표 체계 | 레인 인덱스를 월드 X축 위치로 매핑하고, 적 진행도는 월드 Z축 기준으로 표현 |

### 3D 전장 좌표 제안
| 축 | 용도 |
| --- | --- |
| X | 4개 레인 위치 |
| Y | 캐릭터/적의 시각적 높이 |
| Z | 상단 스폰에서 하단 방어선으로 내려오는 진행 방향 |

## System 설계

### 권장 시스템 그룹
| 그룹 | 시스템 | 역할 |
| --- | --- | --- |
| Bootstrap | `BattleBootstrapSystem` | 전투 초기 singleton, 플레이어, 스테이지 상태 준비 |
| Input | `PlayerInputCollectSystem` | 좌우 입력을 ECS 버퍼로 변환 |
| Input | `PlayerLaneQueueSystem` | 예약 입력을 실제 이동 명령으로 정리 |
| Stage | `BattleTimerSystem` | 60초 생존전 타이머와 종료 상태 갱신 |
| Stage | `EnemySpawnSystem` | 랜덤 레인 스폰 명령 실행 |
| Movement | `PlayerLaneMoveSystem` | 플레이어 레인 이동 처리 |
| Movement | `EnemyAdvanceSystem` | 적 하강 진행 |
| Targeting | `TargetSelectionSystem` | 현재 레인의 가장 앞 적 선택 |
| Combat | `AutoAttackSystem` | 이동 완료 후 자동공격과 공격 쿨다운 처리 |
| Resolve | `EnemyHealthResolveSystem` | 적 체력 감소와 처치 반영 |
| Resolve | `LifeAndComboResolveSystem` | 하단 돌파 시 라이프 감소와 콤보 종료 처리 |
| Resolve | `RewardAccumulateSystem` | 전투 중 보상 누적 |
| Presentation | `PresentationSyncSystem` | ECS 상태를 3D 모델, 레인 강조, 방어선 연출에 반영 |
| Flow | `BattleEndCheckSystem` | 시간 종료/라이프 0 여부 확인 |
| Flow | `BattleResultSystem` | 종료 결과 저장 및 메타 전달 |

### 시스템 순서 메모
1. 입력 수집
2. 이동 예약 정리
3. 타이머/스폰 갱신
4. 플레이어 이동
5. 적 이동
6. 타겟 선택
7. 자동공격 처리
8. 적 체력/라이프/콤보/보상 반영
9. 3D 표현 동기화
10. 전투 종료 여부 확인

## Authoring/Baker 필요 요소
| Authoring 대상 | Baker 결과 | 목적 |
| --- | --- | --- |
| `StageDefinitionAuthoring` | 생존 시간, 스폰 규칙 BlobAsset 참조 singleton | 스테이지 데이터 주입 |
| `EnemyArchetypeAuthoring` | 적 기본 체력/속도 BlobAsset | 적 프리셋 정의 |
| `SpawnProfileAuthoring` | 랜덤 스폰 간격/가중치 BlobAsset | 스폰 규칙 정의 |
| `SkillTreeDefinitionAuthoring` | 메타 노드 정의 BlobAsset | 성장 데이터 일관화 |
| `BattleConfigAuthoring` | 전역 전투 밸런스 singleton | 초기 밸런스 조정 |
| `LaneVisualAuthoring` | 레인 메쉬, 발광, 방어선 강조 프리셋 | 3D 레인 가독성 유지 |
| `EnemyViewAuthoring` | 적 모델, 실루엣, 타입별 색상 정보 | 3D 적 식별성 유지 |

### Baker 설계 원칙
- Baker는 전투 중 바뀌지 않는 정적 데이터를 BlobAsset으로 변환하는 데 집중한다.
- 플레이어 해금 상태나 세션별 보상값은 Baker 대상이 아니라 런타임 로드 데이터다.

## 데이터 원본 관리 방식
| 데이터 종류 | 원본 | 런타임 사용 방식 |
| --- | --- | --- |
| 스테이지 구조 | ScriptableObject 또는 전용 authoring asset | Bake 시 BlobAsset 변환 |
| 적 프리셋 | ScriptableObject | BlobAsset + archetype preset |
| 랜덤 스폰 규칙 | ScriptableObject | `SpawnTimerState` 초기화 |
| 스킬 트리 정의 | ScriptableObject | BlobAsset 참조 |
| 3D 뷰 프리셋 | Authoring asset | 프레젠테이션 프록시에 주입 |
| 플레이어 해금 상태 | Save file | 전투 진입 시 ECS 초기값 주입 |
| 전투 결과 | Runtime save model | 메타 화면으로 반환 |

## BlobAsset 사용 후보
| 후보 | 이유 |
| --- | --- |
| `StageWaveBlob` | 생존 시간과 스폰 규칙을 읽기 전용으로 관리 가능 |
| `EnemyPatternBlob` | 현재는 체력/속도 프리셋만 담고 후속 확장에 대응 가능 |
| `SpawnProfileBlob` | 랜덤 스폰 간격과 레인 가중치를 고정 데이터로 관리 가능 |
| `SkillTreeBlob` | 노드 정의, 선행 조건, 효과 타입을 안정적으로 참조 가능 |
| `RewardCurveBlob` | 단계별 보상량과 계수 테이블을 버전 관리하기 쉽다 |
| `LaneVisualBlob` | 레인 폭, 발광 강도, 방어선 시각 규칙을 고정 데이터로 관리 가능 |

## 전투 루프에서 시스템 흐름

### 전투 시작
1. `BattleBootstrapSystem`이 메타 저장 데이터와 스테이지 선택 결과를 읽는다.
2. 플레이어 엔티티와 전역 singleton을 생성한다.
3. 선택된 스킬 노드 효과를 `SkillLoadoutState`와 modifier buffer에 기록한다.
4. `EnemySpawnSystem`과 `BattleTimerSystem`이 첫 스폰과 60초 타이머를 준비한다.

### 전투 진행
1. 입력 시스템이 좌우 버튼 입력을 `LaneMoveCommandBufferElement`에 기록한다.
2. `PlayerLaneQueueSystem`이 예약 입력을 실제 이동 명령으로 정리한다.
3. `PlayerLaneMoveSystem`이 한 칸 이동과 이동 중 공격 불가 상태를 처리한다.
4. `EnemyAdvanceSystem`이 적 위치를 이동시킨다.
5. `TargetSelectionSystem`이 현재 레인의 가장 앞 적을 선택한다.
6. `AutoAttackSystem`이 이동 완료 후 공격 쿨다운 기준으로 자동공격을 실행한다.
7. `EnemyHealthResolveSystem`, `LifeAndComboResolveSystem`, `RewardAccumulateSystem`이 결과를 반영한다.
8. `PresentationSyncSystem`이 3D 캐릭터, 적 위치, 레인 강조, 하단 방어선 연출을 동기화한다.

### 전투 종료
1. `BattleEndCheckSystem`이 시간 종료 또는 라이프 0 여부를 판단한다.
2. `BattleResultSystem`이 재화, 성과, 해금 조건을 저장 모델로 변환한다.
3. 메타 화면은 이 결과를 받아 다음 성장 선택으로 연결한다.

## MVP 기준 ECS 구현 우선순위
| 단계 | 구현 범위 | 목적 |
| --- | --- | --- |
| 1 | `Player`, `Enemy`, `Lane`, `AutoAttack`, `Life` 코어 | 전투 정체성 검증 |
| 2 | 랜덤 스폰과 3종 적 프리셋 | 반복 가능한 스테이지 확보 |
| 3 | 콤보 UI와 보상 정산 | 반복 동기 부여 |
| 4 | 메타 노드 6개 반영 | 성장-전투 연결 검증 |
| 5 | 오프라인 보상과 경제 보정 | 방치형 감성 확인 |

## ECS 채택 이유와 주의점

### 채택 이유
| 이유 | 설명 |
| --- | --- |
| 다수 적 처리 | 레인별 적이 동시에 내려오므로 개체 수 증가에 강해야 한다 |
| 일관된 자동공격 파이프라인 | 입력, 이동, 타겟 선택, 공격, 라이프 결과를 데이터 흐름으로 정리하기 쉽다 |
| 스테이지 데이터화 | 스폰 규칙, 적 프리셋, 보상 테이블을 데이터 중심으로 관리하기 좋다 |
| 성장 효과 주입 | 메타 성장 결과를 초기 컴포넌트 변경으로 연결하기 쉽다 |
| 3D 표현 분리 | 시뮬레이션과 연출을 분리해 가독성과 유지보수를 동시에 확보하기 쉽다 |

### 주의점
| 주의점 | 설명 | 대응 |
| --- | --- | --- |
| UI까지 ECS 강박 적용 | 생산성이 떨어질 수 있음 | 전투 런타임만 ECS 집중 |
| 이벤트 엔티티 남발 | 디버깅과 메모리 비용 증가 | 짧은 수명의 표준 이벤트 패턴 정의 |
| 너무 이른 일반화 | 초기 속도 저하 | MVP 단계에서는 적 타입 3종만 먼저 고정 |
| 하이브리드 표현 계층 혼선 | 시각 연출과 ECS 상태가 어긋날 수 있음 | Presentation sync 시스템 분리 |

## 향후 확장 포인트
- `PierceCount`로 한 레인 내 다중 타격 구조를 확장할 수 있다.
- `AttackCoverage`로 인접 레인/멀티레인 공격 범위를 확장할 수 있다.
- `TemporaryCombatBuff`로 콤보 기반 전투 중 강화 구조를 연결할 수 있다.

## 실행 우선순위
1. 전투 코어 엔티티와 singleton 설계부터 고정한다.
2. `PlayerLaneMoveSystem`, `TargetSelectionSystem`, `AutoAttackSystem` 3개를 먼저 구현한다.
3. 스테이지/적/스폰 데이터를 BlobAsset 파이프라인으로 바로 연결한다.
4. 메타 성장은 전투 진입 시 modifier 주입 방식으로만 우선 반영한다.
