using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    public Transform playerTarget;  // 플레이어의 위치
    public Vector3 disOffSet = new Vector3(0, 15.17f, -10.19f);       // 카메라와 플레이어 간의 거리
    public Vector3 rotOffSet = new Vector3(50f, 0, 0);       // 카메라의 초기 회전 오프셋
    
    // 진동 강도 (0.0f ~ 1.0f)
    [Header("Haptic Settings")]
    public float lowFrequency = 0.5f;   // 저주파 강도 (깊은 진동)
    public float highFrequency = 0.8f;  // 고주파 강도 (날카로운 진동)
    public float vibrationDuration = 0.3f; // 진동 지속 시간 (초)
    private Gamepad currentGamepad;
    [Header("카메라 줌인 효과")]
    public float zoomSpeed = 2f;
    public float zoomAmount = 5f;
    public bool isZooming = false;
    private Vector3 defaultOffset;
    
    [Header("카메라 쉐이크 효과")]
    [SerializeField] private float roughness = 1.5f; // 흔들림 속도
    [SerializeField] private float magnitude = 0.3f; // 흔들림 강도
    [SerializeField] private float shakeDuration = 0.5f; // 기본 지속 시간
    LayerMask wallLayer;
    private Vector3 originalPos;
    private float seed;
    private Vector3 basePosition; // 기준 월드 포지션
    private Coroutine activeCameraFeedbackRoutine;
    private float activeFeedbackEndTime = -1f;
    private int activeFeedbackPriority = -1;
    private float lastFeedbackRequestTime = -1f;
    [SerializeField] private float feedbackReplaceWindow = 0.05f;

    public float fadeSpeed = 2f;

    private float currentAlpha = 1f;
    private WallClipping wall;
    public WallClipping currentWall;
    void Start()
    {
        wallLayer = LayerMask.GetMask("Wall");
        // 처음 시작할 때 카메라의 회전값을 설정
        transform.rotation = Quaternion.Euler(rotOffSet);
        originalPos = transform.localPosition;
        seed = Random.Range(0f, 100f);
        defaultOffset = disOffSet; // 기본 오프셋 저장
        currentGamepad = Gamepad.current;

        if (currentGamepad == null)
        {
            Debug.LogWarning("게임 패드가 연결되어 있지 않아 햅틱 반응을 실행할 수 없습니다.");
        }
    }
    public List<WallClipping> currentWalls = new List<WallClipping>();

    void Update()
    {
        RaycastHit hit;

        // 플레이어 → 카메라 방향
        Vector3 dir = (transform.position - playerTarget.position).normalized;
        float dist = Vector3.Distance(playerTarget.position, transform.position);

        Debug.DrawRay(playerTarget.position, dir * dist, Color.yellow);

        if (Physics.Raycast(playerTarget.position, dir, out hit, dist, wallLayer))
        {
            // 타겟 부모 찾기
            Transform parent = hit.collider.transform.parent;
            if (parent == null) return;

            // 부모의 모든 WallClipping 수집
            WallClipping[] wallsInParent = parent.GetComponentsInChildren<WallClipping>();

            // 새로운 벽 그룹이면 기존 그룹 복구
            if (!IsSameWalls(wallsInParent, currentWalls))
            {
                RestoreCurrentWalls(); // 기존 투명화 복구
                currentWalls = new List<WallClipping>(wallsInParent);
            }

            // 새 그룹에 투명 적용
            foreach (var w in currentWalls)
                w.SetAlpha(0f, fadeSpeed);
        }
        else
        {
            RestoreCurrentWalls();
        }

        basePosition = playerTarget.position + disOffSet;
        transform.position = basePosition;
    }

    private void RestoreCurrentWalls()
    {
        if (currentWalls == null) return;

        foreach (var w in currentWalls)
            if (w != null)
                w.SetAlpha(1f, fadeSpeed);
    }

    private bool IsSameWalls(WallClipping[] newWalls, List<WallClipping> oldWalls)
    {
        if (newWalls.Length != oldWalls.Count) return false;

        for (int i = 0; i < newWalls.Length; i++)
            if (newWalls[i] != oldWalls[i]) return false;

        return true;
    }


    public void Shake(float duration, float strength)
    {
        PlayHitFeedback(new CombatFeedbackRequest
        {
            cameraMode = CameraFeedbackMode.Shake,
            feedbackLevel = strength >= 0.45f ? HitFeedbackLevel.Heavy : HitFeedbackLevel.Medium,
            cameraDuration = duration,
            cameraStrength = strength,
            reactionTier = HitReactionTier.None
        });
    }
    public void Impulse(float duration, float strength)
    {
        PlayHitFeedback(new CombatFeedbackRequest
        {
            cameraMode = CameraFeedbackMode.Impulse,
            feedbackLevel = strength >= 0.22f ? HitFeedbackLevel.Medium : HitFeedbackLevel.Light,
            cameraDuration = duration,
            cameraStrength = strength,
            reactionTier = HitReactionTier.None
        });
    }

    public void PlayHitFeedback(CombatFeedbackRequest request)
    {
        if (request == null || request.cameraMode == CameraFeedbackMode.None || request.cameraDuration <= 0f || request.cameraStrength <= 0f)
            return;

        bool shouldReplace = Time.unscaledTime > activeFeedbackEndTime
            || request.Priority >= activeFeedbackPriority
            || (Time.unscaledTime - lastFeedbackRequestTime) > feedbackReplaceWindow;

        if (!shouldReplace)
            return;

        lastFeedbackRequestTime = Time.unscaledTime;
        activeFeedbackPriority = request.Priority;
        activeFeedbackEndTime = Time.unscaledTime + request.cameraDuration;

        if (activeCameraFeedbackRoutine != null)
            StopCoroutine(activeCameraFeedbackRoutine);

        if (request.cameraMode == CameraFeedbackMode.Shake)
            activeCameraFeedbackRoutine = StartCoroutine(ShakeCoroutine(request.cameraDuration, request.cameraStrength));
        else
            activeCameraFeedbackRoutine = StartCoroutine(ShakeImpulse(request.cameraDuration, request.cameraStrength));

        if (currentGamepad != null)
        {
            float high = Mathf.Clamp01(request.cameraStrength + 0.2f);
            StartCoroutine(RunHaptics(request.cameraStrength, high, request.cameraDuration));
        }
    }
    private IEnumerator ShakeImpulse(float duration, float strength) // 충격파 용도
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // 감쇠 계수 적용 (선형 감쇠)
            float damper = 1f - (elapsed / duration);

            // 임펄스형 랜덤 흔들림
            Vector3 shakeOffset = Random.insideUnitSphere * strength * damper;

            // y축 위주로 줄이고 싶다면 Vector3(x, y * 0.5f, z)
            transform.position = basePosition + shakeOffset;

            yield return null;
        }

        // 위치 복귀
        transform.position = basePosition;
        activeCameraFeedbackRoutine = null;
        activeFeedbackPriority = -1;
    }
    public void CameraZoom(bool state)
    {
        StartCoroutine(CameraZoomCor(state));
    }
    private IEnumerator CameraZoomCor(bool zoomIn)
    {
        isZooming = zoomIn;
        Vector3 startOffset = disOffSet;
        Vector3 targetOffset;

        // 카메라 시선 방향으로 가까워지기
        Vector3 zoomDir = transform.rotation * Vector3.forward;
        zoomDir.Normalize();

        if (zoomIn)
            targetOffset = defaultOffset + zoomDir * zoomAmount; // 시선 방향으로 이동
        else
            targetOffset = defaultOffset;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * zoomSpeed;
            disOffSet = Vector3.Lerp(startOffset, targetOffset, t);
            yield return null;
        }

        if (!zoomIn)
            isZooming = false;
    }


    private IEnumerator ShakeCoroutine(float duration, float strength) // 지진용도
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float tick = Time.time * roughness;

            float x = (Mathf.PerlinNoise(seed, tick) - 0.5f) * strength;
            float y = (Mathf.PerlinNoise(seed + 1f, tick) - 0.5f) * strength;

            // 회전된 방향에 맞게 흔들림 적용
            Vector3 offset = transform.right * x + transform.up * y;
            transform.position = basePosition + offset;

            yield return null;
        }

        // 원래 위치로 부드럽게 복귀
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 10f;
            transform.position = Vector3.Lerp(transform.position, basePosition, t);
            yield return null;
        }

        transform.position = basePosition;
        activeCameraFeedbackRoutine = null;
        activeFeedbackPriority = -1;
    }

    IEnumerator RunHaptics(float low, float high, float duration)
    {
        currentGamepad.SetMotorSpeeds(low, high);

        yield return new WaitForSeconds(duration);

        StopHaptics();
    }

    /// <summary>
    /// 진동을 즉시 멈춥니다.
    /// </summary>
    public void StopHaptics()
    {
        if (currentGamepad == null) return;

        // SetMotorSpeeds(0f, 0f)로 모터 속도를 0으로 설정하여 진동을 중지
        currentGamepad.SetMotorSpeeds(0f, 0f);
    }
}

