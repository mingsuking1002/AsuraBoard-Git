# 게임 작업공간

공용 오프라인 위키는 Game-Analysis-Wiki-OpenSource/offline/game-wiki.html이다. 연결된 게임은 기능이나 인계 내용이 달라진 작업에서 해당 게임 문서 원본·누적 이력을 갱신하고 등록된 wiki 검사를 실행한다. 다른 프로젝트와 공유하는 결정은 영향을 받는 게임과 근거 문서 경로를 기록한다.

먼저 사용자가 지목한 게임의 루트와 그 안의 AGENTS.md, 현재 인수인계 문서를 확인한다. 개별 게임에 이미 정해진 개발·검증 절차를 따른다. harness.json이 있는 게임은 GameHarness/README.md의 공통 명령을 사용한다. 각 게임의 검사 기록과 작업 카드는 그 게임 폴더에 둔다. 다음 프로젝트를 연결할 때 기존 지침과 검증 명령을 확인하고 해당 프로젝트에 맞춰 병합한다.

새 복제본은 루트에서 `git config core.hooksPath .githooks`로 공개 파일 검사를 활성화한다. 커밋 전 `python tools/check_public_tree.py`가 Git 인덱스의 경로와 본문을 검사한다.
