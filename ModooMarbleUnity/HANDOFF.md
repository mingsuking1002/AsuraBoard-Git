# 아수라판 인수인계

- 담당·상태 — Antigravity 교대 완료(2026-11-19 23:46 KST). 새 게임 작업 지정 대기. 게임 소스 변경 없음.
- Antigravity 교대 확인 — 루트에서 doctor를 다시 실행해 `Environment OK`, 등록 검사 source·front·loop·dice, 종료 코드 0을 확인했다. harness.json의 필수 도구와 source 명령이 powershell.exe로 바뀐 것과 5.1 소스 검사 report.json이 passed·exitCode 0인 것을 읽어서 확인했다. Antigravity 세션에서는 check를 새로 실행하지 않았다. 이번 교대에서 Antigravity가 수정한 파일은 HANDOFF.md뿐이다.
- 인수 이슈 해결 — Antigravity에서 status는 정상, doctor는 `Missing: pwsh`로 실패했다. harness.json의 필수 도구와 source 명령을 powershell.exe로 통일했다. Codex에서 pwsh를 제외한 PATH로 doctor와 check source를 실행해 Windows PowerShell 5.1.26100.9818에서 모두 종료 코드 0을 확인했다. [5.1 소스 검사](Validation/harness-runs/20261119T144423Z-source-a8b60f7d/report.json)
- 위키 인계 — Documentation/wiki에서 시작·유지보수·변경 이력을 읽는다. 이 인수인계 문서도 check wiki를 실행할 때 공용 오프라인 위키에 반영된다.
- 변경 — AGENTS.md, harness.json, Tools/handoff_hook.py, .codex/, .agents/hooks.json, README.md, .gitignore, 공통 GameHarness/.
- 기준 빌드 — 기존 Windows 0.19.0. 게임 소스 변경 없음.
- 통과 — doctor, 양쪽 시작 훅 핸들러 직접 실행, [소스 검사](Validation/harness-runs/20261119T143724Z-source-078e4eea/report.json), [Windows 경기·로비 복귀](Validation/harness-runs/20261119T143757Z-loop-8f96ed66/report.json).
- 이번에 미실행 — front, dice. 이번 변경 범위는 하네스와 지침이다.
- 미검증 — 두 클라이언트의 새 세션에서 훅 자동 실행, 실제 두 Steam 계정 및 마이크.
- 다음 행동 — 사용자가 새 게임 작업을 지정하면 목표와 플레이어가 확인할 수 있는 완료 조건을 먼저 적고, 변경 범위에 맞춰 check source·front·dice·loop를 실행한다.
