# 플레이어 이동/행동 충돌 및 목적지 마커 — 2026-11-06

## 실제 적용 위치

- 실행 중인 Unity 프로젝트: C:/Users/Admin/Desktop/Github/Unity_PF
- 적용 전 HEAD: c69f2b1, main. 작업 트리 깨끗함 확인.
- 변경 파일: Assets/Storybook/Scripts/HongGildong.cs
- 이전에는 Documents/ChatGPT/Unity 포트폴리오/Unity_PF 복사본만 수정했다. 실제 열린 프로젝트 적용 여부를 확인하지 않고 완료 보고한 오류를 이번에 바로잡았다.
- 적용 전 플레이어 스크립트 Git blob 102814311e156f93bfb57124258ca882ddd211a2를 확인하고 원본을 백업했다. 다른 로컬 작업을 덮어쓰지 않았다.

## 동작 변경

- 공격·대시 발동 시 기존 destination을 제거하고, 종료 후 과거 목적지로 이동하지 않는다.
- 공격 입력을 일반 이동 처리 전에 처리한다. 기존 공격 중 48% 이동 코드를 제거했다.
- 공격·대시 중 우클릭은 예약하지 않는다. 행동 종료 후 새 우클릭부터 이동한다.
- 원형 이동 마커는 실제 보정된 목적지에 표시하며, 목적지 변경 시 갱신하고 도착·행동 취소 시 숨긴다.
- 향후 스킬은 PrepareForAction()을 기본 호출하고, 명시적인 목적지 유지 예외는 preserveDestination 인수로 지정할 수 있다. QWER는 추가하지 않았다.
- 기존 공격 판정·피격 반응 코드는 유지했다.
- Inspector의 Move Marker에서 반지름 .22m, 선 두께 .035m, 높이 .025m, 색상과 머티리얼을 조절할 수 있다. 추가 연결 없이 기존 Blade Trail 머티리얼을 사용한다.

## 확인 범위

- 실제 프로젝트 소스와 수정본의 SHA256 일치 확인.
- 열린 Unity의 Logs/Editor.log에서 스크립트 컴파일 및 어셈블리 재로드 확인. Library/ScriptAssemblies/Assembly-CSharp.dll은 2026-11-06 14:04:46 KST에 생성됨.
- git diff --check 통과. 로컬 미커밋이며 Push하지 않았다.
- 동일 수정본의 별도 작업 복사본에서 수행한 16개 Edit 모드 상태 검사는 통과했지만, 이를 실제 열린 에디터의 Play 모드 검사로 보고하지 않는다.
- 화면 도구가 두 차례 시간 초과로 실패하여 실제 Play 모드 시각 검증은 수행하지 못했다.

## Unity에서 확인할 순서

FirstClearing에서 Play:

1. 먼 곳 우클릭: 목적지에 원형 마커 표시.
2. 이동 중 좌클릭: 즉시 멈춰 공격, 마커 제거, 공격 후 정지 유지.
3. 이동 중 Space: 대시 후 정지. 대시 중 우클릭은 예약하지 않음.
4. 새 우클릭: 이동 재개 및 마커 위치 갱신. 도착 시 제거.
5. 허수아비 공격: 기존 흔들림/색상 피격 반응 유지.

## 커서 기준 대시 방향 수정
- 이동 여부와 무관하게 BeginDash에서 TryGroundPoint를 재사용하여 입력 순간 커서 방향을 XZ 평면에 투영한다.
- 근접 커서는 현재 바라보는 XZ 방향, 해당 방향도 무효이면 Vector3.forward를 사용한다.
- dashDirection은 시작 시 고정. 목적지 취소, 마커 제거, 충돌, 무적, 쿨다운, 거리/시간 수치는 변경하지 않았다.
- 기존 접지용 하향 이동은 유지하며 대시 방향의 Y 성분은 0이다.
- 실제 Play 모드 테스트는 미수행. 정지/역방향/직각 대시, 대시 중 커서 변경, 종료 후 정지, 충돌 및 무적 타이머/쿨다운 확인 필요.

### 에디터 미반영 원인 및 조치
소스 수정(14:08) 이후 DLL이 14:04 버전에 머물러 있었음을 확인했다. 실행 중인 Unity에서 Ctrl+R 새로고침을 실행했고, 2026-11-06 14:12:49 KST에 Assembly-CSharp.dll 재생성과 forced synchronous recompile/assembly reload 로그를 확인했다. C# 컴파일 오류는 확인되지 않았다. Play 모드 대시 동작 자체는 미검증이다.

## 대시 거리 조정
코드 기본값과 FirstClearing 씬의 dashDistance를 3m에서 2m로 변경. 지속시간 .25초, 무적 .2초, 쿨다운 2초는 유지.

## 침식된 게 신하 — 외형 및 HP/피격/사망
- 실제 적용 프로젝트: Desktop/Github/Unity_PF. 외부 모델 자산 없음 확인 후 기본 도형과 직접 생성한 찢어진 옷자락 Mesh로 제작.
- Prefab: Assets/Storybook/Enemies/CrabSoldier/CorruptedCrabSoldier.prefab. FirstClearing에 (0.6,0,-0.6) 배치. 붉은 Emission 눈, 청록 갑각, 집게 2개, 다리 2개, 손상 갑옷/관복, 보라 침식 흔적.
- 현재 GraphicsSettings 및 Quality의 render pipeline이 비어 있어 Built-in이다. 기존 장면을 임의로 URP로 전환하지 않고 Standard Emission을 사용. URP 전용 발광 머티리얼을 적용했다고 주장하지 않음. Bloom에 의한 빛 번짐은 미구현.
- AttackTarget.cs: 공통 피격 대상. StrawTarget도 이 타입을 사용하여 기존 허수아비 반응 유지.
- MonsterHealth.cs: Max HP(100), Current HP, 사망 상태, 사망 시 Collider 비활성화, .55초 뒤 적 비활성화. 사망 뒤 추가 피해 무시. Awake에서 최대 HP로 초기화, 풀링/부활 기능은 없음.
- MonsterHitVisual.cs: HP와 별도로 피격 .24초 흔들림/붉은 점멸, 사망 시 기울어지며 축소. 기존 허수아비의 흔들림 시간과 수식을 재활용. Animator/외부 사망 애니메이션은 사용하지 않음.
- HongGildong.cs: Inspector Attack Damage 25 추가. 기존 공격 타이밍/부채꼴 판정/한 공격당 중복 방지 유지, AttackTarget 대상으로 일반화. 이동/커서 대시/2m 거리/마커/무적 타이머 변경 없음.
- Editor/CrabSoldierBuilder.cs: Storybook > Build Crab Soldier 생성 도구. 저장된 FirstClearing에서만 동작하고 미저장 씬은 덮어쓰지 않음.
- Editor/CrabCombatVerification.cs: 검증용 .utmp/test-crab-request 파일이 있을 때만 Play 모드 검사. 검사 종료 후 자동으로 Edit 모드 복귀.

### 실제 Unity 검증
Unity 6000.6.0f1에서 프리팹 생성 및 씬 저장, 컴파일 완료. Play 모드에서 기존 StrikeTargets 메서드를 직접 호출하는 자동 검사 10개 통과(CRAB_COMBAT_TEST_PASSED: 10, Logs/Editor.log).
초기 HP 100, 공격력 25, 네 번의 공격별 HP 75/50/25/0, 각 공격을 중복 판정해도 1회 피해, 4회 사망, 사망 Collider 즉시 해제, 추가 피해 무효, 지연 후 비활성화 확인.
실제 마우스 클릭 입력/전체 공격 애니메이션 타이밍과 외형 시각 품질은 자동 판정 검증과 다르며 수동 확인 필요.

### Inspector 및 수동 확인
1. FirstClearing에서 Corrupted Crab Soldier 선택. MonsterHealth > Max HP를 변경 가능. Current HP는 런타임 상태이며 Awake에서 Max HP로 설정됨.
2. Hong Gildong > HongGildong > Attack Damage에서 기본 공격력 변경 가능. 기본값 25. 프리팹의 MonsterHitVisual에 Visual Root/Flash Renderers는 연결 완료.
3. Play에서 게 옆에 접근해 좌클릭 4회. Inspector Current HP 100→75→50→25→0과 붉은 점멸/흔들림 확인. 4회 후 쓰러짐/축소 및 비활성화 확인.
4. 죽은 대상 추가 공격 무반응, 기존 허수아비 피격 유지 확인.
5. 우클릭 이동/마커, 커서 방향 Space 2m 대시 회귀 확인. 이번 변경에서 AI/적 공격/드롭/경험치/QWER는 추가하지 않음.
6. 모델은 임시 도형 조합이며 의상 손상은 들쭉날쭉한 삼각형 옷자락과 비대칭 판금으로 표현함. 최종 리깅 모델은 아님.

## 게 실루엣 재작업
사용자 피드백에 따라 넓고 납작한 갑각, 돌출된 눈자루와 큰 붉은 눈, 곡선 Mesh로 만든 열린 집게, 갑각 옆 돌기로 외형을 수정. HP/피격/사망 코드는 변경 없음. 실제 Unity에서 프리팹을 갱신하고 별도 조명의 모델 미리보기를 렌더하여 확인. 최종 리깅 모델이 아닌 임시 도형 모델이며 게임 카메라 가독성은 추가 확인 필요.

## 문서 기준 게 신하 재디자인
제공된 게신하_Codex_아트_구현_명세.docx 본문과 내장 참고 이미지 3개 확인. 둥근 게 대신 마른 인간형, 창백한 날카로운 갑각, 비대칭 검붉은 집게, 붉은 두 눈, 찢어진 남색 관복 기준 적용.
내장 image_gen 도구로 참고 기반의 새 투명 단일 Idle 이미지를 생성함. 참고 이미지/포즈 목록을 애니메이션 시트로 분할하지 않음. 프롬프트 핵심: lean bipedal corrupted crab humanoid soldier, pale angular shell, two red eyes, asymmetric open crimson pincers, torn navy Korean palace official robe, pixel-art-inspired isolated static idle sprite, transparent, no text/staff/tail/warning symbols.
원본: Assets/Storybook/Enemies/CrabSoldier/CrabSoldierIdle.png. Unity Sprite Single/Point/압축 없음/Mipmap 없음, maxTextureSize 128. 원본 해상도 기준 PPU로 실루엣 높이 약 .85m 목표, 발밑 pivot (.5,.04). 프레임 정렬된 96px 애니메이션 제작 완료가 아님.
기존 모델은 LegacyPrimitiveCrab.prefab으로 백업하고 원래 Visual 자식도 비활성 상태로 보존. 기존 CorruptedCrabSoldier.prefab의 참조를 유지하여 실제 씬에 새 SpriteVisual 적용. 카메라 고정 각도에 맞춘 회전이며 카메라가 동적으로 바뀌는 경우 런타임 Billboard 추가 필요. 전용 3D 리깅 모델 아님.
MonsterHitVisual에 SpriteRenderer.color 점멸 분기 추가. Hit는 기존 흔들림, Death는 기울임/축소 후 비활성화. 전용 프레임 Hit/Death 애니메이션은 미제작. HP/공격력/대시/스킬 범위 변경 없음.
교체 후 Play 모드에서 기존 공격 판정 4회 HP 100→75→50→25→0, 중복 피해 방지/사망 뒤 피해 무효/비활성화 검사 10개 재통과. 크기 수정은 실제 게임 카메라 렌더로 확인. 최종 히트박스는 몸통 중심 Capsule 높이 .65, 반지름 .18, 중심 y=.35.
실제 게임 카메라 렌더를 crab-game-view.png로 생성해 크기/배치 확인. 마우스 입력을 통한 전체 애니메이션 타이밍은 수동 확인 필요. 빨간 눈은 스프라이트 픽셀 표현이며 URP Emission 3D 눈이 아님.

## 캐릭터 스타일 통일 및 카메라 가독성 조정
### Git 및 기존 상태 확인
2026-11-06 실제 프로젝트 Desktop/Github/Unity_PF에서 git fetch origin --prune 성공. main HEAD c69f2b1, origin/main과 ahead/behind 0/0. 기존 미커밋 씬/플레이어/피격 코드 및 신규 게 자산을 보존. 강제 초기화/Push 없음. 원격 외형 브랜치 별도 보존. 실제 ProjectVersion은 6000.6.0f1이고 요청 기준 6000.6.7f1과 다름.
현재 우클릭 이동, 좌클릭 공격, 커서 방향 Space 2m 대시, 무적 상태값/쿨다운, 목적지 마커, HP100/공격력25/사망이 존재한다. Shift/QWER 바인딩 없음. 무적은 피해 시스템 연동이 아닌 기존 상태값이라는 한계를 유지한다.
현재 카메라는 추적 코드가 없는 고정 Orthographic이었다. Cinemachine 패키지/구성 없음. 사용자가 설명한 추적 상태를 구현 완료로 가정하지 않고 실제 확인 후 보완했다.

### 새 외형과 움직임
홍길동 IdleFront.png 원본을 최우선 스타일 참고로 image_gen 내장 도구를 사용해 새 4열2행 투명 시트를 생성. 프롬프트: match simple hero pixel clusters, dark one-pixel outline, three shades per material, bipedal palace crab soldier, navy torn robe, pale armor, asymmetric crimson open claws, two red eyes, four breathing Idle + four heavy Walk, equal cells, no labels/background/staff/tail. 정밀 수작업 팔레트 일치가 아니라 생성 후 인게임에서 유사도를 확인한 스타일 통일 시안이다.
파일 CrabSoldierStyleSheet.png, Unity 256x128/Point/압축 없음, 각 셀 약64x64. 알파 영역을 기준으로 프레임을 잘라 발밑 pivot을 맞춘 IdleFrame0~3.asset, WalkFrame0~3.asset 생성. 기존 상세 외형은 DetailedSpriteCrabBackup.prefab으로 보존.
CrabSpriteAnimation.cs: Idle4프레임 5fps, Walk4프레임 7fps. 실제 XZ 이동량에 반응하며 previewWalk를 Inspector에서 켜서 정지 상태에서도 Walk를 확인할 수 있다. AI/추적/공격 패턴을 추가하지 않았으므로 평소 게는 제자리 Idle이다. Hit/Death는 기존 흔들림·붉은 점멸·기울기/축소. 전용 Hit/Death 프레임과 방향별 후면 스프라이트는 없음.

### 카메라/크기/설정
ClearingCamera.cs: followTarget을 Hong Gildong으로 연결. LateUpdate SmoothDamp(.12초), 지면 기준 추적. Orthographic 기본 수직Size 4.5→2.6, 최소 가로반폭 6.75→3.9(좁은 화면비 보정), X회전36→50도. offset(0,10,-8.391), lookAhead(0,0,.25). 원근 카메라/Cinemachine으로 교체하지 않았다.
캐릭터/허수아비 SpriteRenderer를 새 고정 시점에 맞춰 회전시켰다. 카메라 회전은 런타임 중 바뀌지 않는다. 홍길동 루트/스프라이트 Scale, 이동속도, 사거리, 대시거리2m, Collider는 변경하지 않았다. 게 새 프레임의 표시 높이 약 .98m를 목표로 PPU51.02041 적용, 외형만 조금 키움. 게 몸통 Collider는 기존 .65m 높이/.18m 반지름 유지.
Inspector: Clearing Camera > ClearingCamera의 Follow Target / Vertical Size / Minimum Half Width / Follow Smooth Time. 게 SpriteVisual > CrabSpriteAnimation의 Idle FPS / Walk FPS / Preview Walk. Preview Walk는 테스트할 때만 켜고 평소 해제.

### 검증 및 한계
실제 Unity 6000.6.0f1 Play 모드에서 14개 검사 통과: 화면비 보정 Orthographic, 카메라 변경 후 마우스 좌표 역투영 일치, Idle/Walk 각각4프레임 연결, HP100/공격력25/공격당1회 피해/4회 사망/사망Collider/추가피해무효/지연비활성화, 플레이어 위치 변경에 대한 카메라 추적.
첫 카메라 검사에서 화면비를 고려하지 않은 테스트 조건이 실패하여 조건을 수정 후 재검사 통과. 게임 코드를 실패에 맞춰 임의로 바꾼 것은 아님.
실제 게임 카메라 렌더로 시점/크기/스타일 확인. 직접 마우스 입력으로 이동/공격/대시/마커 전 과정을 시연한 검사는 아님. Walk 발디딤/Idle 눈 점멸/옷자락의 자연스러움은 생성된4프레임의 시각 품질 확인이 추가로 필요하다. 기존 배경은 고정 테스트방 이미지이며 넓은 용궁 레벨/가장자리 카메라 연출은 아직 완성되지 않았다.
다음 순서: 16:9 Game View에서 이동/역방향대시/좌클릭회귀 확인 → Preview Walk로 프레임 튐과 발밑 정렬 확인 → 실제 용궁 방 크기에 맞춰 카메라 경계와 배경 구성 → 방향별 스프라이트/Hit·Death 전용 프레임 보강.

## 스프라이트 품질 문제 진단 및 설정 복구
새 이미지는 생성하지 않았다. 홍길동 원본78x102/PPU52/Artwork Scale .46, 게 원본1774x887/이전 임포트256x128/IdleFrame0 43x48/PPU51.02041/SpriteVisual Scale1 확인. 양쪽 Point/무압축/Mipmap없음. 스케일을 무시하고 PPU 숫자만 비슷하게 맞춘 것이 오류였다. 축소된 게 텍셀의 월드 크기는 홍길동보다 약2.2배 컸다.
원본 자체도 일정한 논리 픽셀격자가 보장된 네이티브 도트가 아니라 생성된 픽셀풍 이미지이며, 작은 집게/해골기사형 얼굴은 원본 디자인 문제다. 임포트 설정으로 종족 실루엣을 바꿀 수 없으며 디자인 재제작이 필요하다. 참고자료의 게 얼굴/넓은 갑각/큰 집게를 최우선으로 하는 별도 원화 검토가 필요하고, 이번에 임의 재생성하지 않았다.
기존 프레임과 메타를 .utmp/quality-before에 백업. MaxTextureSize2048/NPOT None/Point/무압축/Clamp로 변경해 실제 임포트1774x887 복구. 단순 확대/업스케일 아님. 프레임Rect를 6.9296875배, PPU를 동일 비율로 보정(51.02041→353.5555)하여 8개 프레임의 월드 bounds 불변 검증. Transform/카메라/HP/피격/사망/홍길동 외형은 변경하지 않음. 생성 도구도256 강제축소를 다시 적용하지 않도록 수정.
Unity에서 CRAB_QUALITY_FIXED 로그 및 실제 게임 카메라 렌더 확인. 이전 축소 임포트보다 윤곽과 색 구분이 또렷해진 것을 확인했으나 원본에 그려진 굵은 색덩어리/불균일 픽셀밀도/인간형 디자인은 남는다. 이미지 원본을 복구한 것이 홍길동과 같은 도트 규칙을 재제작한 것은 아니다. HP 검사는 이번 설정 변경에서 다시 수행하지 않았고 기존 HP/공격 코드는 보존했다.
후속 원본 재제작 시 96x128 같은 고정 정수 프레임에서 게 얼굴/넓은 갑각/대형 집게의 단일 Idle부터 검토하고, 픽셀 한 칸의 월드크기를 홍길동(.46/52)에 맞춘 후 애니메이션으로 확장하는 것이 적절하다. 원본시트1774x887은4열2행 정수 셀로 정확히 나누어지지 않아 원본 프레임 정렬 검토도 필요하다.

## 게 신하 Idle 속도 조정
CrabSpriteAnimation의 Idle 기본값과 실제 CorruptedCrabSoldier 프리팹 Idle FPS를 5→2.5로 변경. 4프레임 주기 .8초→1.6초. Walk FPS7 유지, Hit/Death/HP 및 프레임 이미지/움직임 폭 변경 없음. SpriteVisual > CrabSpriteAnimation > Idle FPS에서 추후 조정 가능. 전체 Animator 속도를 바꾸지 않음.

Idle 추가 감속: 2.5→1.5FPS, 4프레임 한 주기 약2.67초. 다른 동작 유지.

## 공통 적 머리 위 HP바
기존 MonsterHealth의 CurrentHP/MaxHP 및 Damaged/Died 이벤트를 사용. HP 계산과 공격 판정 코드를 다시 구현하거나 수정하지 않았다.
신규 런타임 코드 Assets/Storybook/Scripts/EnemyHealthBar.cs. 신규 프리팹 Assets/Storybook/UI/EnemyHealthBar.prefab. 게 CorruptedCrabSoldier.prefab에 중첩 프리팹으로 연결 완료.
World Space Canvas + 어두운 테두리/빈 게이지/붉은 Image Fill. 숫자 없음. GraphicRaycaster/입력 EventSystem 추가 없음, 모든 Image Raycast Target 해제. 기존 우클릭/좌클릭 입력을 가로채지 않음.
피해 전 숨김. Damaged에서 최신HP비율 표시 및 마지막 피격시간+3초로 갱신. Died/몬스터 비활성화 시 즉시 숨김. Time.time 기준이므로 게임 일시정지 시 표시시간도 정지한다. 화면 밖이어도 시간은 정상 소모한다.
애니메이션 SpriteVisual 대신 MonsterHealth 루트에 연결. 초기 upright Sprite의 bounds높이×lossyScale로 머리 높이를 한 번 계산해 프레임/피격흔들림을 따라가지 않는다. LateUpdate(실행순서100)에서 루트 위치+카메라up 방향 높이로 배치하고 카메라 회전을 따라 읽기 쉽게 표시한다. 높이가 다른 적은 Height Override 지정 가능.
Inspector: 게의 Enemy Health Bar 자식 > EnemyHealthBar. Size 기본(.65,.07)m, Head Gap .12m, Height Override 0(자동), Visible Seconds3. 다른 적에서도 MonsterHealth가 있는 루트 아래 EnemyHealthBar.prefab을 자식으로 배치하면 자동 연결. Health/Body/View Camera를 직접 연결할 수도 있다. 여러 몬스터의 타이머와 HP비율은 각각 독립이다.
Editor/EnemyHealthBarBuilder.cs는 프리팹 생성 도구, Editor/EnemyHealthBarVerification.cs는 요청파일이 있을 때만 실행하는 검증 도구다.
실제 Unity6000.6.0f1 Play 모드 검사14개 통과(ENEMY_BAR_TEST_PASSED:14): 초기숨김,25피해75%,다른적무영향,3초이전표시,재피격50%,기존기한이후표시유지,카메라방향정렬,머리위위치,두번째적별도75%,첫적시간만료숨김,두번째적독립타이머,치명피해즉시숨김,적/카메라이동후중앙유지,표시중사망숨김.
게임카메라 렌더 enemy-hp-bars.png에서 서로다른 비율의 바가 독립표시되는 것을 확인. 테스트는 ReceiveDamage를 직접 호출하는 실제Play모드 검사이며 직접마우스공격을 시연한 검사는 아님. 임시복제 적은Play종료로제거됐다. 기존 공격/피격/사망/카메라/Idle1.5FPS 설정 변경 없음.
수동확인: FirstClearing Play → 게접근후좌클릭1회(HP75/붉은게이지75%) →3초대기(숨김) →3초안에추가공격(타이머갱신) →총4회공격(사망즉시숨김). 프리팹복제로각각다른체력표시및카메라추적중머리위위치를확인한다.
