# 겜블 눈 매각 시야 효과 재확인

2026-10-27, Gamble With Your Friends 설치본의 `Assembly-CSharp.dll`을 정적으로 분석했다. 게임을 실행하거나 설치본을 변경하지 않았다.

## 확인한 동작

`PlayerEyesUI`에는 좌우 눈의 활성 상태와 URP `Vignette`, `blindnessVolume` 참조가 있다. `ToggleEye`가 한쪽 상태를 갱신하고 `UpdateVignette`를 호출한다.

| 눈 상태 | Volume weight | Vignette center |
|---|---:|---|
| 양쪽 정상 | 0 | 갱신 없이 반환 |
| 왼눈 없음 | 1 | (0.75, 0.5) |
| 오른눈 없음 | 1 | (0.25, 0.5) |
| 양눈 없음 | 1 | (0.5, 2.0) |

이후 Vignette를 활성화하고 중심 좌표를 Override한다. 왼눈이 없으면 시야 중심을 오른쪽으로, 오른눈이 없으면 왼쪽으로 옮긴다. 따라서 없어진 눈 방향을 더 어둡게 가리는 비네트 연출이라는 해석이 코드와 부합한다. 양눈이 없으면 중심을 화면 위 바깥으로 옮긴다. 전체 화면 가우시안 블러로 대체하는 것은 이 좌우 처리와 다르다.

위 표는 CIL에서 확인한 분기와 상수다. 검은 영역의 정확한 범위·색·경계 부드러움은 VolumeProfile의 직렬화 값과 런타임 화면을 이번 확인에서 조사하지 않았으므로 확정하지 않는다. 특히 정확히 화면 절반이 완전한 검정이 된다고 주장하지 않는다.

## 근거와 범위

- 대상 SHA-256: `f2147868930f1fc9df3489ef89339759f7eeb8a92c270bec094a4ece6dd2935b`
- MVID: `d531044a-de62-4d57-85e9-3914d8e9adc0`
- 이번 설치본 식별 Evidence: `ev_91391e8bb8cdcace3eb47e50fe2fe9963c7bbb5fa5f50535ddee1eb79b24eb58`
- 이번 PlayerEyesUI 선택 멤버 Evidence: `ev_5239b61043e98965ee59525569ea271e9ff0ca3e5e819fcf9a159c895193ca4d`
- UpdateVignette 토큰: `0x06001669`, raw CIL SHA-256: `8afbfa258cd28ec7d38dbedaf8c56e97f6a545b9c467a7827fec4350bc723b46`
- 저장된 전체 관련 IL: [gamble-organs-il.txt](gamble-organs-il.txt), PlayerEyesUI 422행 이후. 이번 REA의 상수·분기와 일치한다.

0.14.1의 전체 화면 블러는 0.14.2에서 좌우 시야 가림으로 교체했다. 중심 이동 방식은 이 조사 결과를 참고했으며 마스크의 경계와 강도는 직접 구현한 값이다. [구현과 실행 검증](../unity-vignette-runtime.md).
