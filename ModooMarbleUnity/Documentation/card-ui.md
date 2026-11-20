# 색상 카드 UI와 증강 정보 — 0.8.0

2026-10-26. 손그림 검은 테두리·겹친 종이 구조를 유지하고 카드 전체 바탕의 색 구분을 강화했다. 모든 시각 요소는 코드로 생성하며 외부 게임 그래픽을 사용하지 않는다.

- 증강: 브론즈 구릿빛 / 실버 은빛 / 골드 금빛 / 프리즘 청록·보라·분홍의 무지개 바탕. 제목 띠는 더 진하게, 선택 카드는 더 진한 바탕과 체크 도장으로 표시한다.
- 건설: 땅 갈색 / 별장 초록 / 빌딩 파랑 / 호텔 보라. 선택·보유·잠금 텍스트를 유지한다. 잠긴 건물은 회색으로 처리한다.
- 플레이어: 각 플레이어의 파랑·빨강·초록·노랑을 바탕과 이름 띠에 적용한다. 현재 차례는 더 진한 바탕과 두꺼운 잉크 테두리로 표시한다.
- 도시: 도시 그룹 및 소유자 색상을 타일 바탕에도 반영한다.
- 보유 증강: 글자 없이 등급 색의 보석 아이콘만 표시하고, 호버하면 주변에 정보 카드가 뜬다. 현재 증강 내용은 미설정이므로 실제 능력 아이콘은 공통 자리표시자다.
- 리롤: 선택 단계마다 플레이어별 1회, 버튼 또는 R. 확정 이후 불가. 리롤 후 이전 카드 후보를 확정할 수 없고 사용 횟수는 저장·온라인 복구에서도 유지된다. 현재 빈 콘텐츠는 리롤해도 비어 있다.

## 검증

- 최종 `Validation/unity-validation.json`: passed. 규칙 22 / 건설 11 / 증강 15 / 온라인 25, AI 경기 64회와 모의 멀티 경기 12회. 빌드 경고 0 / 오류 0.
- `Validation/reroll-icons.json`: 리롤 사용·두 번째 리롤 거부·후보 초기화·이전 후보 거부·리롤 후 획득·8개 보유·호버 시 상태 불변 passed.
- `Validation/reroll-color-tiers.json`: 최종 4등급 선택 화면 및 선택 기록 passed. 실제 스크린샷 직접 확인.
- 앞선 카드색 전환 검증 `color-construction.json`, `color-owned.json`, `color-paper.json`: 구매/건설·8개 한도·기존 32칸 배치·메뉴/도움말/결과 화면 passed.
- `reroll-icons-icons.png`, `reroll-icons-hover.png`, `reroll-icons-rerolled.png`에서 아이콘만 표시·정보창·리롤 사용 상태를 직접 확인.
- 실제 두 Steam 계정 간 동시 리롤은 아직 미검증. 온라인 검증은 인메모리 전송이다.

코드는 `Assets/Marble/Runtime/MarbleAugmentPanel.cs`, `MarbleConstructionPanel.cs`, `MarbleApp.cs`, `MarbleBoardView.cs`와 `PaperTheme.cs`에 있다. 리롤 규칙은 `Assets/Marble/Core/GameAugments.cs`, 호스트 판정은 `Assets/Marble/Online/OnlineSession.cs`에 있다. 기존 저장 형식 2를 유지하며 온라인 호환 키는 `marble-classic-online-6`이다.
