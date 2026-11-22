# 아수라판

아수라판은 도시 투자, 통행료, 신체 경제와 증강을 다루는 2~4인 Unity 보드게임이다. 같은 C# 규칙 엔진으로 게임을 플레이하고 화면 없는 자동 경기를 실행한다.

| 구성 | 내용 |
|---|---|
| [게임 엔진](ModooMarbleUnity/Assets/Marble/Core) | 결정적 턴 처리, 이동, 경제, 체력·무인도, 리플레이 |
| [밸런스 시뮬레이터](ModooMarbleUnity/Tools/Balance/README.md) | 4인 AI 배치, 효과별 로그, 같은 시드의 대응 비교 |
| [개발 하네스](GameHarness/README.md) | 기능별 영향 범위, 데이터·저장 호환·빌드 검증, 위키 기록 |
| [유지보수 지도](ModooMarbleUnity/Documentation/wiki/maintenance.md) | 기능별 진입 파일, 검사 명령, 인수인계 |

## 밸런스 실험 실행

Python 3.10 이상과 Unity 6000.3.9f1의 Editor 설치가 필요하다.

~~~powershell
cd ModooMarbleUnity
python Tools/Balance/run.py --games 200 --seed 4200
~~~

Validation/Balance/dashboard.html을 브라우저에서 열면 자유 선택 200판과 대응 비교 120판을 탐색할 수 있다. 경기별 로그는 matches.jsonl에 저장된다. 대시보드에서 확인한 시드·맵·노이즈·좌석 회전·지급 값으로 같은 판을 다시 실행할 수 있다.

[16판 대시보드 샘플](docs/balance-demo.html)을 다운로드하면 설치 없이 브라우저에서 탐색할 수 있다. 샘플은 UI 확인용이며 밸런스 판단에는 더 큰 표본이 필요하다.

## 게임 실행과 검증

Unity Hub에서 ModooMarbleUnity를 열고 Assets/Scenes/Classic.unity를 실행한다. 다운로드한 모델과 텍스처는 사용 권한이 있는 원본에서 Assets/Marble/Resources/Downloaded 아래에 복원한다. Unity의 Build → Validate and build에서 Windows 실행 파일을 만든다.

공통 하네스의 원본은 [GameHarness 저장소](https://github.com/mingsuking1002/GameHarness)에 있다. 소스 검사에는 Unity 설치 경로와 로컬 컴파일 캐시 경로가 필요하다. 공용 HTML 위키 재생성에는 별도 Game-Analysis-Wiki-OpenSource 프로젝트가 필요하다.
