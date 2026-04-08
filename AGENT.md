이 디렉터리 자체가 Unity 프로젝트이며, 참고 자료는 `References` 아래에 있다.

## 참고 자료

## Skill / Workflow 사용

요청이 왔을 때 먼저 `.agent/skills`, `.agent/workflows`에 프로젝트 전용 항목이 있는지 확인하고, 없으면 `~/.codex/skills`의 글로벌 스킬을 참고한다.
프로젝트 전용 스킬과 워크플로만 이 저장소 안에 두고, 글로벌 스킬은 `.agent/skills`로 심볼릭 링크 복제하지 않는다.

Unity 에디터 제어(프리팹 생성, 메테리얼 변경 등), 콘솔 확인, 컴파일 검증, 간단한 런타임 실행은 모두 `unity-cli` 기준으로 수행한다.
글로벌 스킬 중 MCP 연결을 전제로 하는 항목이 있더라도, 이 저장소에서는 선택하지 않고 `unity-cli` 기반 경로로 대체한다.

Unity 컴파일 검증이 필요하면 `.agent/workflows/compile_and_verify.md`를 먼저 참고한다.
다만 해당 워크플로를 현재 환경에서 직접 실행할 수 없거나 결과 신뢰성이 부족하면, Unity CLI `-batchmode -quit -projectPath ... -logFile ...` 방식으로 대체 검증한다.

## 작업 방식

큰 리팩터링, 다중 티켓 작업, 실험적 변경은 별도 worktree에서 진행하는 것을 기본값으로 한다.
사용자가 티켓 단위 진행을 요청하면 각 티켓마다 구현, 검증, 커밋까지 끊어서 마무리한다.

커밋 메시지는 기본적으로 한국어로 작성한다.
사용자가 별도 형식을 요구하지 않았다면 `feat(...)`, `fix(...)`, `refactor(...)`, `docs(...)` 같은 prefix는 유지하되, 설명 본문은 한국어로 적는다.

## 검증

전투, ECS, 충돌, 물리, 런타임 시스템을 수정했다면 가능한 한 Unity 컴파일 검증까지 수행한다.
Unity CLI 검증 시에는 컴파일 성공 여부를 로그에서 `Tundra build success`, `error CS`, `Scripts have compiler errors` 기준으로 확인한다.
기존 Unity batchmode 프로세스가 프로젝트를 점유하고 있으면, 새 검증 전에 해당 프로세스 정리 여부를 먼저 확인한다.

## 커밋 제외 항목

명시적 요청이 없으면 아래와 같은 로컬/생성 파일은 커밋하지 않는다.

- `.vscode/settings.json`
- `*.slnx`
- `.DS_Store`
- Rider, Xcode, Unity가 로컬 환경에 맞춰 생성한 설정 파일

## 문서 정합성

README나 설계 문서는 실제 구현 상태와 다르면 함께 갱신한다.
특히 충돌/물리 구조처럼 아키텍처 판단에 직접 영향을 주는 항목은 코드 변경 후 문서도 같은 턴 안에서 맞춘다.
