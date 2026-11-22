# 아수라판 작업 규칙

코딩 전 `python ../GameHarness/run.py impact 기능명`으로 알려진 영향 범위를 확인하고 실제 호출부·공유 상태·저장과 온라인 소비자를 읽는다. 이슈는 수정 전에 초기 상태, 입력이나 시드, 관찰 결과와 기대 결과를 고정한다. 가능하면 실패를 먼저 확인하고 같은 조건을 회귀 테스트로 남긴다. 새 의존성이 드러나면 harness.json의 features와 개발 위키를 갱신한다. 수정 후 `verify 기능명`으로 관련 신규·기존 검사를 실행한다. Windows 검사는 변경 소스를 빌드한 후 실행하고 기존 빌드 결과는 별도로 표시한다.

기능·실행법·검증·인계 상태가 바뀌면 Documentation/wiki와 HANDOFF.md를 함께 확인한다. 기능 단위의 변경은 GameHarness README의 `record` 명령으로 이유·검증 증거·남은 일을 history.md에 기록한다. 교대 전에 게임 루트에서 `python ../GameHarness/run.py check wiki`로 공용 오프라인 위키에 반영한다.

이 폴더를 두 클라이언트의 프로젝트 루트로 연다. 작업 시작 시 HANDOFF.md를 읽고 현재 사용자 요청과 대조한다. 코드 수정 전 목표와 플레이어가 확인할 수 있는 완료 조건을 적는다. 단순 질문은 파일 기록 없이 답한다.

- 한 번에 한 도구가 수정한다. 다른 도구가 작성 중이라면 읽기와 리뷰를 진행한다. 교대 시 기존 작업이 멈췄는지 확인한다. 담당자 표시는 협업 약속이다.
- 수정은 Assets, Packages, ProjectSettings 등 원본에서 진행한다. Validation 아래 복제 프로젝트에서 빌드할 때는 원본과 동기화됐는지 확인한다.
- 루트에서 `python ../GameHarness/run.py doctor`로 환경을 확인한다. 규칙이나 C# 변경은 `python ../GameHarness/run.py check source`로 검증한다.
- UI 변경은 변경 소스에서 빌드한 플레이어로 `check front`, 이동은 `check dice`, 경기 흐름은 `check loop`를 실행한다. 스크린샷을 직접 읽고 화면 품질을 판단한다. 실행 검사는 Builds/Windows의 플레이어를 대상으로 한다.
- Unity의 Build → Validate and build가 기록한 소스·빌드 해시를 실행 검사 전에 대조한다. 데이터 변경은 `verify content`, 저장·동기화 변경은 `verify replay-online`을 실행한다. Steam 실기기 결과는 모의 검사와 구분해 기록한다.
- 배포 후보는 소스와 관련 실행 검사를 마치고 빌드 버전, 증거 폴더, 실제 두 Steam 계정 및 마이크 확인 여부를 기록한다.
- 기능 단위 또는 교대 직전에 HANDOFF.md의 담당·상태, 변경 파일, 통과·실패·미실행과 이유, 다음 행동을 갱신한다. 세션 시작 훅을 사용할 수 없으면 같은 문서를 직접 읽는다.
