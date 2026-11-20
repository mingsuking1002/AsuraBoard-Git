# Steam 멀티플레이

0.12.0 소스는 [방 음성과 환경설정](settings-and-voice.md)을 포함하며 최신 호환 키는 marble-classic-online-10이다. 아래 0.11.0 이전 호환 키는 당시 기록이다.

0.11.0 새 방은 [신체 경제 모드](body-economy/README.md)로 시작한다. 만기 미상환 플레이어만 탈락하고 남은 경기를 이어간다. 신체·부채·이동 경로도 동기화하며 호환 키는 marble-classic-online-9다.

0.9.0 소스는 메인 메뉴와 전용 대기 로비를 사용한다. 현재 배포 폴더의 실행 파일은 이전 0.8.0이며 새 소스의 빌드·실계정 검증은 아직 수행하지 못했다. [플레이 루프](play-loop.md).

Unity 프로젝트에 Steam 대기실과 2~4인 기본판 동기화를 추가했다. MECCHA CHAMELEON 설치본에서 확인한 방 생성·공개/비공개 검색·초대 UI의 역할을 참고했으며, 실제 구현은 이 프로젝트의 결정적 규칙 엔진과 Steamworks API를 연결한 별도 코드다.

## 플레이 순서

1. 모든 플레이어가 같은 Windows 빌드를 받는다. 실행 파일만 보내지 말고 `Builds/Windows` 폴더 전체를 전달한다.
2. 각 PC에서 서로 다른 Steam 계정으로 Steam을 온라인 실행한다.
3. 새 빌드에서는 메인 메뉴의 **방 만들기 / 방 찾기**에서 Steam에 연결한다. 이전 0.8.0 실행 파일은 상단 **Steam 멀티** → **Steam 연결**을 사용한다.
4. 호스트는 방 이름·정원·공개 범위를 선택해 **방 만들기**.
5. **친구 초대**에서 Steam 오버레이를 열고 초대할 친구를 선택한다. 초대를 수락한 클라이언트는 해당 방에 입장한다. 친구는 이미 동일 빌드를 실행해 두는 것이 현재 개발 ID에서의 테스트 절차다.
6. 호스트는 대기실에서 월드 여행·해안 여행 맵을 선택하고 **증강 선택 횟수**를 1~8회로 정한다(기본 4회). 변경하면 준비가 초기화된다. 호스트 외 참가자는 **준비 완료**. 최소 두 명이고 모두 준비하면 호스트가 **게임 시작**.
7. 자기 차례에 주사위·구매·건설 등을 조작한다. 온라인 기본판은 참가자 수대로 2~4명이며 AI 빈자리 채우기는 하지 않는다.

공개 방은 목록에서 검색한다. **친구 전용**과 **프라이빗**은 공개 목록에 표시하지 않는다. 프라이빗 방은 초대받은 사람만 입장하며, 방 ID를 안다는 사실만으로 초대 권한을 대신하지 않는다. 이는 Steam의 로비 공개 범위이며 별도 비밀번호 기능은 아니다.

Steam 연결에 실패해도 기존 로컬 모드는 사용할 수 있다. 온라인 중 로컬 세이브 로드·새 게임은 막고, 방을 나온 뒤 로컬 게임을 시작하도록 했다.

## App ID와 초대 실행

현재 `Assets/StreamingAssets/steam-config.json`:

```json
{"appId":480,"developmentAppId":true}
```

480은 현재 개발 테스트 설정이다. 공개 방 검색에 이 프로젝트만의 `game` 및 `compat` 메타데이터 필터를 넣어 다른 개발 게임과 섞이지 않도록 했다. 참고한 MECCHA의 App ID는 사용하지 않는다.

전용 Steam 앱이 준비되면 이 JSON의 `appId`를 그 앱의 ID로 바꾸고 `developmentAppId`를 `false`로 바꿔 다시 빌드한다. 개발 모드 빌드에는 `steam_appid.txt`를 쓰고, 출시 설정 빌드에서는 이전 개발용 파일을 제거한다. Steam 앱 설정의 실행 파일·디포 등록과 실제 업로드/출시는 별도 작업이며 수행하지 않았다.

실행 중 초대 수락 콜백과 `+connect_lobby <ID>` 실행 인수를 처리한다. **개발 ID 480의 초대가 꺼져 있는 이 커스텀 실행 파일을 자동으로 실행해 준다고 보장하지 않는다.** 앱이 꺼진 상태의 정식 초대 실행은 전용 App ID에 이 빌드를 등록한 뒤 검증해야 한다. 현재 두 친구가 같은 빌드를 먼저 실행한 상태에서 초대 기능을 테스트하는 절차를 제공한다.

Steam 오버레이가 꺼져 있으면 초대 버튼은 원인을 표시한다. 오버레이를 사용할 수 있는 Steam 실행 환경과 동일 빌드가 필요하다. 방 ID 입력은 Steam 측 입장 권한·빈자리·호환성 검사를 그대로 거친다.

## 동기화와 권한

- Steam 로비의 실제 멤버·소유자 ID를 사용하고, 패킷 안에서 주장한 송신자 ID를 신뢰하지 않는다.
- 호스트가 시드와 좌석 순서를 정하고 유효한 행동만 실행한다. 참가자는 자기 차례의 허용된 행동만 요청한다.
- 로비 ID·프로토콜·경기 nonce·행동 순번을 확인해 다른 방, 이전 경기, 중복 입력을 거부한다.
- 확정 행동은 Steam Networking Messages의 신뢰 전송으로 전파한다. 참가자는 같은 규칙 엔진으로 적용하고 상태 해시를 비교한다.
- 순번 누락이나 해시 불일치 시 호스트의 시드·행동 기록으로 상태를 재구성한다. 복구를 기다리는 동안 입력을 차단한다.
- 메시지·문자열·좌석·리플레이 길이를 제한하며 네트워크 데이터를 실행 코드로 평가하지 않는다.
- 경기 시작 후 추가 입장은 닫는다. 참가자 퇴장·호스트 변경·응답 시간 초과에는 경기를 종료하고 이유를 표시한다. 호스트 이관과 진행 중 재접속은 지원하지 않는다.
- 첫 파산 시 경기가 끝나고 이번 경기의 등수를 표시한다. 참가자 누구나 결과 화면에서 같은 방 로비로 복귀를 요청할 수 있다. 진행 중 게임 메뉴의 복귀도 현재 경기를 끝내고 전원을 로비로 돌린다. 방·맵·외형·증강 설정은 유지하며 준비를 초기화한 뒤 다시 시작한다. 영구 랭킹은 없다.

## 코드

| 파일 | 역할 |
|---|---|
| `Assets/Marble/Online/OnlineProtocol.cs` | 전송 형식·크기 제한·상태 해시·서비스 인터페이스 |
| `Assets/Marble/Online/OnlineSession.cs` | 대기·준비·호스트 판정·순서·복구·종료 |
| `Assets/Marble/Online/SteamRoomService.cs` | Steam 초기화·로비·오버레이·P2P·콜백 |
| `Assets/Marble/Runtime/MarbleSteamPanel.cs` | 방 생성·검색·초대·준비·시작 UI와 게임 연결 |
| `Assets/Marble/Online/OnlineSelfTests.cs` | 인메모리 다중 클라이언트 검증 |

Steamworks.NET **2025.164.1**은 기존 로컬에 있던 배포 소스에서 프로젝트의 embedded package로 복사했다. MIT 라이선스는 `Packages/com.rlabrecque.steamworks.net/LICENSE.md`에 유지했다. 패키지의 Windows 네이티브 DLL도 빌드에 포함한다.

API 기준: [Steam 로비와 초대](https://partner.steamgames.com/doc/features/multiplayer/matchmaking?l=english), [ISteamMatchmaking](https://partner.steamgames.com/doc/api/ISteamMatchmaking?l=english), [Steam Networking Messages](https://partner.steamgames.com/doc/api/ISteamNetworkingMessages?l=english).

## 검증 범위

최종 결과는 `Validation/unity-validation.json`, `Validation/steam-native-final-probe.json`, `Validation/online-verification.md`를 따른다. 인메모리 테스트는 잘못된 차례·위조 결과·중복·패킷 누락/해시 복구·연결 종료·재경기와 전체 경기 12회를 포함한다. 이 결과를 두 PC 간 Steam 통신 검증으로 바꾸어 말하지 않는다.

실제 Steam 초기화와 임시 프라이빗 방 생성은 성공했고, 검사 후 방을 나와 종료했다. 친구에게 초대는 보내지 않았다. **서로 다른 두 Steam 계정의 실제 초대 수락·P2P 경기·외부 사용자의 프라이빗 입장 거부는 아직 미검증**이다.

실행 파일 검사 모드 `-marble-steam-smoke -marble-steam-report <절대 JSON 경로>`는 실제 Steam에 임시 프라이빗 방을 만들고 결과를 저장한 뒤 방을 정리한다. 일반 실행에서는 사용자가 Steam 연결·방 만들기를 누르기 전까지 이 검사를 자동 실행하지 않는다.

증강은 모든 플레이어가 동시에 고르며 전원 확정 후 턴을 재개한다. 보유 한도는 8개, 보유 표시는 플레이어 카드 위에 있다. 상세 검증과 설정은 `Documentation/augments.md` 참조.
