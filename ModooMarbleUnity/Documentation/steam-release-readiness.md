# Steam 출시 준비 상태

현재는 플레이 가능한 개발 빌드이며 Steam 출시 완료나 Valve 심사를 통과한 빌드가 아니다. 원본 게임을 조사한 참고 자료는 빌드 아트로 넣지 않고, 보드 기능·정보 배치만 참고해 사용자 보유 에셋과 자체 비주얼을 사용한다. 참고한 자료: [넷마블 공식 게임 소개](https://m.netmarble.net/smart/game/momak), [모두의마블 보드와 플레이어 정보 가이드](https://c2.img.netmarble.kr/web/board/moma/v/html/guide/iframe/guide3.html?dm=game2.netmarble.net), [Pummel Party Steam 소개](https://store.steampowered.com/app/880940/Pummel_Party/).

## 출시 전 필요한 것

1. **전용 Steam App ID**: `Assets/StreamingAssets/steam-config.json`은 현재 개발용 480이며 `developmentAppId`가 true다. 전용 ID 발급 뒤 값을 변경하고 개발 설정을 해제해야 한다. 빌드 설정은 개발 모드에서 `steam_appid.txt`를 생성하며, 480으로 출시 설정을 빌드하려 하면 중단한다. Steam 업로드 폴더에 이전 파일이 남지 않는지 확인해야 한다. [Steam API 초기화 안내](https://partner.steamgames.com/doc/sdk/api?language=english).
2. **실제 두 계정 테스트**: 초대·프라이빗 방·준비·시작·종료·재접속·호스트 이탈·설정 동기화와 마이크 항상 열림/눌러서 말하기를 실제 Steam 환경에서 확인해야 한다. 현재 자동 검사는 가짜 백엔드와 합성 음성이다.
3. **도전과제**: 전용 App ID의 Steamworks에 코드와 같은 ID를 등록하고 게시한 뒤 해금·저장·재접속 동기화를 실계정으로 확인해야 한다. 개발 ID 480에서는 게임 내 로컬 해금만 사용한다. [Steam 도전과제 문서](https://partner.steamgames.com/doc/features/achievements?l=english).
4. **게임 콘텐츠 결정과 품질 검수**: 증강 선택지는 이전 요청에 따라 내용이 비어 있고, 별도 특수능력 모듈은 자동 적용되지 않는다. 출시할 콘텐츠를 확정한 뒤 실제 대전 밸런스·음성·시야·접근성·저사양 성능·긴 경기 안정성을 검증해야 한다. 구버전에서 창 크기 변경 후 종료 중 네이티브 오류가 한 번 있었고, 새 빌드 반복 확인에서는 재현되지 않았으나 원인은 아직 확인하지 못했다. 현재 사운드는 합성 효과음이며 음악은 준비되지 않았다.
5. **출시 자산과 권리**: 사용자 보유 Synty/Sidekick/Simple Town 리소스의 사용 범위와 제3자 의존성을 배포 조건에 맞게 확인하고, 상점 캡슐·스크린샷·소개 문구·연령/콘텐츠 정보를 준비한다. 기존 플레이어 캡처는 개발 검증용이다.
6. **스토어·빌드 심사**: Steamworks 체크리스트를 채우고 상점 페이지와 빌드를 각각 심사받아야 한다. Steam의 현재 출시 절차와 일정 조건은 [공식 출시 문서](https://partner.steamgames.com/documentation/releasing)를 확인한다.

## 현재 확인된 것

Unity 소스·플레이어 자동 검증, 로컬 플레이 루프, 오프라인/가짜 백엔드 온라인 시뮬레이션, 외형 선택·동기화 형식, 눈 시야와 주사위 이동을 확인했다. 계정 인증, 실제 Steam 네트워크·음성, 스토어 권한 및 Valve 심사는 이 환경에서 확인하지 못했다. [폴리싱 검증 결과](release-polish-018.md).
