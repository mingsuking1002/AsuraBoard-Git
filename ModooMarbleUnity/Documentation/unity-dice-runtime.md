# Unity 0.13.0 실행 검증

2026-10-27. 웹 시안 변경 없이 Unity 코드와 Windows 빌드에서 작업했다.

- 실행 파일: `Builds/Windows/ModooMarbleClassic.exe`
- Unity 6000.3.9f1 빌드 성공, C# 검사 290개, 경고 0, 오류 0.
- 실제 렌더 창에서 주사위 공중 궤적·착지·결과·말 이동을 캡처했다.
- 검사 결과 주사위 3+5, 결과 눈 일치, 결과 확정 전 말 이동 없음, 왼쪽으로 8칸 이동 후 무인도 도착.
- 연출 완화에서 던지기 생략, 실제 렌더 및 게임 실행 오류 없음.

[빌드 검사 결과](../Validation/unity-validation.json) · [실행 검사 결과](../Validation/unity-dice-visible-verified/runtime-validation.json)

[던지는 순간](../Validation/unity-dice-visible-verified/02-flight.png) · [결과 확정](../Validation/unity-dice-visible-verified/04-result.png) · [도착](../Validation/unity-dice-visible-verified/05-arrived.png)

재현은 실행 파일에 `-marble-dice-check`와 출력 폴더를 전달한다. 별도 로컬 진단 경기로 실행하고 설정을 저장하지 않으며, 검사가 끝나면 종료한다. 숨긴 창은 Unity IMGUI를 다시 그리지 않아 전체 화면 검사에 사용하지 않는다. 실제 표시 창에서 저장한 결과만 화면 검증으로 취급한다. 초기 숨김 실행에서의 검은 캡처는 최종 화면 검증을 대체하지 않는다.

Steam 계정·음성 장치는 이 검사에서 연결하지 않았다. 친구에게 게임을 전달할 때는 실행 파일 하나가 아닌 `Builds/Windows` 폴더 전체를 전달한다.
