AZOR 포트폴리오 핵심 코드 증빙

AZOR에서 직접 구현한 Unity C# 핵심 코드 중 전투·AI, 스킬·패시브, Addressables, 성능 최적화, 전투 가독성 사례를 선별한 코드 포트폴리오입니다.

##01. 전투 상태·AI 구조

Notion: https://app.notion.com/p/3a674e134c7980e1a830d84ca4e141d1

-StateMachine.cs, BaseState.cs, MoveState.cs, AttackState.cs: 플레이어 상태 수명주기와 전이
-PlayerController.cs: 입력을 현재 상태로 전달하는 중심 Controller
-EnemyClass.cs, EnemyController.cs, EnemyAttack.cs: 적 공통 생명주기, 이동·넉백·공격 책임
-EnemyIdleState.cs, EnemyAttackState.cs, EnemyStunState.cs, EnemyDeathState.cs: 일반 적 상태 구현
-PaganFleeState.cs, BossCombatState.cs: 적 종류별 확장 사례

##02. 스킬·패시브·버프 확장 구조

Notion: https://app.notion.com/p/3a374e134c79817aac82c2a0d6d3e821

-SkillData.cs, PlayerSkill.cs, SkillManager.cs: SO 기반 스킬 계약, 공통 실행, 키 조회
-PassiveSkillData.cs, PassiveSkillManager.cs, PlayerPassiveController.cs: 패시브 훅과 장착 수명 관리
-AttackBuff.cs, ShieldBuff.cs, AdditionalHitBuff.cs: 개별 버프 구현 예시
-PlayerInfo.cs: 키 기반 공격력 Modifier 관리
-EnemyStatusController.cs, Stigma.cs: 적 상태의 시간·스택·풀링 재사용 정리

##03. Addressables 기반 런타임 리소스 관리

Notion: https://app.notion.com/p/3a374e134c7981929c36cab8044781dd

-GameSession.cs: 초기화, 라벨 로드, Dictionary 캐시, Handle 수명 관리
-LoadingScene.cs: 캐시 진행 이벤트 구독과 씬 진입 제어
-GameManager.cs: 캐시 준비 여부를 확인하는 실제 소비 지점

##04. 시스템 성능 최적화

Notion: https://app.notion.com/p/3a474e134c79813e8147e7ad7c816e39

-HitParticleBatchFeature.cs: HitParticle 전용 Render Pass
-EnemyPoolManager.cs, EffectPoolManager.cs: 적·이펙트 풀링과 반환 수명
-SectionManager.cs: 스폰 Time Slicing과 풀 사용
-EnemyClass.cs, EnemyController.cs: 재사용 시 전투·이동 상태 초기화
-YieldInstructionCache.cs: 제한된 고정 대기 객체 캐시

##05. 전투 가독성·플레이 편의성

Notion: https://app.notion.com/p/3a474e134c7981ceb0f5eb170f4e49f0

-GameAudioManager.cs, GlobalEnemySoundManager.cs, SoundAsset.cs: 쿨다운·동시 재생 상한
-PlayerCamera.cs, WallClipping.cs: 벽 감지, 페이드, 시야 복구
-CombatFeedbackTypes.cs, SkillData.cs: 데이터 기반 타격 피드백 요청
-EnemyClass.cs, EnemyController.cs, InGameUI.cs: 피격 반응·카메라·Hit Stop 분배 및 제한
