# 어디를 고치고 어떻게 확인하나

| 작업 | 처음 읽을 곳 | 확인 방법 |
|---|---|---|
| 턴·도시·건설·파산 규칙 | Assets/Marble/Core/Game.cs와 Documentation/body-economy | check source, 경기 종료가 바뀌면 check loop |
| 메인·로비·설정 | Assets/Marble/Runtime/MarbleFrontEnd.cs와 MarbleApp.cs | check front의 1280×720 화면과 로비 복귀 확인 |
| 보드와 주사위 이동 | Assets/Marble/Runtime/MarbleBoardView.cs와 MarbleDiceRuntimeCheck.cs | check dice에서 10·11·12 걸음 및 카드 추가 이동 확인 |
| 증강과 능력 | Documentation/augments.md와 Documentation/special-mechanics.md | 선택 UI와 실제 규칙에 연결된 모듈의 범위를 확인하고 check source |
| Steam | Assets/Marble/Online과 Documentation/steam-multiplayer.md | 시뮬레이션 결과와 두 계정 실측 결과를 개별 기록 |

## 소스 변경에서 Windows 확인까지

게임 프로젝트 루트에서 Python으로 아래 명령을 실행한다.

~~~powershell
python ../GameHarness/run.py status
python ../GameHarness/run.py doctor
python ../GameHarness/run.py check source
~~~

doctor가 누락 파일을 표시하면 harness.json과 Validation/VerifySource.ps1의 Unity 설치 경로를 확인한다. 소스 검사는 Windows PowerShell 5.1을 사용한다. 런타임 검사는 Builds/Windows에 있는 실행 파일을 실행한다. 변경한 코드를 검사하려면 Unity의 Build → Validate and build로 해당 소스를 먼저 빌드한다. 복제 프로젝트를 썼다면 원본과 동기화한 소스로 빌드했는지 확인한다.

게임 폴더에서 같은 명령의 마지막 인자를 front, dice, loop 중 변경과 관련된 항목으로 바꾼다. Validation/harness-runs 아래 해당 실행의 report.json은 상태와 종료 코드를, run.log는 출력 로그를 담는다. 화면과 실제 결과를 확인한 뒤 HANDOFF.md에 결과 경로와 남은 검사를 적는다.

## 작업 기록을 위키에 반영하기

게임 폴더의 Documentation/wiki가 장기 문서 원본이다. 사용법·구조가 바뀌면 해당 페이지를 수정하고 history.md 맨 위에 날짜, 변경 이유, 검증 증거, 미확인 항목을 추가한다. 최근 작업 담당과 다음 행동은 HANDOFF.md에 적는다. 두 도구 모두 게임 루트에서 다음 명령으로 오프라인 위키를 갱신하고 검증한다.

~~~powershell
python ../GameHarness/run.py check wiki
~~~

위키에 복사한 페이지를 직접 편집하면 다음 동기화 때 덮어쓴다. 다른 게임의 작업을 이어받을 때는 그 게임의 지침과 인수인계를 먼저 읽는다.
