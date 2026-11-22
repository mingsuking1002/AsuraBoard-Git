# 어디를 고치고 어떻게 확인하나

| 작업 | 처음 읽을 곳 | 확인 방법 |
|---|---|---|
| 턴·도시·건설·파산 규칙 | Assets/Marble/Core/Game.cs와 Documentation/body-economy | check source, 경기 종료가 바뀌면 check loop |
| 메인·로비·설정 | Assets/Marble/Runtime/MarbleFrontEnd.cs와 MarbleApp.cs | check front의 1280×720 화면과 로비 복귀 확인 |
| 보드와 주사위 이동 | Assets/Marble/Runtime/MarbleBoardView.cs와 MarbleDiceRuntimeCheck.cs | check dice에서 10·11·12 걸음 및 카드 추가 이동 확인 |
| 증강과 능력 | Documentation/augments.md와 Documentation/special-mechanics.md | 선택 UI와 실제 규칙에 연결된 모듈의 범위를 확인하고 check source |
| 체력과 무인도 | Core/Game.cs의 ApplyHealthDamage와 Online/OnlineSession.cs의 호스트 효과 처리 | 부분 피해, 체력 0 이동, 탈출 회복, 리플레이와 온라인 동기화를 check source로 확인 |
| Steam | Assets/Marble/Online과 Documentation/steam-multiplayer.md | 시뮬레이션 결과와 두 계정 실측 결과를 개별 기록 |

## 체력 효과 연결

플레이어는 체력 100으로 시작한다. 일반 이동과 통행료로 체력은 감소하지 않는다. 증강이나 아이템이 유효하다고 게임 규칙에서 확인한 뒤 로컬의 Game.ApplyHealthDamage 또는 온라인 호스트의 OnlineSession.ApplyHealthDamage를 호출한다. 기존 증강 자리표시자에는 피해 효과가 들어 있지 않다. 효과는 주사위 대기, 도착 후 결정, 여행 선택 단계에서 처리한다. 선택·채무 정산 중에 효과를 붙일 때는 해당 처리가 완료된 뒤의 적용 시점을 정한다.

체력 0이 된 플레이어는 무인도로 바로 이동하고 여행 대기는 취소된다. 현재 플레이어가 대상이면 차례를 끝낸다. 탈출 카드, 비용 지불, 더블 또는 대기 후 정상 탈출로 체력 100을 회복한다. 체력 효과는 저장 리플레이에 기록되고 온라인은 상대의 체력까지 함께 검증한다.

## 소스 변경에서 Windows 확인까지

게임 프로젝트 루트에서 Python으로 아래 명령을 실행한다.

~~~powershell
python ../GameHarness/run.py status
python ../GameHarness/run.py doctor
python ../GameHarness/run.py check source
~~~

doctor가 누락 파일을 표시하면 harness.json과 Validation/VerifySource.ps1의 Unity 설치 경로를 확인한다. 소스 검사는 Windows PowerShell 5.1을 사용한다. 런타임 검사는 Builds/Windows에 있는 실행 파일을 실행한다. 변경한 코드를 검사하려면 Unity의 Build → Validate and build로 해당 소스를 먼저 빌드한다. 복제 프로젝트를 썼다면 원본과 동기화한 소스로 빌드했는지 확인한다.

게임 폴더에서 같은 명령의 마지막 인자를 front, dice, loop 중 변경과 관련된 항목으로 바꾼다. Validation/harness-runs 아래 해당 실행의 report.json은 상태와 종료 코드를, run.log는 출력 로그를 담는다. 화면과 실제 결과를 확인한 뒤 HANDOFF.md에 결과 경로와 남은 검사를 적는다.

### 배포 전에 확인할 항목

| 명령 | 확인 대상 |
|---|---|
| `check data` | Steam App ID 설정과 업적 등록 목록. 중복 ID, 끊긴 업적 참조, 코드와 문서의 보상·설명 차이를 검사한다. 아직 내용이 비어 있는 증강과 아이템은 데이터가 생기면 그 형식에 맞춰 등록한다. |
| `verify content` | 데이터 검사와 기존 보드·증강·게임 규칙 소스 검사. |
| `verify replay-online` | 모의 호스트·게스트 일치, Windows에서 버전 3 저장의 기본 외형 및 행동 복원, 버전 6 저장의 체력·위치 복원과 잘못된 행동 거부. 실제 Steam 초대와 음성은 두 계정으로 확인해야 한다. |
| `check build-id` | Unity 빌드의 Assets, Packages, ProjectSettings와 Windows 산출물의 해시. 불일치 시 Build → Validate and build를 다시 실행한다. |

front, loop, dice는 빌드 출처 검사가 통과해야 실행한다. 출처 파일은 Builds/Windows/build-identity.json이다. 이 파일과 게임 폴더를 함께 옮겨도 상대 경로와 내용이 동일하면 확인할 수 있다.

## 작업 기록을 위키에 반영하기

증강 밸런스는 `check balance`로 검증한다. 4인 자유 드래프트 200판과 대응 비교 120판을 돌리고 생성된 dashboard.html에서 데이터와 재생을 확인한다. 스크립트, 로그 형식, 재현 인자는 Tools/Balance/README.md에 정리했다.

수정 전에 공통 하네스의 `impact 기능명`으로 영향을 받는 기능을 확인한다. 게임 설정의 features에 실제 호출부와 공유 상태를 대조하고, 빠진 의존성이 발견되면 추가한다. 버그는 같은 초기 상태·시드·입력으로 재현하고, 기대값을 자동 테스트로 남긴다. 수정 뒤 `verify 기능명`으로 연결된 기존 검사까지 실행한다. 예를 들어 `verify health`에는 소스, 경기 흐름, 화면 검사가 포함된다. 재현이 확인된 경우 `record --repro`에 조건과 기대값을 함께 기록한다.

주사위 고눈 이동 검사는 MarbleDiceRuntimeCheck.cs에 있다. 10·11·12가 나오는 시드로 굴리고 실제 주사위 걸음 수와 목적지를 비교한다. 돌발 카드가 추가 이동을 일으키는 경우에는 카드 이동의 이유와 판정도 확인한다. 이동을 수정하면 리플레이·온라인·경기 흐름·화면에서 어디까지 영향이 가는지 `impact movement`로 볼 수 있다.

게임 폴더의 Documentation/wiki가 장기 문서 원본이다. 사용법·구조가 바뀌면 해당 페이지를 수정한다. 검증을 마친 뒤 하네스로 변경 이유, 증거와 남은 일을 이력에 추가한다. 최근 작업 담당과 다음 행동은 HANDOFF.md에 적는다. 두 도구 모두 게임 루트에서 다음 명령을 사용한다.

~~~powershell
python ../GameHarness/run.py record --title "작업 제목" --summary "변경과 이유" --evidence "검사 결과와 로그 경로" --next "남은 작업"
python ../GameHarness/run.py check wiki
~~~

위키에 복사한 페이지를 직접 편집하면 다음 동기화 때 덮어쓴다. 다른 게임의 작업을 이어받을 때는 그 게임의 지침과 인수인계를 먼저 읽는다.
