# 허브 스킬트리 구조 문서

## 목적
- 이 문서는 현재 허브 메타 진행 구조와 3탭 스킬트리 구현 방식을 정리한다.
- 기준 구현은 `DefaultMetaProgressionCatalog`와 3분할 tree asset 구조다.

## 허브 역할
- 전투 밖에서 현재 성장 상태를 확인하고 다음 세션 준비를 하는 공간이다.
- 스킬트리, 현재 재화, 직전 결과, 시작 버튼을 포함한다.

## 상위 구조
- 허브 스킬트리는 3개의 큰 탭으로 구성된다.
  - `물류 센터 성능`
  - `사람 성능`
  - `로봇 성능`
- 각 탭 아래에는 브랜치 트리가 배치된다.
- 저장 기준은 기존 노드 ID를 그대로 사용하고, 탭은 표시 계층이다.

## 3탭과 브랜치 매핑
- `물류 센터 성능`
  - `경영`
- `사람 성능`
  - `체력`
  - `근력`
  - `이동`
  - `숙련`
- `로봇 성능`
  - `자동화`

## 데이터 구조
- 루트 카탈로그
  - `DefaultMetaProgressionCatalog.asset`
  - 전역 메타 정보와 3개 트리 자산 참조를 가진다.
- 탭별 트리 자산
  - `CenterMetaProgressionTree.asset`
  - `HumanMetaProgressionTree.asset`
  - `RobotMetaProgressionTree.asset`
- 런타임 노출
  - 외부에서는 계속 `skillTabs`, `skillBranches`, `skillNodes`를 읽는다.
  - 실제 내부 데이터는 3개 트리 자산을 병합한 결과다.

## 대표 타입
- `SkillTreeTabId`
  - `Center`, `Human`, `Robot`
- `SkillBranchId`
  - `Vitality`, `Strength`, `Mobility`, `Mastery`, `Management`, `Automation`
- `SkillNodeDefinition`
  - 개별 노드 정의
- `StartingProgressionDefinition`
  - 새 세이브 시작 시 기본 해금 상태

## 상하차 해금 구조
- `management.loading_dock_unlock`
  - `경영` 브랜치의 센터 오픈 노드다.
- 현재 기본 시작 진행 상태에서는 이 노드가 해금된 상태로 들어간다.
- 즉 프로토타입에서는 상하차 구역이 기본 제공 기능처럼 열려 있다.

## 허브 UI 구조
- 탭 전환
  - 3개 상위 탭 중 하나를 선택
- 트리 표시
  - 선택한 탭에 속한 브랜치와 노드만 렌더링
- 상세 패널
  - 선택 노드 설명
  - 현재 재화와 결과 요약
- 시작 버튼
  - 현재 메타 상태를 유지한 채 전투 씬으로 이동

## 전투 반영 방식
- 허브에서 해금된 노드는 전투 시작 시 런타임 집계 결과로 변환된다.
- 대표 반영 항목
  - 세션 작업시간
  - 최대 처리 무게
  - 레인 이동 시간
  - 보상/패널티 배율
  - 해금된 레인 수
  - 상하차/자동화 플래그

## 현재 구현 기준 요약
- 카탈로그 루트는 유지하고 실제 트리 데이터는 3개 ScriptableObject로 분리되어 있다.
- 허브는 3탭 UI를 사용하지만, 저장 포맷과 기존 노드 ID는 유지한다.
- 상하차 해금 노드는 센터 탭 소속이며, 현재 프로토타입 시작 상태에서는 기본 해금이다.
