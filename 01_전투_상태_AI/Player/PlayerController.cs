using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
[RequireComponent(typeof(Dash))]
public class PlayerController : MonoBehaviour
{
    private static readonly int MoveHash = Animator.StringToHash("Move");
    private static readonly int StunHash = Animator.StringToHash("Stun");
    private static readonly int AfkHash = Animator.StringToHash("AFK");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AniSpeedHash = Animator.StringToHash("AniSpeed");
    private static readonly int AfkIndexHash = Animator.StringToHash("AFKIndex");

    public StateMachine<BaseState> StateMachine { get; private set; }
    public MoveState MoveState { get; private set; }
    public DeathState DeathState { get; private set; }
    public StunState StunState { get; private set; }
    public ActionState ActionState { get; private set; }
    public AttackState AttackState { get; private set; }
    public LockState LockState { get; private set; }
    internal Animator animator;
    internal Dash dash;

    public bool IsStuned { get; private set; }

    public bool IsDead { get; private set; }

    public bool LockPlayer { get; private set; }

    public Volume volume;
    internal MotionBlur motionBlur;
    internal PlayerSkill playerSkill;
    internal PlayerPassiveController playerPassiveController;
    internal PlayerTargetSystem playerTargetSystem;
    internal PlayerMovement playerMovement;
    internal PlayerPortal playerPortal;
    internal PlayerCombat playerCombat;
    internal PlayerCamera playerCamera;
    internal PlayerEffectManager playerEffectManager;
    internal PassiveSkillManager passiveSkillManager;
    UIManager uiManager;

    bool isAFK = false;
    float lastInputTime;
    float afkDelay = 10f;
    private void Awake()
    {
        //motion blur find 
        volume.profile.TryGet<MotionBlur>(out motionBlur);
        motionBlur.active = false;
        playerMovement = GetComponent<PlayerMovement>();
        playerPortal = GetComponent<PlayerPortal>();
        playerTargetSystem = GetComponent<PlayerTargetSystem>();
        animator = GetComponent<Animator>();
        dash = GetComponent<Dash>();
        playerSkill = GetComponent<PlayerSkill>();
        playerCombat = GetComponent<PlayerCombat>();
        playerPassiveController = GetComponent<PlayerPassiveController>();
        playerCamera = FindFirstObjectByType<PlayerCamera>();
        passiveSkillManager = FindFirstObjectByType<PassiveSkillManager>();
        uiManager = FindFirstObjectByType<UIManager>();
        playerEffectManager = GetComponent<PlayerEffectManager>();
        SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        Initialize();

        StateMachine = new StateMachine<BaseState>();
        MoveState = new MoveState(this, StateMachine);
        DeathState = new DeathState(this, StateMachine);
        StunState = new StunState(this, StateMachine);
        ActionState = new ActionState(this, StateMachine);
        AttackState = new AttackState(this, StateMachine);
        LockState = new LockState(this, StateMachine);
        StateMachine.Initialize(MoveState);
    }

    private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
    {
        Initialize();
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
    }

    void Initialize()
    {
        if (SceneManager.GetActiveScene().name == "GameTitle" || SceneManager.GetActiveScene().name == "LoadingScene" || SceneManager.GetActiveScene().name == "Prologue")
        {
            SetLockPlayer(true);
            uiManager.UpdateMouseState(true);
            uiManager.CloseAllUI();
        }
        else
        {
            SetLockPlayer(false);
            uiManager.UpdateMouseState(false);
            uiManager.CloseAllUI();
        }
        playerMovement.Initialize();
    }

    public void OnMoveInput(InputAction.CallbackContext context)
    {
        if (!HasReadyStateMachine())
            return;

        StateMachine.CurrentState.OnMoveInput(context.ReadValue<Vector2>());
    }

    public void OnPortal(InputAction.CallbackContext context)
    {
        if (context.performed)
            StateMachine.CurrentState.OnPortalInput();
    }
    public void OpenPortal()
    {
        playerMovement.Initialize();
        playerPortal.SetIsPortal();
        RegisterInput();
    }
    private void Update()
    {
        playerTargetSystem.FindLockOnTarget();
        playerTargetSystem.UpdateArrowUI();

        if (LockPlayer)
        {
            if (animator != null)
            {
                animator.SetBool(MoveHash, false);
                animator.SetFloat(SpeedHash, 0f);
                animator.SetFloat(AniSpeedHash, 0f);
            }

            return;
        }

        if (!isAFK && Time.time - lastInputTime > afkDelay)
        {
            isAFK = true;
            animator.SetFloat("AFKIndex", Random.Range(0,2));
            animator.SetBool("AFK", isAFK);
        }
        StateMachine.Update();
    }
    public void SetDead(bool value)
    {
        IsDead = value;
    }
    public void SetLockPlayer(bool value)
    {
        LockPlayer = value;
        if (value)
            ResetAnimationToDefault();

        if(StateMachine != null && StateMachine.CurrentState != null)
            StateMachine.CurrentState.LockPlayer(value);
    }

    public void ResetTransientCombatState()
    {
        playerSkill?.ResetRuntimeSkillState();
        dash?.ResetRuntimeState();
        playerCombat?.ResetRuntimeCombatState();
        PlayerInfo.Instance?.ExitCombat();

        SetLockPlayer(false);

        if (StateMachine != null && MoveState != null)
            StateMachine.ChangeState(MoveState);

        playerMovement?.SetStopMovement(false, true);
        playerMovement?.Initialize();
    }
    public void Stun(float duration)
    {
        StateMachine.CurrentState.OnStunInput(duration);
    }
    public void SetStun(bool value)
    {
        IsStuned = value;
    }
    public void RegisterInput()
    {
        lastInputTime = Time.time;
        isAFK = false;
        animator.SetBool(AfkHash, isAFK);

    }

    public void ResetAnimationToDefault()
    {
        if (playerMovement != null)
        {
            playerMovement.SetMoveInput(Vector2.zero);
            playerMovement.SetStopMovement(true, true);
            playerMovement.Initialize();
        }

        if (animator == null)
            return;

        animator.SetBool(MoveHash, false);
        animator.SetBool(StunHash, false);
        animator.SetBool(AfkHash, false);
        animator.SetFloat(SpeedHash, 0f);
        animator.SetFloat(AniSpeedHash, 0f);
        animator.SetFloat(AfkIndexHash, 0f);

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger)
                animator.ResetTrigger(parameter.name);
        }

        animator.Play("Idle", 0, 0f);
        animator.Update(0f);
    }

    private void FixedUpdate()
    {
        if (StateMachine == null)
        {
            Debug.LogError("StateMachine is null");
            return;
        }
        if (StateMachine.CurrentState == null)
        {
            Debug.LogError("CurrentState is null");
            return;
        }

        StateMachine.FixedUpdate();
    }

    public void OnDashInput(InputAction.CallbackContext context)
    {
        if (!context.started) return; // 눌렸을 때만
        if (!HasReadyStateMachine())
            return;

        RegisterInput();
        if (dash.currentDashCount == 0)
        {
            InGameUI.ShowWarning("Dash가 쿨타임입니다");
        }
        else if (dash.currentDashCount > 0)
        { 
            StateMachine.CurrentState.OnDashInput();
        }
    }
    public void UseDash()
    {
        dash.PerformDash(playerMovement.Direction);
        motionBlur.active = true;
    }
    public void OnHealInput(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (!HasReadyStateMachine())
            return;

        StateMachine.CurrentState.OnHealInput();
    }
    public void UseHeal()
    {
        RegisterInput();
        PlayerInfo.Instance.UseHPStone();
    }
    private bool HasReadyStateMachine()
    {
        return StateMachine != null && StateMachine.CurrentState != null;
    }

    public void ReceiveBossDamage(int damage, GameObject source, float shakeDuration = 0f, float shakeMagnitude = 0f)
    {
        if (!HasReadyStateMachine())
            return;

        StateMachine.CurrentState.OnDamage(damage, false, source);
        if (playerCamera != null && shakeDuration > 0f && shakeMagnitude > 0f)
            playerCamera.Shake(shakeDuration, shakeMagnitude);
    }

    private void OnTriggerEnter(Collider other)
    {
        BossDamageSource damageSource = other.GetComponentInParent<BossDamageSource>();
        if (damageSource != null && damageSource.TryApply(this))
            return;

    }
}

