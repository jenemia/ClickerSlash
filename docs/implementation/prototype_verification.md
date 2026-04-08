# Prototype Verification

## 검증 로그
- `unity-cli editor refresh --compile` 성공
- `Tools/ClikerSlash/Build Prototype Battle Scene` 메뉴 실행 성공
- 게임 뷰 스크린샷 생성:
  - `/Users/sean/Documents/ClikerSlash/ClikerSlash/Temp/prototype-battle.png`
  - `/Users/sean/Documents/ClikerSlash/ClikerSlash/Temp/prototype-battle-live.png`

## 런타임 확인값
- 플레이 진입 후 ECS 월드 확인:
  - `Players=1`
  - `StageProgressState`, `SpawnTimerState`, `BattleOutcomeState` 생성 확인
- 프레임 진행 확인:
  - `Frame=608`
  - `PlayTime=1.82`
  - `Stage=58.16`
  - `Enemies=2`
- 이동 큐 확인:
  - 레인 1에서 `Direction=1` 입력을 큐에 넣은 뒤
  - 2초 후 `Lane=2`, `Moving=0` 확인
- 생존 규칙 확인:
  - 6초 경과 후 `Life=0`, `Finished=1`, `Outcome=1/0`
- 자동공격 확인:
  - 플레이어 레인에 적 1개를 강제 생성
  - 2초 뒤 해당 적 엔티티 `Exists=False` 확인

## 메모
- `unity-cli` 백그라운드 실행에서 프레임이 진행되도록 `Application.runInBackground = true`를 부트스트랩에서 강제했다.
- 키보드 직접 입력은 CLI로 자동화하지 않았고, 이동 시스템은 ECS 버퍼 주입으로 검증했다.

## 체크 항목
- [x] Unity 컴파일 성공
- [x] PrototypeBattle 씬 생성 성공
- [x] 플레이어 4레인 이동 시스템 확인
- [x] 자동공격 동작 확인
- [x] 랜덤 적 스폰 확인
- [x] 하단 돌파 시 라이프 감소 확인
- [ ] 60초 종료 확인
