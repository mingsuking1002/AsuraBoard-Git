# Unity 0.14.0 로비·방·환경설정

2026-10-27. Unity의 실제 IMGUI 화면을 새 낙서 종이 구성으로 변경했다.

- 메인: 방 만들기·찾기를 위에 배치하고 꾸미기·설정·종료로 연결.
- 방 만들기: 방 이름/공개 범위, Steam·로컬/정원을 두 열로 구성. 생성은 대기실로 연결.
- 방 찾기: 목록 행, 참가 인원·정원 초과, 연결·검색 상태, 방 ID 입장. 실제로 확보하지 않은 맵/버전 데이터는 표시하지 않는다.
- 로비: 참가자 2×2 카드, 오른쪽 맵·증강 규칙, 아래 준비·시작. Steam 참가자는 Steam 사진을 사용하고 사진이 없으면 대체 이미지를 표시한다. 로컬 방은 AI 초상화를 표시한다.
- 환경설정: 소리/마이크·음성/화면·성능/게임·편의 4탭, 우측 스위치, 음량 슬라이더, 저장/복원. 로비에서 닫으면 같은 로비를 유지하고 경기에서는 경기 메뉴로 복귀.
- 참가자별 음량 창과 방 나가기 확인창. ESC는 참가자 창 → 설정 → 원래 화면 순서로 닫는다.

개인 설정은 변경 즉시 적용하고 저장 시 다음 실행에도 보관한다. 마이크 장치 선택은 현재 백엔드가 지원하지 않아 추가하지 않는다. 실제 녹음·전송은 기존 음성 구현과 방 상태를 사용한다.

## 실제 실행 검증

Unity 6000.3.9f1 Windows 빌드 성공, C# 검사 290개 통과, 빌드 경고·오류 0개. 실제 표시 창에서 1440×900 및 1280×720 화면 12개를 캡처했다. 로컬 방 생성 후 로비 유지, 맵/증강 설정 적용, 설정에서 로비·경기로 복귀, 같은 방 로비 복귀, 이탈, 마이크 모드 3종 표시를 확인했다. 실제 Steam 두 계정 연결과 마이크 장치 송수신은 이번 검사에서 수행하지 않았다.

[빌드 결과](../Validation/unity-validation.json) · [실행 검사 결과](../Validation/unity-front-verified/runtime-validation.json)

[대기실](../Validation/unity-front-verified/04-lobby.png) · [방 만들기](../Validation/unity-front-verified/02-create-room.png) · [방 찾기](../Validation/unity-front-verified/03-find-room.png) · [마이크 설정](../Validation/unity-front-verified/05-settings-1.png) · [경기 중 설정](../Validation/unity-front-verified/08-match-settings.png)

실행파일: `Builds/Windows/ModooMarbleClassic.exe`. 재현은 `-marble-front-check`와 출력 폴더를 전달한다. 로컬 테스트만 진행하며 계정에 접속하거나 설정을 저장하지 않고 자동 종료한다.
