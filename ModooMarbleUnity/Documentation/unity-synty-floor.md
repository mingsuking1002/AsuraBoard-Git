# Synty 1×1 바닥판 적용 — 0.15.0

현재 0.16.0은 모서리 옆 칸을 자르지 않고 일반 28칸을 동일하게 배치한다. 넓어진 보드와 라인별 색상 패널은 [현재 구현과 실행 검증](unity-equal-line-tiles.md)을 참고한다. 아래는 이전 단계별 기록이다.

0.15.3 변경: 출발지·무인도·올림픽·여행 칸을 기존 대비 가로·세로 각각 1.5배(136×86 → 204×129, 중심 유지)로 확대했다. 커진 모서리 면을 우선 클릭 판정하고 인접 칸은 그 경계에 맞춰 잘라낸다. 이름·통행료는 잘린 타일의 실제 상판 중심으로 정렬한다. 건물도 해당 중심 기준 안쪽으로 배치한다. `Validation/unity-large-corners-build-validation.json`: 검사 290개 및 빌드 통과, 경고·오류 0개. `Validation/unity-large-corners-verified/runtime-validation.json`: 실제 실행 passed, 캡처 `01-normal.png` 확인. `Validation/unity-large-corners-publish.json`: 기존 실행 경로의 146개 파일 해시 일치. 비활성 경제의 시야 검증은 단위 표기(G/만)가 달라지는 글자 대신 근거리·좌우 월드 표본을 비교한다.

0.15.2 변경: 사용자의 ‘모마처럼 타일’ 요청에 맞춰 Synty 바닥의 솟은 턱을 상판 아래로 내리고 별도의 평평한 입체 상판을 추가했다. 선명한 평면, 약 1px 베벨, 총 8px 깊이의 얕은 옆면으로 구성한다. 인접한 큰 모서리 칸의 경계로 일반 칸 상판을 잘라 겹침을 제거하고 약 1~2px 이음새를 둔다. 색 띠는 제거하고 타일 면 전체에 도시 그룹·소유자 색을 적용했다. 개발용 칸 번호도 숨겼다. 클릭 영역과 이동 경로는 그대로다.

0.15.2 검증: `Validation/unity-flat-tiles-verified/runtime-validation.json` passed. 원본 바닥 메시 로드·32칸·시야·복구·이동 확대 통과. 실제 전체 보드 캡처 `01-normal.png` 확인. `Validation/unity-flat-tiles-build-validation.json`: 기존 검사 290개, 빌드 Succeeded, 경고·오류 0개. `Validation/unity-flat-tiles-publish.json`: 기존 실행 경로의 146개 파일 해시 일치.

0.15.1 변경: 보드의 검은 외곽선·안쪽 선·상시 칸 테두리·소유자 테두리를 제거했다. 입체 바닥판, 칸 색 띠, 클릭 선택·이동 목적지 강조는 유지한다. 실제 화면 `Validation/unity-board-no-lines-verified/01-normal.png`를 확인했고 같은 폴더의 실행 검증은 passed다. 규칙 검사 290개 및 빌드 통과, 경고·오류 0개. 기존 실행 경로 반영 후 146개 파일 해시 일치를 확인했다(`Validation/unity-board-no-lines-publish.json`).

사용자가 다운로드한 `POLYGON - Starter Pack - Art by Synty`의 `SM_PolygonPrototype_Buildings_Floor_1x1_01P` 모델을 32칸에 적용했다. 썸네일을 바닥으로 그리는 방식이 아니라 원본 FBX 메시를 Unity에서 임포트해 실제 입체 모델로 렌더한다.

## 출처와 반입 범위

- 원본: 사용자 Unity Asset Store 캐시의 POLYGON Starter Pack.
- 프로젝트 파일: `Assets/Marble/Resources/Downloaded/Synty/Floor1x1.fbx`.
- 원본 GUID: `efecce0669e04d14aada7871935a434d`.
- FBX SHA-256: `acd9a12c9cf8ab363a65e9a3b3a76ee8effbd6d07121e1cdcc0d0a8f6eda1463`.
- 원본 FBX 바이트는 변경하지 않았다. 보드 모양에 맞출 수 있도록 Read/Write를 켰다.
- 패키지의 셰이더·팔레트·도구 스크립트는 반입하지 않았다. 자체 재질로 기존 칸 색상과 소유자 색을 적용한다.
- [기계 판독용 출처 기록](asset-audit/synty-floor.json).

모마에서 추출한 아트는 계속 제외한다. 빌드 검사는 선택한 파일 경로와 해시만 허용하고 그 밖의 파일형 이미지·모델·음원은 기존처럼 검사한다.

## 보드 연결

`BoardFloorLayer`가 원본의 92개 정점을 가진 메시를 32개 복제한다. 윗면은 기존 `DiamondBoardLayout.Vertex`의 네 꼭짓점에 맞추고 옆면 두께는 캔버스 기준 12px로 맞춘다. 모서리 칸은 기존처럼 크다. 원본의 측면과 모서리 형상은 유지하며 색은 기존 보드 팔레트를 쓴다.

전용 직교 카메라와 투명 RenderTexture로 바닥을 그린 다음 기존 낙서 테두리·도시명·통행료·건물·말을 얹는다. 바닥은 보드와 같은 확대 행렬을 사용한다. 주사위 결과·이동 순서·클릭 영역·소유권 규칙은 변경하지 않았다. 좌우·양눈 시야 효과도 바닥에 함께 적용된다. 새 게임을 만들면 이전 바닥의 메시·재질·렌더 타깃을 정리한다.

## 실행과 빌드 검증

열린 Unity가 원본 프로젝트를 점유해 `Validation/FloorValidationProject`에 Assets·Packages·ProjectSettings를 복사하고 같은 Unity 6000.3.9f1로 빌드했다. 원본 에디터를 종료하거나 조작하지 않았다.

- `Validation/unity-synty-floor-build-validation.json`: 규칙 검사 290개, 빌드 Succeeded, 경고·오류 0개. 보고서의 빌드 경로는 검증용 복사본이다.
- `Validation/unity-synty-floor-verified/runtime-validation.json`: 실제 Windows 플레이어 0.15.0, passed. 다운로드 모델 로드, 32칸, 원본 정점 92개, 윗면 투영 오차 최대 0.0008px. 기존 시야·복구·이동 확대 검사 모두 통과.
- `Validation/unity-synty-floor-dice-verified/runtime-validation.json`: passed. 주사위 3+5, 무인도 8번 칸 도착, 던지기 후 이동 대기·도착 위치·결과 눈·연출 완화 확인.
- 실제 전체 보드 캡처 `Validation/unity-synty-floor-verified/01-normal.png`를 직접 확인했다.
- 검증된 빌드를 `Builds/Windows`로 반영했다. `Validation/unity-synty-floor-publish.json`: 146개 파일의 해시 일치, 불일치 없음.

주요 소스: `Runtime/BoardFloorLayer.cs`, `Runtime/MarbleBoardView.cs`, `Resources/BoardFloor.shader`, `Editor/MarbleBuildTools.cs`.
