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

## 침식된 게 신하 추적 및 집게 내려찍기
### 작업 시작 상태
2026-11-06 Desktop/Github/Unity_PF에서 git fetch origin --prune 성공. main HEAD c6398f5, origin/main과 ahead/behind0/0, 작업 트리 깨끗함 확인. 기존 작업에 새 프로젝트 생성/강제 초기화/Push 없이 이어서 구현. 실제 설치 및 검증 Unity6000.6.0f1, 활성 파이프라인 Built-in이며 요청의6000.6.7f1/URP로 임의 전환하지 않음.

### 구현 파일 및 설정
- Scripts/CrabSoldierAI.cs: Idle→Chase→Windup→Slam→Recovery, Dead 상태. 감지8m, 공격시작1.8m, 이동속도2.5m/s, 준비.6초, 내려찍기/접촉판정.15초, 회복.7초, 피해15, 공격간격2초. 모든 값 Inspector 조정 가능. 기본 공격 영역 길이1.8m/폭1.1m, 전방 사각형 바닥 경고 표시. 공격 시작에 XZ 방향과 시작 시각 고정. nextAttack=시작+2로 관리하여1.45초 동작 시간을 추가 가산하지 않음.
- 기존 활성 NavMesh/Agent가 없으므로 같은 몸통 크기의 CharacterController 이동. 기존 CapsuleCollider는 중복충돌 방지를 위해 비활성, 공격대상 Collider는 CharacterController로 유지. 벽 관통 방지. 장애물 뒤 공격은 RaycastAll 가림 검사로 막음. 복잡한 경로 우회/경사면 경로 탐색은 없음.
- Scripts/CrabAttackVisual.cs: 기존 한 장 이미지의 오른손 큰집게가 보이는 화면 왼쪽 영역을 임시 분리하고 나머지를 세 조각으로 재조립. 원본 PNG/프레임/크기/팔레트는 변경하지 않음. 준비 중 집게영역 회전/상승, .6초에 빠른 내려찍기 포즈로 전환하여 바닥쪽 하강, 이어지는 .15초 접촉판정, .7초에 걸쳐 복귀. 완성된 도트 공격 프레임이 아니며 절단 경계/관복 일부 동반 움직임/방향별 신체 포즈 부족이 있는 임시 모션. 실제 공격 방향은 고정된 바닥 경고로 읽힌다.
- Scripts/CrabSpriteAnimation.cs: ActionLocked 속성만 추가. 공격 중 Idle/Walk 프레임교체를 잠가 임시 공격조각과 충돌하지 않도록 함. Idle1.5FPS/Walk7FPS/피격/사망속도 유지. 루트 회전/좌우반전을 하지 않아 이동방향별 원본 스프라이트가 임의로 뒤집히지 않음. 후면 전용 이동 스프라이트는 없음.
- Scripts/PlayerHealth.cs: 기존 플레이어 HP가 없어서 최대100/현재HP/피해 수신/사망 이벤트만 추가. HongGildong.IsInvulnerable이면 피해 무효. 사망 시 HongGildong 비활성화로 이동/공격/대시/마커 취소. 사망 뒤 추가피해 무시. 플레이어 HP HUD/리트라이/저장/완성된 플레이어 피격애니메이션은 추가하지 않음.
- 게 피격은 기존 흔들림/색변화/HP바 유지하며 공격을 취소하지 않음. 공격조각은 원본 SpriteRenderer의 피격 색상을 반영하고, 움직임은 별도 자식영역에서 처리. 게 사망 이벤트는 즉시 공격/경고취소 및 기존 사망처리로 연결. 플레이어 사망도 다음 AI Update에서 추적/공격중지, 피해수신은 사망즉시거절.
- Prefab CorruptedCrabSoldier.prefab에 AI/CharacterController 및 SpriteVisual/CrabAttackVisual 연결. FirstClearing의 Hong Gildong에 PlayerHealth 연결. Editor/CrabAISetup.cs는 생성/연결 도구, Editor/CrabAIVerification.cs는 요청파일이 있을 때만 실행하는 검증 도구.

### 실제 Unity 검증과 한계
Play 모드 검사23개 최종 재통과(CRAB_AI_TEST_PASSED:23, Logs/Editor.log). 실제 프레임 시간 경과로 감지/추적이동/Walk전환/벽앞정지를 확인. 공격 메서드와 경과타이머를 지정하는 검사로 준비중무피해, 방향고정및범위이탈회피, 실제 BeginDash 호출 후 무적피해무효, 접촉시15피해, 같은공격중복방지, 회복중이동금지, 회복완료, 시작기준2초간격, 공격중기존피격과HP바유지, 사망시경고/판정취소, 플레이어사망시조작/AI중지, 사망뒤추가피해무효를 확인했다.
공격 전체를 직접 마우스로 회피하며 시연한 테스트와 구분한다. 준비모션은 실제 카메라 렌더 crab-ai-windup.png로 확인했으며 완성된 픽셀 공격애니메이션으로 보고하지 않는다. 전용 모션 아트 보강, 장애물 경로우회, 플레이어HP UI/재시작은 미구현 범위다.

### Inspector 및 수동 테스트
CrabSoldierAI의 Target은 비워도 Start에서 PlayerHealth 자동 탐색. Detection Range8 / Attack Range1.8 / Move Speed2.5 / Windup Time.6 / Active Time.15 / Recovery Time.7 / Attack Damage15 / Attack Interval2 / Hit Length1.8 / Hit Width1.1을 조정. PlayerHealth의 Max HP100과 Current HP를 Inspector에서 확인.
1. FirstClearing Play, 8m 안에서 접근하는 게와 Walk 확인. 1.8m 안에서 이동중단/주황 바닥경고/집게들기 확인.
2. 준비중 우클릭으로 경고 밖 이동: 피해없음. 정면에 남으면 접촉 시 HP100→85, 같은공격에 추가감소없음.
3. 준비 후 .6초 부근 Space로 경고범위를 통과: 대시 무적 .2초 동안 피해없음. 대시 전체 .25초 중 마지막 .05초는 기존 규칙상 무적이 아님.
4. 회복 .7초에는 이동/재공격없음. 공격시작간격은2초, 사거리 밖이면 다시추적.
5. 테스트 Cube벽을 사이에 두면 추적관통과 벽너머피해가 없는지 정면/사선에서 확인. 자동 우회는 기대하지 않음.
6. 공격준비중 게를 죽이면 경고/공격취소. 플레이어HP0이면 조작/추적/공격중단. 게HP바/피격흔들림/기존카메라/우클릭마커 회귀확인.
7. 해당 테스트는 죽은 뒤 재시작UI가 없으므로 Play를 다시 시작해 반복한다. 스킬/보스/드롭/다른 몬스터/연속공격은 추가하지 않았다.

## 홍길동 Idle 시각 모션 추가
정면 IdleFront/후면 IdleBack 각1장만 존재하고 Animator 없이 HongGildong.cs가 Walk/Attack/Dash Sprite를 교체함을 확인. 기존 AI작업 미커밋 변경을 보존하고 Idle만 추가했다.
원본PNG/도트/비율/PPU52/ArtworkScale.46/Point필터/무압축 그대로 유지. 새전신이미지/4~6장프레임은 생성하지 않았다. HongGildongIdleVisual.cs는 Artwork 아래 런타임 Idle Motion Surface를 만들고 원본텍스처의UV를그대로쓰는메시에서만호흡/검쪽/옷자락/삿갓부위를미세변형한다. 하단20%정점은고정, 루트/부모Transform/Collider는애니메이션으로움직이지않음. 연속메시기반임시모션이며 완성된수작업도트프레임애니메이션이아니다. 머리끈/손/옷자락을독립리깅한것도아니므로 세부부위는대략적인높이/위치마스크기준이다.
HongGildong.cs에는 목적지보유여부 읽기속성 HasMoveDestination만 추가. 이동/공격/대시중이거나사망/비활성상태면새시각표시를숨기고기존SpriteRenderer로즉시복귀. 정지상태와기존IdleSprite일때만활성. 정면/후면원본과flipX를반영. 상태종료후Idle주기를0부터시작한다.
FirstClearing의 Hong Gildong/Artwork에 HongGildongIdleVisual 연결. Inspector Loop Duration1.5초, Strength1, Breath Pixels.85, Cloth Pixels.6, Sword Pixels.35, Hat Pixels.15. 픽셀단위는원본픽셀기준미세이동량이다. 강도를0으로하면움직임을끄고원본표시를유지할수있다. Collider/사거리/공격판정/이동속도/대시2m/원본게AI/HP바는변경하지않았다.
실제 Unity6000.6.0f1 Play 모드 검사14개통과(HONG_IDLE_TEST_PASSED:14). 정지Idle활성,1.5초주기,하단정점고정및상체변화,루프끝점연결,루트XZ및스케일유지,목적지지정즉시Idle중단,실제이동중Idle미재생,공격시기존렌더러/회복후복귀,대시시기존렌더러/종료후복귀,후면/flipX반영,Collider높이.72/반지름.17유지,비활성시Idle렌더링해제확인. 검사동안적AI만Play모드에서비활성화하여전투간섭을제외했고종료후원래씬으로복원됨. 입력은메서드호출로시작한상태전환검사이며 직접마우스/키보드시연과구분한다.
홍길동정면/후면PNG와meta의git diff 없음, 해시보존확인. 실제카메라렌더 hong-idle.png에서기존캐릭터모습/크기/머티리얼표시확인. 움직임자연스러움/머리끈미세움직임/도트가장자리의서브픽셀떨림은직접Play에서추가확인필요.
수동테스트: Play에서정지1.5초반복확인 →우클릭이동(Idle중단) →도착후복귀 →좌클릭공격/종료복귀 →Space대시/종료복귀 →정면·후면과좌우방향전환확인. 강하거나약하면Artwork컴포넌트의Strength/LoopDuration부터조절. Editor/HongIdleSetup.cs, Editor/HongIdleVerification.cs는설정/검증도구이다. QWER/다른기능은추가하지않았다.

## 게 신하 충전형 바닥 공격 경고
현재 코드 확인: 준비 .6초/접촉판정 .15초/회복 .7초, 길이1.8×폭1.1m의 전방직사각형, LineRenderer4점 경고선. 기존 선색상/너비/모양 및 TryHit의OverlapBox/피해15/대시무적/공격방향고정/공격간격은변경하지않음.
신규 Scripts/ChargedAttackWarning.cs: 기존LineRenderer에연결하는재사용컴포넌트. 원점과방향을Begin에서고정하고직사각형Mesh의길이만0→전체길이로늘려채운다. FillColor기본(0.9,.025,.04,.28), GroundLift .001(기존선기준추가높이), SortingOrder1. 캐릭터스프라이트보다뒤에그리며ZWriteOff/알파블렌딩으로표시.
CrabSoldierAI.cs: 같은elapsed=Time.time-attackStarted와windupTime으로SetProgress를호출. .15/.30/.45/.60초에25/50/75/100%. 별도타이머없음. 준비시간전에는최대.99999로제한. 기존contact=windupTime과같은Tick에서100%와피해검사시작. 기존 .15초 판정구간에는100%를유지하고회복진입시기존선과함께숨김. 단한번의피해성공시점이아니라기존접촉판정창시작과동기화한다.
CancelAttack/사망/플레이어사망/AI비활성화/공격상태비정상탈출에서채우기와선제거. 다음Begin은항상0부터시작. 기존피격은공격을취소하지않는규칙을유지했고새경직은추가하지않았다.
UI/GroundWarningFill.shader 및 .mat 추가: Unlit 투명셰이더, Built-in Always/URP SRPDefaultUnlit 패스제공. 실제프로젝트는Built-in활성이고Unity6000.6.0f1에서검증. URP파이프라인전환실행검사는미수행. 프로젝트파이프라인임의변경없음.
Prefab CorruptedCrabSoldier에ChargedAttackWarning과머티리얼연결. 현재열린씬에미저장표시가있어씬을저장하거나덮어쓰지않음. Editor/ChargedWarningSetup.cs는연결도구, ChargedWarningVerification.cs는요청파일이있을때만실행하는Play모드검증도구.
실제Play모드검사21개통과(CHARGED_WARNING_TEST_PASSED:21). 기존타이밍/직사각형크기유지,시작0%,셰이더컴파일,25/50/75%진행률과면길이,플레이어이동중원점/방향고정,판정전100%와피해금지,100%와피해시작동일Tick,중복피해없음,판정구간후제거,명시취소/재공격초기화,준비시간1.2초변경시자동보정,비정상상태이탈정리,실제대시무적,게/플레이어사망취소확인. 공격경과시간을지정해검사한Play모드검증이며전체전투를직접입력시연한것은아님.
50%시점실제카메라렌더 charged-warning.png에서절반길이의붉은반투명면과기존선/캐릭터앞뒤표시확인. 원본아트/HP바/홍길동Idle/Walk/게AI/피격/사망데이터는유지.
Inspector: 게루트 > ChargedAttackWarning > Fill Color의알파로투명도, Ground Lift로지면겹침, Sorting Order로그리기순서를조정. CrabSoldierAI > Windup Time을바꾸면충전시간도함께변함. Fill Material은연결완료. 같은직사각형공격을가진다른몬스터는BindOutline/Begin/SetProgress/Hide로재사용가능하며다른형태의공격Mesh는이번범위밖이다.
수동테스트: Play→게접근→0.6초동안원점에서앞으로차오르는지확인→공격순간가득찬후.15초판정종료시제거확인→준비중사망/플레이어사망시즉시제거→다음공격0%재시작→Windup Time1.2로시험후원복→대시무적/범위이탈회귀확인. 새로운패턴/몬스터/피해판정변경없음.

## 집게팔 분리 현상 원인 및 어깨 연결 보정
원인: 평소 단일 Sprite, 공격 중 코드에서 몸통3조각/집게1조각으로 임시분할. Animator/위치회전키프레임자산은없음. 오른집게의정규화Pivot(1,.73)은 잘린조각의손목쪽이었고,회전외에상승 .18×sprite높이/하강 .24×sprite높이를집게조각에만독립적용하여몸통과연결점을유지할수없었다. 모든조각SortingOrder가같아절단경계도가려지지않았다. 새전체공격스프라이트로교체하는구조가아니며 Begin시현재Idle/Walk원본을잘라사용했다.
수정파일 Scripts/CrabAttackVisual.cs. 기존분할/원본이미지를유지하면서 SpriteVisual아래 Right Shoulder Joint를추가. 어깨정규화좌표 Shoulder Anchor(.36,.69)를회전축으로고정하고집게조각을자식으로연결. 팔의상대위치/Scale은준비·타격·회복내내고정하고독립상승/하강위치키처리를제거했다. 각도곡선 -115도들기/18도내려찍기/0도회복과기존windup/slam/recovery매개변수는유지.
원본텍스처의어깨근처영역(x.25~.47,y.56~.78)을작은 Shoulder Overlap 조각으로중첩하여관절틈을가림. 집게는원본SortingOrder-1, 고정몸통은원본Order, 어깨겹침은원본Order+1. 원본도트/색상/비율/머리/다리/전체몸통재제작없음. 피격색상을어깨겹침에도동일반영. End에서임시조각을즉시숨겨같은프레임에이전조각이남아보이는문제도방지.
아트는여전히한장원본을잘라움직이는임시모션으로팔부분일부옷감움직임/관절의딱딱한꺾임은한계. 추가로자연스러운관절움직임이필요하면향후공격전용전체프레임또는별도어깨/상완/집게아트제작이대안이며이번에는새이미지를생성하지않았다.
검증도구 Editor/CrabAIVerification.cs에관절부모/겹침Sorting/준비·타격·회복9개시점의어깨고정·팔상대위치·Scale보존·회복후0도복귀검사를추가. 최초검증도구가공개Pose메서드를비공개검색으로호출해실패했으나도구호출범위를수정후실제Unity6000.6.0f1 Play모드35개최종통과(CRAB_AI_TEST_PASSED:35). 실제추적/Walk/충돌,공격타이밍/범위이탈/대시무적/중복피해/HP바/사망취소검사도포함. 공격시간은메서드로지정한상태검사이며직접입력전투시연은별도이다.
준비.59초/타격.6초/회복1.1초의실제게임카메라렌더(crab-arm-windup.png,crab-arm-impact.png,crab-arm-recovery.png)를확인하여팔과몸통사이에기존처럼떠있는틈이생기지않는것을확인. AI/ChargedAttackWarning/MonsterHealth의SHA256이작업전과동일함을확인. 피해판정및충전게이지타이밍은변경하지않음. 열린씬의미저장변경은저장하거나덮어쓰지않았다.
Inspector: SpriteVisual > CrabAttackVisual > Shoulder Anchor는원본Sprite사각형의좌하단기준정규화좌표. 기본값(.36,.69)으로연결완료하며외형이미지를바꾸지않는한추가설정불필요.
수동테스트: Play에서게공격유도 → 준비중큰집게와어깨사이틈확인 → .6초충전완료/타격시연결및피해타이밍확인 → .7초회복후기본자세확인 → 다시추적할때Idle/Walk복귀확인 → 공격준비중피격및사망시겹침조각잔류없는지/HP바정상인지확인. 정상속도와Pause후Step에서각시점도확인한다.

## 공격 팔이 몸통에 가려져 잘려 보이는 문제 추가 보정
현재 구조: Idle/Walk는단일SpriteRenderer, 공격때만코드가몸통3조각+오른집게1조각과어깨덮개를임시생성. Animator/공격전용전체프레임없음. SortingGroup없음을실제Play모드에서확인.
분석결과 이전보정의집게Order=몸통-1과넓은어깨덮개(.22×.22 원본비율)가집게안쪽을가리는것이주원인이었다. 어깨고정축자체는유지되었으나내려찍기18도에서팔이몸통안쪽으로들어왔다. 추가근접렌더에서원본Sprite자체손상이아니라잘라내기하단y.28이집게끝보다높아서집게끝조각이고정몸통쪽에남는문제도확인했다.
게임코드수정은 Scripts/CrabAttackVisual.cs만. ShoulderAnchor(.36,.69)→(.32,.69)로조금바깥보정. 집게Order몸통-1→몸통+1(공격파츠존재중에만),어깨덮개몸통+2. 덮개영역 x.26~.40/y.61~.75로축소하여집게전체를덮지않고관절만겹치게함. 내려찍기각도18→5도로안쪽묻힘완화,준비-115도와회복0도유지. 분할하단y.28→.16으로집게끝까지이동팔영역에포함하여고정위치에끝조각이남지않도록수정. 원본PNG/PPU/Scale/머리·다리·몸통그림재제작없음.
Idle/Walk는이임시파츠를사용하지않으며End에서기존단일렌더러/애니메이션으로복귀한다. 피격색상과어깨고정/사망정리는기존대로유지. AI/ChargedAttackWarning/MonsterHealth의작업전후SHA256동일확인으로공격시점·피해범위·충전·HP가변경되지않았음을확인했다. 열린미저장씬은저장/덮어쓰기하지않았다.
실제Unity6000.6.0f1 Play모드최종39검사통과(CRAB_AI_TEST_PASSED:39). 기존추적/Walk/충돌/공격/대시무적/HP바/사망검사에공격팔이몸통앞인지,덮개크기,SortingGroup없음,집게하단포함,9개시점어깨위치/팔상대위치/Scale불변,회복0도복귀검사추가. 검증도구 Editor/CrabAIVerification.cs에근접렌더추가. 실제카메라준비.59/타격.6/회복1.1초의 crab-arm-*-close.png 및일반화면렌더를확인하여가려짐/남는집게끝조각이해소된것확인. 직접입력으로전체전투를시연한검사와는구분한다.
한계: 단일Idle/Walk이미지를사각형으로잘라회전시키는임시방식이므로관절의딱딱한꺾임/회전시연결부겹침은남는다. 최소수정으로이번가림과집게끝잔류문제는완화/해소했지만전용Attack아트품질을완성했다고보고하지않는다. 추가자연스러움이필요하면Idle/Walk/Hit/Death와HP/AI는유지하고Attack만원본픽셀밀도·비율에맞춘전체프레임으로교체하는것이대안이다. 이번에는새전체프레임/전신파츠를제작하지않음.
확인설정: SpriteVisual > CrabAttackVisual > ShoulderAnchor(.32,.69) 기본. 실제공격중생성되는 Attached Right Claw는몸통보다앞,ShoulderOverlap은관절위만덮는순서. 별도SortingGroup추가불필요.
수동테스트: Play에서게공격유도 → 준비중팔과어깨연결확인 → Pause/Step으로충전100%/타격순간팔안쪽과끝까지보이는지확인 → .7초회복뒤원래자세확인 → 이동/피격/사망전환에임시파츠잔류없는지확인. 공격속도/충전/피해가이전과같은지도함께확인.

## 첫 번째 전투 방과 클리어 (2026-11-07)

- 테스트 씬 FirstCombatRoom을 추가했다. 기존 FirstClearing의 홍길동과 추적 카메라 설정을 재사용한다. 기본 도형 바닥과 벽으로 11×9m 방을 구성하고, 청록 입구와 황토색 출구를 둔다.
- 기존 CorruptedCrabSoldier Prefab 인스턴스 3마리를 배치했다. 방 내부 Trigger에 홍길동이 들어오면 입구 문을 닫고 세 AI를 활성화한다. 벽과 문 Collider가 전투 공간을 제한한다.
- CombatRoom은 등록된 MonsterHealth의 Died 이벤트로 즉시 생존 수를 갱신한다. 사망 후 비활성화는 집계를 바꾸지 않는다. 살아 있는 적의 임시 비활성화는 처치로 계산하지 않고, 제거된 참조는 다음 갱신에서 제외한다.
- 전멸 즉시 CombatRoomExit가 출구 Collider를 해제하고 문을 들어 올린다. 상단에 '방 클리어'를 표시한다. 출구 너머에는 확인용 발판을 둔다.
- HongGildong의 이동 한계를 Inspector의 Movement Bounds로 옮겼다. FirstClearing은 기존 한계값을 기본값으로 사용하고 FirstCombatRoom은 입구와 출구 발판까지 허용한다.
- Unity 6000.6.0f1 별도 복사본에서 11개 Play 모드 검사 통과. 실제 Trigger 입장, 세 적의 추적, 적의 피해, 문 충돌, 적 사망과 비활성화, 즉시 개방, 문 통과와 상승을 확인했다. 검증은 CharacterController 이동과 MonsterHealth 피해 호출을 사용했다. 배치 실행 중 Unity Editor Search 인덱싱 예외가 한 번 발생했으나 11개 검사는 모두 완료됐다. 직접 마우스·키보드 조작과 Game View의 한글 표시는 확인이 남아 있다.

## 첫 전투 방 쿼터뷰와 접지 (2026-11-07)

- FirstCombatRoom의 직교 카메라 X 각도를 50°에서 43°로 변경했다. 추적 Offset은 (0, 10, -8.391)에서 (0, 9.2, -9.8658)로 조정했다. 직교 Size 2.6, 최소 반폭 3.9, 추적 보간 .12초, 전방 오프셋 .25는 기존 값이다.
- 홍길동과 게 신하의 스프라이트 평면을 바닥에 수직으로 세웠다. 화면 높이 보전을 위해 세로 배율에 1/cos(43°)를 적용했다. 게 HP바는 보정 전 스프라이트 높이를 씬 인스턴스의 Height Override로 쓴다. 홍길동 발 위치 보정은 기존 .02m에서 이 씬만 .008m로 조정했다.
- 발밑 타원형 접지 그림자와 방 양옆의 2m 석주 두 개를 추가했다. 캐릭터 그림자는 바닥 위 .012m에 표시되고 깊이 버퍼를 검사한다. 석주는 불투명 머티리얼, 방향광은 Soft Shadows 강도 .62를 사용한다.
- CrabSpriteAnimation은 공용 최상위 부모 대신 자기 MonsterHealth 루트의 변위를 사용한다. 방 안에서 세 게가 실제로 추적할 때 Walk 재생을 확인했다.
- Unity 6000.6.0f1 복사본에서 Play 모드 검사 15개 통과. 새 카메라의 화면 좌표를 기존 지면 클릭 계산에 넣어 동일한 월드 지점으로 복원되는 것을 확인했다. 같은 위치 전후 렌더와 기둥 앞뒤 렌더에서 홍길동 전신 표시 및 가림을 비교했다. 마우스·키보드 직접 입력은 수동 확인 항목이다. 배치 실행에서 Unity Editor Search 인덱싱 예외가 한 번 있었으나 검사 15개는 완료했다.

## 용궁 던전 전투 방 연결 (2026-11-07)

- DragonPalaceDungeon 씬을 추가했다. 기존 전투 방의 11×9m 바닥·벽과 쿼터뷰 기둥을 재사용해 방 두 개를 5m 통로로 연결했다. 홍길동은 한 씬에서 통로를 직접 걸어 이동하며 카메라는 기존 방식으로 추적한다.
- 각 방은 기존 게 병사 Prefab 인스턴스를 2마리, 3마리씩 가지고 있다. 각 CombatRoom의 적 목록과 사망 집계는 독립적이다. 게의 최근 밸런스(1.6m/s, 감지 4.5m, 포기 6.5m, 공격 시작 1.3m, 판정 1.35×0.825m)를 공유한다.
- CombatRoom에 NotStarted, InCombat, Cleared 상태를 명시했다. 입장 시 입구를 닫고 소속 AI를 켠다. 전투 중 벽과 두 문 Collider가 방을 막는다. 전멸 시 기존 CombatRoomExit가 출구를 개방한다. 던전 씬의 Reopen Entrance On Clear는 클리어 후 돌아갈 수 있도록 입구도 개방한다.
- 1번 방의 Next Room은 2번 방을 참조해 2번 방 입장 시 이전 안내를 내린다. 2번 방 전멸 시 '용궁 던전 클리어'가 표시된다. 같은 씬에서 재방문할 때 클리어 상태와 처치 결과를 유지한다.
- Unity 6000.6.0f1 별도 복사본에서 Play 모드 검사 27개를 통과했다. 두 방의 실제 Trigger 입장, 잠긴 문 충돌, 통로 통과, 독립 집계, 중복 사망 호출, 대시 무적, 사망 비활성화와 재방문을 검증했다. 이동·공격 일부는 메서드 호출로 검사했으며 실제 마우스·키보드 입력과 한글 안내는 Game View 확인 항목이다.
- 기존 FirstCombatRoom 씬과 Prefab을 보존했다. 이번 던전 확인은 Unity에서 DragonPalaceDungeon 씬을 직접 열어 실행한다. 빌드 시작 씬 설정은 변경하지 않았다.
