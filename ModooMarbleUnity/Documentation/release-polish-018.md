# 0.18.0 리소스 폴리싱

## 반영

- `Simple Town Lite - Cartoon Assets`의 아파트 FBX와 텍스처를 사용해 빌딩·호텔·랜드마크를 입체로 표시한다. 별장과 랜드마크 지붕·깃발은 Unity에서 직접 조합했다. 일반 세 건물은 각각 한 번씩 표시하며 랜드마크로 바뀌면 세 건물은 감추고 별도 미니어처 한 개만 보인다.
- 각 도시의 안쪽 색상 띠에 놓고, 네 면의 건물 정면을 보드 안쪽으로 돌린다. 소유자 색상은 받침에 표시하며 바닥 띠는 라인 색상을 유지한다. 화면상 사유지 이름과 통행료는 타일 원근 방향에 따라 표시한다.
- 출발·무인도·올림픽·세계여행 아이콘의 형태와 색상을 다듬고, 포춘카드 칸에 작은 카드 표식을 넣었다.
- 하단 거래 버튼이 잘리지 않게 짧은 버튼의 글자 크기를 조절하고 주사위 조작 안내가 타일 이름을 덮지 않도록 보드 중앙으로 옮겼다.
- 로컬 대기실의 AI 외형을 실제 경기에 그대로 적용한다. 플레이어 색을 어떤 색으로 고르더라도 AI 세 명이 남은 색을 하나씩 사용해 색이 겹치지 않는다.
- 경기 결과의 탈락 표기를 신체 경제 모드의 플레이어 카드와 일치시켰다.
- 합성 버튼·발걸음 소리를 목재와 종이 느낌으로 교체하고 주사위가 닿는 세 시점에 음량이 줄어드는 충돌음을 연결했다. 기존 효과음·전체 음량과 음소거 설정을 사용한다.
- 모델·텍스처 원본 경로와 SHA-256은 `asset-audit/simpletown.json`에 기록한다. 패키지에서 사용하는 파일은 모델과 텍스처다.

## 실행 검증

- `Validation/unity-polish-release-build.json`: Unity 6000.3.9f1 빌드, 290개 검사, AI 64경기·온라인 시뮬레이션 12회.
- `Validation/polish-release-vision/runtime-validation.json`: 실제 플레이어에서 32칸·28개 색상 띠, 다운로드 건물 719정점, 12개 건물과 네 면 안쪽 방향, 시야 효과·이동 추적 검사.
- `Validation/polish-release-wardrobe/runtime-validation.json`: 39개 옷장 선택지, 외형 패킷·저장 JSON, 랜드마크 건설·인수 차단 및 미니어처 전환 검사.
- `Validation/polish-release-front/runtime-validation.json`: 메인·방·로비·설정·로비 복귀, AI 외형 일치·색 중복 방지 및 1280×720 표시.
- `Validation/polish-release-loop.json`: 로컬 경기 종료·등수 표시·같은 방 복귀·재시작.
- `Validation/steam-release-guard-test.json`: 개발 ID 480을 출시 설정으로 지정한 경우 빌드 전에 차단.
- `Validation/polish-release-dice/runtime-validation.json`: 10·11·12 포함 실제 이동 9가지, 주사위 면·세 번 충돌 타이밍·연출 완화.
- `Validation/unity-polish-publish.json`: 검증 빌드와 배포 폴더의 파일별 해시.
- `Validation/polish-published-front/runtime-validation.json`: 배포 경로 실행 파일의 로비·설정·AI 외형 검증.

검사에 사용한 온라인 상대는 테스트 백엔드이며 실제 두 Steam 계정·음성 장치 실측은 포함하지 않는다. 초반 구버전에서 창 크기 변경 후 Windows 네이티브 종료 오류가 한 차례 관찰됐다. 새 빌드에서 동일 화면 전환을 세 차례 반복하고 배포 경로에서도 재실행했을 때 정상 종료했지만, 원인은 확인하지 못했다. Steam 출시는 [별도 준비 상태](steam-release-readiness.md)를 참고한다.
