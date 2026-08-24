# AZOR | Addressables 기반 게임플레이 데이터 캐시

> Unity Addressables로 게임플레이 데이터를 비동기 로드하고, 런타임 캐시·로딩 UI·리소스 해제를 한 흐름으로 관리한 사례

## 한 줄 소개

게임 시작 시 필요한 ScriptableObject, UI Sprite, 사운드 데이터를 Addressables 라벨 기준으로 비동기 로드하고, 런타임 조회용 Dictionary 캐시와 로딩 진행 이벤트, `AsyncOperationHandle` 해제를 `GameSession`에 통합했습니다.

## 프로젝트 맥락

- 장르: 3D 액션 RPG
- 역할: Unity 클라이언트 개발
- 핵심 기술: Unity 6 / C# / Addressables / ScriptableObject / Coroutine
- 중심 코드: `Assets/GameManager/GameSession.cs`

---

## 1. 문제 정의

스킬, 적 스폰, 스테이지 보상, 대화, 아이콘, 사운드처럼 게임플레이에 필요한 데이터가 여러 폴더와 시스템에 분산되면 다음 문제가 생깁니다.

1. 씬 진입 시 필요한 에셋의 로드 시점이 제각각이라 초기화 순서를 예측하기 어렵다.
2. 시스템마다 Addressables 로드를 직접 수행하면 중복 로드와 핸들 누수가 발생하기 쉽다.
3. 기획 데이터가 늘어날수록 에셋을 인스펙터에 수동 연결하는 방식은 유지보수 비용이 커진다.
4. 로딩 화면이 실제 준비 상태를 알 수 없어, 플레이어에게 진행 상황을 전달하기 어렵다.

**목표:** 게임플레이 진입 전에 필요한 공통 데이터를 한 번만 준비하고, 이후 시스템은 키 기반 캐시 조회만 수행하도록 만들었습니다.

---

## 2. 설계

```text
LoadingScene
    │  PreloadGameplayCacheAsync()
    ▼
GameSession (단일 진입점)
    ├─ Addressables 초기화 및 실패 처리
    ├─ 라벨/주소 단위 비동기 로드
    ├─ ScriptableObject · Sprite · Sound 런타임 캐시 구축
    ├─ 진행도 이벤트 발행 → Loading UI 갱신
    └─ 씬 종료/세션 정리 시 Handle Release 및 캐시 초기화
    ▼
Gameplay Systems
    └─ 스킬 · 스테이지 · 적 · 대화 시스템이 캐시를 키로 조회
```

설계의 핵심은 **Addressables 호출을 각 게임 기능에 흩어놓지 않고 `GameSession`으로 수렴**시킨 점입니다. 이후 각 기능은 에셋 경로나 비동기 작업을 직접 알 필요 없이, 준비된 데이터만 사용합니다.

---

## 3. 주요 구현 코드와 설명

### A. 로드 핸들과 런타임 캐시의 소유권을 한 곳에 명시

```csharp
private AsyncOperationHandle<IList<EnemySpawnData>> enemyDataHandle;
private AsyncOperationHandle<IList<SkillData>> skillDataHandle;
private AsyncOperationHandle<IList<SoundAsset>> soundAssetHandle;

private readonly Dictionary<int, EnemySpawnData> enemySpawnTable = new();
private readonly Dictionary<SkillType, SkillData> cachedSkillData = new();
private readonly Dictionary<string, SoundAsset> soundTable =
    new(StringComparer.OrdinalIgnoreCase);
```

**설명:** 로드 결과를 담는 `AsyncOperationHandle`과 실제 게임에서 빠르게 조회할 Dictionary를 분리했습니다. 핸들은 에셋 수명 관리에, Dictionary는 전투 중 반복 조회에 사용합니다. 이로써 적 스폰이나 스킬 사용 시마다 Addressables를 다시 호출하지 않습니다.

**캡처할 코드:** `GameSession.cs` 74~109줄

### B. 중복 실행을 막고, 게임플레이 데이터 준비 과정을 순차적으로 제어

```csharp
if (IsGameplayCacheReady)
    yield break;

if (isCacheLoading)
{
    while (isCacheLoading)
        yield return null;
    yield break;
}

isCacheLoading = true;

ReportGameplayCachePreloadStep("Initializing Addressables...", 0.02f);
yield return EnsureAddressablesInitializedCoroutine();
```

**설명:** 씬 전환이나 UI 이벤트로 로드 요청이 겹쳐도 같은 캐시를 중복 생성하지 않도록 `IsGameplayCacheReady`와 `isCacheLoading`을 구분했습니다. 초기화가 실패하면 이후 로드를 중단하고 오류 원인을 로그로 남깁니다.

**캡처할 코드:** `GameSession.cs` 224~249줄

### C. 라벨 기반 제네릭 로더로 데이터 유형별 중복 코드를 제거

```csharp
private IEnumerator LoadLabelCache<T>(
    object key,
    Action<AsyncOperationHandle<IList<T>>> cacheHandle,
    Action<IList<T>> buildAction,
    string debugName)
{
    AsyncOperationHandle<IList<T>> handle =
        Addressables.LoadAssetsAsync<T>(key, null);
    cacheHandle?.Invoke(handle);
    yield return handle;

    if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
    {
        Debug.LogError($"GameSession: failed to load {debugName}. key={key}");
        yield break;
    }

    buildAction?.Invoke(handle.Result);
}
```

**설명:** 적 데이터, 스킬 데이터, 사운드, UI Sprite 등은 라벨과 캐시 구축 방식만 다르고 로드 절차는 같습니다. 제네릭 로더로 공통 절차를 통합하고, 각 타입은 `buildAction`만 전달해 자기 캐시를 구축하게 했습니다.

**캡처할 코드:** `GameSession.cs` 892~912줄

### D. 에셋 배열을 게임플레이 키 기반 캐시로 변환

```csharp
private void BuildEnemyCache(IList<EnemySpawnData> loadedAssets)
{
    enemySpawnTable.Clear();
    foreach (EnemySpawnData data in loadedAssets)
    {
        if (data != null)
            enemySpawnTable[data.SpawnLevel] = data;
    }
}

private void BuildSkillCache(IList<SkillData> loadedAssets)
{
    cachedSkillData.Clear();
    foreach (SkillData skillData in loadedAssets)
    {
        if (skillData != null && !cachedSkillData.ContainsKey(skillData.skillType))
            cachedSkillData[skillData.skillType] = skillData;
    }
}
```

**설명:** Addressables가 반환한 리스트를 그대로 전역 공유하지 않고, 실제 도메인 키인 `SpawnLevel`, `SkillType`으로 인덱싱했습니다. 런타임에서는 선형 탐색 대신 키 조회를 사용하며, 데이터의 사용 의도가 코드에 드러납니다.

**캡처할 코드:** `GameSession.cs` 974줄 이후의 `BuildEnemyCache`, 1173줄 이후의 `BuildSkillCache`

### E. 로딩 진행도를 UI와 느슨하게 연결

```csharp
public static event Action<string, float> OnGameplayCachePreloadStep;

private static void ReportGameplayCachePreloadStep(string message, float progress)
{
    OnGameplayCachePreloadStep?.Invoke(message, Mathf.Clamp01(progress));
}
```

```csharp
ReportGameplayCachePreloadStep("Loading enemy stage data...", 0.34f);
yield return LoadLabelCache<EnemySpawnData>(
    EnemyDataLabel,
    handle => enemyDataHandle = handle,
    BuildEnemyCache,
    "EnemySpawnData");
```

**설명:** 데이터 로더가 특정 UI를 직접 참조하지 않도록 이벤트를 발행하고, `LoadingScene`이 이를 구독해 문구와 진행 바를 갱신합니다. 로딩 로직과 표현 계층의 결합도를 낮춘 구조입니다.

**캡처할 코드:** `GameSession.cs` 41줄, 250~280줄 / `LoadingScene.cs` 48~82줄

### F. 캐시 정리 시 모든 Handle을 해제해 수명 관리

```csharp
public void ClearGameplayCache()
{
    ReleaseHandle(ref playerProfileHandle);
    ReleaseHandle(ref enemyDataHandle);
    ReleaseHandle(ref skillDataHandle);
    ReleaseHandle(ref stageLevelConfigHandle);

    foreach (AsyncOperationHandle<Sprite> handle in skillIconHandles)
    {
        if (handle.IsValid())
            Addressables.Release(handle);
    }
    skillIconHandles.Clear();

    enemySpawnTable.Clear();
    cachedSkillData.Clear();
    IsGameplayCacheReady = false;
}
```

**설명:** 캐시를 비우는 동작과 Addressables Handle 해제를 함께 수행합니다. 특히 개별 Sprite처럼 리스트로 관리되는 동적 로드 Handle도 별도로 해제해, 씬 재진입이나 세션 종료 시 누적될 수 있는 메모리 점유를 관리했습니다.

**캡처할 코드:** `GameSession.cs` 309~385줄, 1458~1471줄

---

## 4. 포트폴리오 본문에 넣을 서술 예시

### 제목

**Addressables 기반 게임플레이 데이터 캐시 및 로딩 파이프라인 구축**

### 문제 → 해결 → 결과

> 스킬, 적 스폰, 스테이지 보상, 대화, UI 아이콘, 사운드가 각 기능에 분산되어 있어 초기화 순서와 에셋 수명 관리가 복잡해질 수 있었습니다. 이를 해결하기 위해 `GameSession`을 게임플레이 데이터의 단일 진입점으로 두고, Addressables 라벨 기반 비동기 로드와 런타임 Dictionary 캐시를 구성했습니다.
>
> 공통 로딩 절차는 제네릭 메서드로 통합하고, 각 데이터 타입은 도메인 키(`SkillType`, 스테이지 레벨 등)로 캐시를 구축하도록 설계했습니다. 로딩 단계는 이벤트로 UI에 전달하고, 세션 정리 시 모든 `AsyncOperationHandle`과 동적 Sprite Handle을 해제해 에셋 수명을 명시적으로 관리했습니다.
>
> 그 결과 게임 기능은 Addressables 경로나 비동기 로드 구현을 직접 알 필요 없이 캐시를 조회하게 되었고, 데이터 추가 시 라벨과 캐시 빌드 규칙만 확장하면 되는 구조를 만들었습니다.

### 면접에서 30초로 설명하는 버전

> AZOR에서는 게임 시작에 필요한 데이터를 시스템별로 제각각 로드하지 않고 `GameSession`에서 통합 관리했습니다. Addressables로 라벨 단위의 ScriptableObject와 Sprite를 비동기 로드한 뒤, 전투 시스템이 바로 사용할 수 있도록 스킬 타입과 스테이지 레벨 기준 Dictionary 캐시로 변환했습니다. 로딩 화면은 이벤트로 분리했고, 씬을 정리할 때는 Handle을 함께 Release해서 중복 로드와 리소스 수명 문제를 관리했습니다.

---

## 5. 화면 구성 제안

이 사례는 코드만 보여주면 약해 보일 수 있으므로 아래 3개를 한 화면에 배치합니다.

1. **좌측: 8~12초 영상/GIF** — 로딩 화면의 진행 문구와 Progress Bar가 변한 뒤 실제 스테이지에 진입하는 장면
2. **우측 상단: 구조도** — `LoadingScene → GameSession → Addressables/Cache → Gameplay Systems` 흐름
3. **우측 하단: 코드 캡처 2장** — `LoadLabelCache<T>`와 `ClearGameplayCache()`

코드 캡처는 한 장에 25줄 내외로 자르고, 주석으로 아래 세 가지만 표시합니다.

- `중복 로드 방지`
- `라벨 기반 공통 로더`
- `명시적 Handle 해제`

---

## 6. 정직하게 검증 수치를 추가하는 방법

현재 코드만으로는 성능 개선 수치를 단정할 수 없습니다. 빌드에서 아래 중 하나를 측정한 후, 측정 조건과 함께 결과를 추가합니다.

- 동일 장면 재진입 전후의 Reserved/Used Memory (Unity Memory Profiler)
- 캐시 준비 전후 혹은 재진입 시 Addressables 로드 요청 수 (Profiler/로그)
- 로딩 시작부터 `Gameplay cache ready`까지의 시간
- 스킬·적·스테이지 데이터 에셋 수와 캐시 조회 경로

**표현 예시:** `Windows Development Build, 1920×1080, 동일 PC에서 5회 측정 평균`처럼 조건을 함께 쓰고, 실제 측정값만 기입합니다.

---

## 7. 발표 시 피할 표현

- “Addressables를 사용했습니다”에서 끝내지 않는다.
- 측정하지 않은 메모리 절감률이나 로딩 시간은 쓰지 않는다.
- “모든 에셋을 프리로드했다” 대신, **게임플레이 진입에 필요한 공통 데이터와 UI 에셋을 라벨 기준으로 준비했다**고 정확히 표현한다.

