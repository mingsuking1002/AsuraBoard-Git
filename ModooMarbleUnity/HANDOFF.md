# 개발 상태

## 소스 검사와 런타임 진단

- 새 복제본에서는 Unity Hub 기본 설치 또는 ASURA_UNITY_EDITOR로 Unity를 찾는다. Steamworks.NET을 패키지 소스에서 컴파일하므로 기존 Library 캐시 없이 doctor, source, data를 확인했다.
- 창 포커스를 잃었을 때 마이크 정지에 실패하면 Unity 로그에 기록한다. 시야 검사 실패는 failedChecks에 그룹별로 기록한다.
- 공개 복제본의 front·vision 실행은 Windows 빌드 산출물이 있어야 한다. 다음 플레이어 빌드에서 두 검사와 실제 Steam 마이크 동작을 확인한다.

- Windows 0.19.1은 체력, 무인도 이송과 탈출 회복을 지원한다.
- `check balance`는 자유 선택 200판과 대응 비교 120판을 생성한다. 시작 시드와 AI 배치를 고정해 경기 로그를 재현할 수 있다.
- `verify build`는 소스, 저장 호환, 화면, 경기 흐름과 주사위를 확인한다. Windows 실행 검사에는 Unity 빌드 출처 파일이 필요하다.
- 실제 Steam 계정 간 초대·음성은 모의 멀티플레이 검사와 별도로 확인한다.
- 실행·수정 안내는 [유지보수 위키](Documentation/wiki/maintenance.md), 시뮬레이터는 [밸런스 실험](Tools/Balance/README.md)을 참조한다.
