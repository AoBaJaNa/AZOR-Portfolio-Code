# AZOR — Unity Client Portfolio

> 13인 팀의 메인 프로그래머이자 Unity 클라이언트 단독 담당으로, 전투부터 AI·콘텐츠 확장 구조·리소스 수명·최적화까지 구현한 3D 액션 핵앤슬래시 프로젝트입니다.

[![Unity](https://img.shields.io/badge/Unity-2022.3.55f1-000000?logo=unity)](https://unity.com/)
[![C%23](https://img.shields.io/badge/C%23-Gameplay%20Programming-512BD4?logo=csharp)](https://learn.microsoft.com/dotnet/csharp/)
[![URP](https://img.shields.io/badge/Rendering-URP-4C8BF5)](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest)
[![Addressables](https://img.shields.io/badge/Resources-Addressables-0A7EA4)](https://docs.unity3d.com/Packages/com.unity.addressables@latest)

[상세 Notion 포트폴리오](https://app.notion.com/p/3c974e134c798134a606de1d876b73a1) · [트레일러](https://drive.google.com/file/d/1kxkjxo_oMS6XraCvzj3wPhkIXM0GnNQv/view?usp=sharing) · [플레이 영상](https://drive.google.com/file/d/1fYwq0zUgarrLLAqpjoUhw1IvkejQ0ZBV/view?usp=sharing)

## 30초 요약

| 항목 | 내용 |
|---|---|
| 프로젝트 | 3D 액션 핵앤슬래시 · 2025.01–2026.09 예정 |
| 역할 | 메인 프로그래머 · Unity 클라이언트 프로그래밍 단독 담당 |
| 팀 | 13명 시작 · 졸업 후 3~4명으로 개발 지속 |
| 담당 | 플레이어 전투, 적 AI, 스킬·패시브, UI·인벤토리·저장, 리소스 관리, 최적화 |
| 대표 결과 | 피격 이펙트 Draw Call 약 **60~70 → 7~8** |

이 저장소는 전체 프로젝트 소스가 아니라, 제가 직접 구현한 코드 중 **설계 판단과 문제 해결 과정을 설명할 수 있는 사례**를 선별한 코드 포트폴리오입니다.

## 대표 구현 사례

| 사례 | 해결한 문제 | 코드 | 상세 설명 |
|---|---|---|---|
| **01. 전투 상태·AI** | 행동 증가에 따른 Controller 조건 분기와 적 유형별 중복 | [코드 보기](./01_전투_상태_AI) | [Notion](https://app.notion.com/p/91a74e134c79826cbc33014da9fd9d6c) |
| **02. 스킬·패시브·버프** | 신규 콘텐츠마다 공통 실행부를 수정하는 결합 | [코드 보기](./02_스킬_패시브_버프) | [Notion](https://app.notion.com/p/f7374e134c798330a301810dbd12a697) |
| **03. Addressables 수명 관리** | 중복 로드와 장착·스테이지 전환 뒤 잔존 리소스 | [코드 보기](./03_Addressables_리소스관리) | [Notion](https://app.notion.com/p/67074e134c7983d0bfa2013dde9e53dc) |
| **04. 시스템 성능 최적화** | 피격 이펙트 렌더링 병목과 전투 피크 할당 | [코드 보기](./04_시스템_성능최적화) | [Notion](https://app.notion.com/p/eec74e134c798213aeb1012d0824667a) |
| **05. 전투 가독성·편의성** | 난전의 사운드·시야·타격 피드백 과밀 | [코드 보기](./05_전투가독성_플레이편의성) | [Notion](https://app.notion.com/p/32a74e134c798350b57701d37576fb5f) |

### 01. 다양한 적·보스 패턴을 확장한 전투 상태·AI

- `IState`와 `StateMachine<T>`로 상태 수명주기와 전이 책임을 통일했습니다.
- 일반 적의 공통 상태를 기반으로 `PaganFleeState`, `BossCombatState`를 확장했습니다.
- `NavMeshAgent` 추적 프레임 끊김과 `Rigidbody` 넉백 후 위치 동기화 문제를 해결했습니다.

### 02. 공통 수정 없이 추가하는 스킬·패시브 구조

- ScriptableObject 데이터, 공통 실행 계약, 장착 수명과 Modifier 책임을 분리했습니다.
- **낙인 30스택 폭발**, **광전사 HP 소모↔회복**처럼 서로 다른 규칙을 개별 훅으로 구현했습니다.
- 풀링된 적 재사용 시 시간·스택·이벤트 상태를 함께 초기화했습니다.

### 03. 로드부터 해제까지 추적하는 Addressables 수명

`Preload → Dictionary Cache → Consume → Handle Tracking → IsValid → Release`

- 초기 진입 로드와 실제 소비 지점을 분리하고 진행 이벤트로 연결했습니다.
- 장착 변경과 스테이지 전환을 해제 시점으로 정해 Handle, 캐시, 테이블을 함께 정리했습니다.

### 04. 피격 이펙트 Draw Call 60~70 → 7~8

- HitParticle을 전용 Render Pass에서 별도 렌더링하고 Order in Layer를 정렬했습니다.
- 적·이펙트 풀링, NonAlloc 물리 쿼리, 스폰 Time Slicing, 고정 대기 객체 캐시를 함께 적용했습니다.
- Unity Profiler와 Frame Debugger로 병목 지점과 적용 결과를 확인했습니다.

## 추천 검토 순서

시간이 짧다면 **04 최적화 → 01 전투·AI → 02 스킬·패시브 → 03 Addressables** 순서로 확인해 주세요.  
각 Notion 페이지에는 문제 상황, 설계 판단, 핵심 코드, 실제 게임 적용 결과가 정리되어 있습니다.

---

Unity Client Programmer · 박지민
