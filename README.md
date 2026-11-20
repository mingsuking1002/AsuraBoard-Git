# 아수라판

Unity 게임 소스는 [ModooMarbleUnity](ModooMarbleUnity/README.md), 공통 개발 하네스는 [GameHarness](GameHarness/README.md)에 있다. 두 폴더를 나란히 유지하고 Unity Hub에서는 ModooMarbleUnity를 열어 Unity 6000.3.9f1로 실행한다. 시작 씬은 Assets/Scenes/Classic.unity다. 유지보수는 게임 폴더의 Documentation/wiki와 HANDOFF.md에서 시작한다.

공통 하네스 자체는 [독립 GameHarness 저장소](https://github.com/mingsuking1002/GameHarness)에서 관리한다. 이 저장소의 GameHarness 폴더는 게임에서 바로 실행할 수 있도록 같이 둔 호환용 사본이다. 게임 폴더에서 `record`로 변경 이력을 남기고, `check wiki`로 공용 오프라인 위키를 갱신한다.

다운로드한 모델·텍스처는 각자 사용 권한이 있는 원본에서 게임 폴더의 Assets/Marble/Resources/Downloaded 아래에 복원한다. 검증된 Windows 빌드는 Unity에서 별도로 생성한다. 기존 소스 검사에는 Unity 설치 경로와 로컬 Unity 컴파일 캐시 경로가 들어 있어 새 PC에서 조정이 필요하다. HTML 위키 재생성에는 별도로 관리하는 Game-Analysis-Wiki-OpenSource 프로젝트도 필요하다.
