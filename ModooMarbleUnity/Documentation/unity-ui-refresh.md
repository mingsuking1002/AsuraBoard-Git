# 종이 보드 UI 개선

기존에 조사한 [Machine Party](ui-ux/reference-analysis/party-ui-ux.md), [Gamble With Your Friends](ui-ux/reference-analysis/gamble-ui-ux.md), [Goose Goose Duck](ui-ux/reference-analysis/goose-ui-ux-report.md)과 [모두의마블 게임 안내](https://modoo.netmarble.net/Guide/Guide.asp)를 참고해 우리 게임의 화면 흐름과 정보 우선순위를 정리했다. 설치본의 정적 분석은 실제 화면의 정확한 색상이나 배치를 입증하지 않는다. 다른 게임의 그림이나 UI 에셋을 가져오지 않았다.

- 공통 버튼: 주 행동의 짙은 초록색, 보조 행동의 밝은 종이색, 비활성·마우스 오버·키보드 포커스 표시.
- 메인과 방: 보드 일러스트, 내 캐릭터 바로가기, 연결 상태, 방 생성·탐색과 초대 경로를 정리했다.
- 대기실: 참가자 이름·방장·준비 상태, 빈 자리, 맵·증강 횟수와 시작 버튼을 구분한다.
- 경기: 보드 중심 배치는 유지하면서 플레이어 카드, 현재 차례와 이벤트 안내, 주사위 게이지를 정리했다. 옷장·설정·건설·증강에도 공통 종이 스타일을 적용한다.
- 결과: 순위와 자산을 한 줄씩 표시한다. 현재 게임은 동점이어도 순위를 각각 부여하며 로비 복귀 흐름을 유지한다.

검증 화면과 리포트는 `Validation/unity-ui-refresh-front`, `Validation/unity-ui-refresh-loop`, `Validation/unity-ui-refresh-vision`에 저장한다. 로컬 흐름, 옷장, 건설, 증강 선택과 리롤 검사를 통과했다. 리롤 검사의 첫 실행은 결과를 기록한 뒤 종료 중 Windows UIAutomationCore에서 충돌했으며, 별도 재실행은 정상 종료했다. 원인은 확정하지 못했다. 실제 Steam 계정 두 개와 마이크 실기기 검증은 별도다.
