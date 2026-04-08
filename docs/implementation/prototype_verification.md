# Prototype Verification

## 검증 로그
- `unity-cli editor refresh --compile` 성공
- `Tools/ClikerSlash/Build Prototype Battle Scene` 메뉴 실행 성공
- 게임 뷰 스크린샷 생성:
  - `/Users/sean/Documents/ClikerSlash-landscape-pc-web/ClikerSlash/Temp/landscape-prototype-fresh.png`
  - `/Users/sean/Documents/ClikerSlash-landscape-pc-web/ClikerSlash/Temp/landscape-prototype-live.png`
- 별도 워크트리에서 검증:
  - Git branch: `codex/landscape-pc-web`
  - Git worktree: `/Users/sean/Documents/ClikerSlash-landscape-pc-web`

## 런타임 확인값
- 가로형 씬 프레이밍 확인:
  - `1920x1080` 게임 뷰에서 4개 레인, 상단 스폰선, 하단 방어선이 모두 보이는지 확인
  - 카메라 값:
    - `Position=(0.00, 10.60, -16.80)`
    - `Rotation=(31.00, 0.00, 0.00)`
    - `FOV=34`
- 플레이 진입 후 ECS 월드 확인:
  - `Player=1`
  - `StageProgressState`, `SpawnTimerState`, `BattleOutcomeState` 생성 확인
  - 초기 상태 예시:
    - `lane=1`
    - `lives=3`
    - `finished=0`
- 이동 큐 확인:
  - 이동 큐에 `Direction=1`, `Direction=1` 주입
  - 중간 상태:
    - `mid lane=2 moving=1 progress=0.66 queued=0`
  - 최종 상태:
    - `final lane=3 moving=0 queued=0`
- 이동 중 공격 불가 확인:
  - 테스트 중 `PlayerMoveDuration=1.0`으로 늘려 이동 상태를 유지
  - 스냅샷:
    - `lane=1 moving=1 progress=0.51 targetNull=True enemies=1`
  - 결과:
    - 이동 중 타깃이 비워지고 적이 즉시 제거되지 않음을 확인
- 자동공격 확인:
  - 플레이어를 `lane=2`에 고정하고 같은 레인 적 1개 생성
  - 0.12초 시점:
    - `targetReady=False cooldown=0.25`
  - 0.55초 시점:
    - `after-auto-attack enemies=0 cooldown=0.00 finished=0`
  - 결과:
    - 자동공격이 발생해 같은 레인 적이 제거됨
- 하단 돌파 라이프 감소 확인:
  - 플레이어는 `lane=1`, 적은 `lane=0`, `defenseLineZ - 0.2`에 생성
  - 0.12초 뒤:
    - `after-life-loss-2 lives=2 enemies=0`
  - 결과:
    - 적 제거와 함께 라이프가 1 감소함
- 승리 판정 확인:
  - `RemainingTime=0.05`, `Life=2`, 적 없음 상태로 강제 세팅
  - 0.20초 뒤:
    - `after-victory time=0.00 finished=1 outcome=1/1`
  - 결과:
    - 60초 종료 승리 상태로 정상 전이됨
- HUD 런타임 값 확인:
  - `InfoText='Time 15.6 / Lives 1'`
  - `LaneText='Lane 3 / 4'`
  - `ControlsText='Controls: A / D or Left / Right'`
  - `Canvas renderMode=ScreenSpaceOverlay`

## 메모
- `unity-cli` 백그라운드 실행에서 프레임이 진행되도록 `Application.runInBackground = true`를 부트스트랩에서 강제했다.
- 키보드 직접 입력은 CLI로 자동화하지 않았고, 이동 시스템은 ECS 버퍼 주입으로 검증했다.
- `unity-cli screenshot --view game` 캡처에서는 `Screen Space Overlay` HUD가 보이지 않았지만, 런타임에서 `Text` 값과 `Canvas` 상태는 정상 갱신되는 것을 확인했다.
- `21:9`는 별도 게임뷰 캡처까지는 아직 하지 않았고, 현재 구성상 `16:9`보다 가로 폭이 넓어지는 방향이라 전장 가시성은 유지될 것으로 본다.

## 체크 항목
- [x] Unity 컴파일 성공
- [x] PrototypeBattle 씬 생성 성공
- [x] 플레이어 4레인 이동 시스템 확인
- [x] 자동공격 동작 확인
- [x] 랜덤 적 스폰 확인
- [x] 하단 돌파 시 라이프 감소 확인
- [x] 60초 종료 확인
