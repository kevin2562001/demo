﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Photon.Pun;
using Photon.Realtime;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CharacterMovement))]
public class CharacterEntity : BaseNetworkGameCharacter
{
    public const float DISCONNECT_WHEN_NOT_RESPAWN_DURATION = 60;
    public const int MAX_EQUIPPABLE_WEAPON_AMOUNT = 10;

    public enum ViewMode
    {
        TopDown,
        ThirdPerson,
    }

    [System.Serializable]
    public class ViewModeSettings
    {
        public Vector3 targetOffsets = Vector3.zero;
        public float zoomDistance = 3f;
        public float minZoomDistance = 3f;
        public float maxZoomDistance = 3f;
        public float xRotation = 45f;
        public float minXRotation = 45f;
        public float maxXRotation = 45f;
        public float yRotation = 0f;
        public float fov = 60f;
        public float nearClipPlane = 0.3f;
        public float farClipPlane = 1000f;
    }

    public ViewMode viewMode;
    public ViewModeSettings topDownViewModeSettings;
    public ViewModeSettings thirdPersionViewModeSettings;
    public bool doNotLockCursor;
    public Transform damageLaunchTransform;
    private Transform[] launchTransformArray;
    public Transform[] LaunchTransformArray => launchTransformArray;
    public Transform effectTransform;
    public Transform characterModelTransform;
    public GameObject[] localPlayerObjects;
    public float dashMoveSpeedMultiplier = 3f;
    [Header("Lookat angle limit")]
    [SerializeField] float maxUpperHAngle = 150.0f;
    [SerializeField] float maxUpperVAngle = 45.0f;
    [SerializeField] float upperRotateSpeed = 0.5f;
    [Header("Boost")]
    [SerializeField] float baseBoostSpeed = 3f;//change to game instance after testing
    [SerializeField] int boostActionCost; //change to game instance after testing
    [Header("Jump Settings")] //should move to characterEntity or gameinstance when all testing is done
    [SerializeField] int baseJumpEnergyCost = 10;
    [SerializeField] int baseJumpHoldEnergyCost = 1;

    [SerializeField] float groundedJumpBoostForce = 3;
    public float GroundJumpBoostForce => groundedJumpBoostForce;


    
    [Header("UI (Enemy view)")]
    public Transform hpBarContainer;
    public Image hpFillImage;
    public Text hpText;
    public Text nameText;
    public GameObject attackSignalObject;
    public GameObject attackSignalObjectForTeamA;
    public GameObject attackSignalObjectForTeamB;
    [Header("Effect")]
    public GameObject invincibleEffect;
    [Header("Online data")]

    #region Sync Vars
    protected int _hp;
    protected int _armor;
    protected int _boostEnergy;
    protected int _exp;
    protected int _level;
    protected int _statPoint;
    protected int _watchAdsCount;
    protected int _selectCharacter;
    protected int _selectHead;
    protected int[] _selectWeapons;
    protected int[] _selectCustomEquipments;
    protected int _selectWeaponIndex;
    protected bool _isInvincible;
    protected bool _isGrounded;
    protected int _attackingActionId = -1;
    protected CharacterStats _addStats;
    protected string _extra;
    protected Vector3 _aimPosition;
    protected Quaternion _lookRotation;

    protected virtual int hp
    {
        get { return _hp; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != hp)
            {
                _hp = value;
                photonView.OthersRPC(RpcUpdateHp, value);
            }
        }
    }
    public int Hp
    {
        get { return hp; }
        set
        {
            if (!PhotonNetwork.IsMasterClient)
                return;

            if (value <= 0)
            {
                value = 0;
                if (!isDead)
                {
                    photonView.TargetRPC(RpcTargetDead, photonView.Owner);
                    deathTime = Time.unscaledTime;
                    ++dieCount;
                    isDead = true;
                }
            }
            if (value > TotalHp)
                value = TotalHp;
            hp = value;
        }
    }
    public virtual int armor
    {
        get { return _armor; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != armor)
            {
                _armor = value;
                photonView.OthersRPC(RpcUpdateArmor, value);
            }
        }
    }
    public int Armor
    {
        get { return armor; }
        set
        {
            if (!PhotonNetwork.IsMasterClient)
                return;

            if (value <= 0)
                value = 0;

            if (value > TotalArmor)
                value = TotalArmor;
        }
    }

    protected virtual int boostEnergy
    {
        get { return _boostEnergy; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != boostEnergy)
            {
                _boostEnergy = value;
                photonView.OthersRPC(RpcUpdateBoostEnergy, value);
            }
        }
    }

    public int BoostEnergy
    {
        get { return boostEnergy; }
        set
        {
            if (!PhotonNetwork.IsMasterClient)
                return;

            if (value <= 0)
            {
                value = 0;
            }
            if (value > TotalBoostEnergy)
                value = TotalBoostEnergy;
            boostEnergy = value;
        }
    }
    public virtual int exp
    {
        get { return _exp; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != exp)
            {
                _exp = value;
                photonView.OthersRPC(RpcUpdateExp, value);
            }
        }
    }
    public virtual int Exp
    {
        get { return exp; }
        set
        {
            if (!PhotonNetwork.IsMasterClient)
                return;

            var gameplayManager = GameplayManager.Singleton;
            while (true)
            {
                if (level == gameplayManager.maxLevel)
                    break;

                var currentExp = gameplayManager.GetExp(level);
                if (value < currentExp)
                    break;
                var remainExp = value - currentExp;
                value = remainExp;
                ++level;
                statPoint += gameplayManager.addingStatPoint;
            }
            exp = value;
        }
    }
    public virtual int level
    {
        get { return _level; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != level)
            {
                _level = value;
                photonView.OthersRPC(RpcUpdateLevel, value);
            }
        }
    }
    public virtual int statPoint
    {
        get { return _statPoint; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != statPoint)
            {
                _statPoint = value;
                photonView.OthersRPC(RpcUpdateStatPoint, value);
            }
        }
    }
    public virtual int watchAdsCount
    {
        get { return _watchAdsCount; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != watchAdsCount)
            {
                _watchAdsCount = value;
                photonView.OthersRPC(RpcUpdateWatchAdsCount, value);
            }
        }
    }
    public virtual int selectCharacter
    {
        get { return _selectCharacter; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != selectCharacter)
            {
                _selectCharacter = value;
                photonView.AllRPC(RpcUpdateSelectCharacter, value);
            }
        }
    }
    public virtual int selectHead
    {
        get { return _selectHead; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != selectHead)
            {
                _selectHead = value;
                photonView.AllRPC(RpcUpdateSelectHead, value);
            }
        }
    }
    public virtual int[] selectWeapons
    {
        get { return _selectWeapons; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != selectWeapons)
            {
                _selectWeapons = value;
                photonView.AllRPC(RpcUpdateSelectWeapons, value);
            }
        }
    }
    public virtual int[] selectCustomEquipments
    {
        get { return _selectCustomEquipments; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != selectCustomEquipments)
            {
                _selectCustomEquipments = value;
                photonView.AllRPC(RpcUpdateSelectCustomEquipments, value);
            }
        }
    }
    public virtual int selectWeaponIndex
    {
        get { return _selectWeaponIndex; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != selectWeaponIndex)
            {
                _selectWeaponIndex = value;
                photonView.AllRPC(RpcUpdateSelectWeaponIndex, value);
            }
        }
    }
    public virtual bool isInvincible
    {
        get { return _isInvincible; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != isInvincible)
            {
                _isInvincible = value;
                photonView.OthersRPC(RpcUpdateIsInvincible, value);
            }
        }
    }

    public virtual bool isGrounded
    {
        get { return CacheCharacterMovement.IsGrounded; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != isGrounded)
            {
                _isGrounded = value;
                photonView.OthersRPC(RpcUpdateIsGrounded, value);
            }
        }
    }
    public virtual int attackingActionId
    {
        get { return _attackingActionId; }
        set
        {
            if (photonView.IsMine && value != attackingActionId)
            {
                _attackingActionId = value;
                photonView.OthersRPC(RpcUpdateAttackingActionId, value);
            }
        }
    }
    public virtual CharacterStats addStats
    {
        get { return _addStats; }
        set
        {
            if (PhotonNetwork.IsMasterClient)
            {
                _addStats = value;
                photonView.OthersRPC(RpcUpdateAddStats, value);
            }
        }
    }
    public virtual string extra
    {
        get { return _extra; }
        set
        {
            if (PhotonNetwork.IsMasterClient && value != extra)
            {
                _extra = value;
                photonView.OthersRPC(RpcUpdateExtra, value);
            }
        }
    }
    public virtual Vector3 aimPosition
    {
        get { return _aimPosition; }
        set
        {
            if (photonView.IsMine && value != aimPosition)
            {
                _aimPosition = value;
                photonView.OthersRPC(RpcUpdateAimPosition, value);
            }
        }
    }

    public virtual Quaternion lookRotation
    {
        get { return _lookRotation; }
        set
        {
            if (photonView.IsMine && value != lookRotation)
            {
                _lookRotation = value;
            }
        }
    }
    #endregion

    public override bool IsDead
    {
        get { return hp <= 0; }
    }

    public override bool IsBot
    {
        get { return false; }
    }

    public System.Action onDead;
    public readonly HashSet<PickupEntity> PickableEntities = new HashSet<PickupEntity>();
    public readonly EquippedWeapon[] equippedWeapons = new EquippedWeapon[MAX_EQUIPPABLE_WEAPON_AMOUNT];
    protected ViewMode dirtyViewMode;
    protected Camera targetCamera;
    protected Vector3 cameraForward;
    protected Vector3 cameraRight;
    protected FollowCameraControls followCameraControls;
    protected Coroutine attackRoutine;
    protected Coroutine reloadRoutine;
    protected Coroutine boostConsumeRoutine;
    protected Coroutine boostRecoveryRoutine;
    protected CharacterModel characterModel;
    protected CharacterData characterData;
    protected HeadData headData;
    protected Dictionary<int, CustomEquipmentData> customEquipmentDict = new Dictionary<int, CustomEquipmentData>();
    protected int defaultWeaponIndex = -1;
    protected bool isMobileInput;
    protected Vector3 inputMove;
    protected Vector3 inputDirection;
    protected bool inputAttack;
    protected bool inputJump;
    protected bool inputJumpHolding;
    protected bool inputJumpDoubleClick;
    protected bool isBoosting;
    protected Vector3 dashInputMove;
    protected float dashingTime;
    protected Vector3? previousPosition;
    protected Vector3 currentVelocity;

    public float startReloadTime { get; private set; }
    public float reloadDuration { get; private set; }
    public bool isReady { get; private set; }
    public bool isDead { get; private set; }
    public bool isPlayingAttackAnim { get; private set; }
    public bool isReloading { get; private set; }
    public bool hasAttackInterruptReload { get; private set; }
    public float deathTime { get; private set; }
    public float invincibleTime { get; private set; }
    public bool currentActionIsForLeftHand { get; protected set; }

    public float FinishReloadTimeRate
    {
        get { return (Time.unscaledTime - startReloadTime) / reloadDuration; }
    }

    public EquippedWeapon CurrentEquippedWeapon
    {
        get
        {
            try
            { return equippedWeapons[selectWeaponIndex]; }
            catch
            { return EquippedWeapon.Empty; }
        }
    }

    public WeaponData WeaponData
    {
        get
        {
            try
            { return CurrentEquippedWeapon.WeaponData; }
            catch
            { return null; }
        }
    }

    private bool isHidding;
    private float baseLandedCoolDown;
    private float baseBoostRecoveryRate;
    public float landedPauseMovementDuration;
    private int inputJumpCount;
    private float inputJumpCooler;
    private float inputJumpHoldTimer;
    private bool startBoostRecovering;
    private bool calledBoostRecovery;
    private Transform characterUpperPart;
    private Transform rotatePartTransform;
    private bool canUpperRotate;
    private bool calledBoostConsume = false;

    public bool IsHidding
    {
        get { return isHidding; }
        set
        {
            isHidding = value;
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
                renderer.enabled = !isHidding;
            var canvases = GetComponentsInChildren<Canvas>();
            foreach (var canvas in canvases)
                canvas.enabled = !isHidding;
            var projectors = GetComponentsInChildren<Projector>();
            foreach (var projector in projectors)
                projector.enabled = !isHidding;
        }
    }

    public Transform CacheTransform { get; private set; }
    public Rigidbody CacheRigidbody { get; private set; }
    public CharacterMovement CacheCharacterMovement { get; private set; }

    public CharacterStats SumAddStats
    {
        get
        {
            var stats = new CharacterStats();
            stats += addStats;
            if (headData != null)
                stats += headData.stats;
            if (characterData != null)
                stats += characterData.stats;
            if (WeaponData != null)
                stats += WeaponData.stats;
            if (customEquipmentDict != null)
            {
                foreach (var value in customEquipmentDict.Values)
                    stats += value.stats;
            }
            return stats;
        }
    }

    public int TotalHp
    {
        get
        {
            var total = GameplayManager.Singleton.baseMaxHp + SumAddStats.addMaxHp;
            return total;
        }
    }

    public int TotalArmor
    {
        get
        {
            var total = GameplayManager.Singleton.baseMaxArmor + SumAddStats.addMaxArmor;
            return total;
        }
    }

    public int TotalBoostEnergy
    {
        get
        {
            var total = GameplayManager.Singleton.baseBoostEnergy + SumAddStats.addBoostEnergy;
            return total;
        }
    }

    public int TotalMoveSpeed
    {
        get
        {
            var total = GameplayManager.Singleton.baseMoveSpeed + SumAddStats.addMoveSpeed;
            return total;
        }
    }

    public int TotalRotateSpeed
    {
        get
        {
            var total = GameplayManager.Singleton.baseRotateSpeed + SumAddStats.addRotateSpeed;
            return total;
        }
    }

    public float TotalWeaponDamageRate
    {
        get
        {
            var total = GameplayManager.Singleton.baseWeaponDamageRate + SumAddStats.addWeaponDamageRate;

            var maxValue = GameplayManager.Singleton.maxWeaponDamageRate;
            if (total < maxValue)
                return total;
            else
                return maxValue;
        }
    }

    public float TotalReduceDamageRate
    {
        get
        {
            var total = GameplayManager.Singleton.baseReduceDamageRate + SumAddStats.addReduceDamageRate;

            var maxValue = GameplayManager.Singleton.maxReduceDamageRate;
            if (total < maxValue)
                return total;
            else
                return maxValue;
        }
    }

    public float TotalArmorReduceDamage
    {
        get
        {
            var total = GameplayManager.Singleton.baseArmorReduceDamage + SumAddStats.addArmorReduceDamage;

            var maxValue = GameplayManager.Singleton.maxArmorReduceDamage;
            if (total < maxValue)
                return total;
            else
                return maxValue;
        }
    }

    public float TotalExpRate
    {
        get
        {
            var total = 1 + SumAddStats.addExpRate;
            return total;
        }
    }

    public float TotalScoreRate
    {
        get
        {
            var total = 1 + SumAddStats.addScoreRate;
            return total;
        }
    }

    public float TotalHpRecoveryRate
    {
        get
        {
            var total = 1 + SumAddStats.addHpRecoveryRate;
            return total;
        }
    }

    public float TotalArmorRecoveryRate
    {
        get
        {
            var total = 1 + SumAddStats.addArmorRecoveryRate;
            return total;
        }
    }
    public float BoostRecoveryPerSecond
    {
        get
        {
            var total = 1 + SumAddStats.addBoostEnergyRecoveryRate;
            return total;
        }
    }

    public float TotalDamageRateLeechHp
    {
        get
        {
            var total = SumAddStats.addDamageRateLeechHp;
            return total;
        }
    }

    public object CacheCollider { get; internal set; }

    protected override void Init()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        base.Init();
        hp = 0;
        armor = 0;
        boostEnergy = 0;
        exp = 0;
        level = 1;
        statPoint = 0;
        watchAdsCount = 0;
        selectCharacter = 0;
        selectHead = 0;
        selectWeapons = new int[0];
        selectCustomEquipments = new int[0];
        selectWeaponIndex = -1;
        isInvincible = false;
        addStats = new CharacterStats();
        extra = "";
    }

    protected override void Awake()
    {
        base.Awake();
        gameObject.layer = GameInstance.Singleton.characterLayer;
        CacheTransform = transform;
        CacheRigidbody = gameObject.GetOrAddComponent<Rigidbody>();
        CacheRigidbody.useGravity = false;
        CacheCharacterMovement = gameObject.GetOrAddComponent<CharacterMovement>();
        if (damageLaunchTransform == null)
            damageLaunchTransform = CacheTransform;
        if (effectTransform == null)
            effectTransform = CacheTransform;
        if (characterModelTransform == null)
            characterModelTransform = CacheTransform;
        foreach (var localPlayerObject in localPlayerObjects)
        {
            localPlayerObject.SetActive(false);
        }
        deathTime = Time.unscaledTime;
    }

    protected override void SyncData()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        base.SyncData();
        photonView.OthersRPC(RpcUpdateHp, hp);
        photonView.OthersRPC(RpcUpdateArmor, armor);
        photonView.OthersRPC(RpcUpdateBoostEnergy, TotalBoostEnergy);
        photonView.OthersRPC(RpcUpdateStatPoint, statPoint);
        photonView.OthersRPC(RpcUpdateSelectCharacter, selectCharacter);
        photonView.OthersRPC(RpcUpdateSelectWeapons, selectWeapons);
        photonView.OthersRPC(RpcUpdateSelectWeaponIndex, selectWeaponIndex);
        photonView.OthersRPC(RpcUpdateIsInvincible, isInvincible);
        photonView.OthersRPC(RpcUpdateAttackingActionId, attackingActionId);
        photonView.OthersRPC(RpcUpdateAddStats, addStats);
        photonView.OthersRPC(RpcUpdateAimPosition, aimPosition);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        base.OnPlayerEnteredRoom(newPlayer);
        photonView.TargetRPC(RpcUpdateHp, newPlayer, hp);
        photonView.TargetRPC(RpcUpdateArmor, newPlayer, armor);
        photonView.TargetRPC(RpcUpdateBoostEnergy, newPlayer, TotalBoostEnergy);
        photonView.TargetRPC(RpcUpdateStatPoint, newPlayer, statPoint);
        photonView.TargetRPC(RpcUpdateSelectCharacter, newPlayer, selectCharacter);
        photonView.TargetRPC(RpcUpdateSelectWeapons, newPlayer, selectWeapons);
        photonView.TargetRPC(RpcUpdateSelectWeaponIndex, newPlayer, selectWeaponIndex);
        photonView.TargetRPC(RpcUpdateIsInvincible, newPlayer, isInvincible);
        photonView.TargetRPC(RpcUpdateAttackingActionId, newPlayer, attackingActionId);
        photonView.TargetRPC(RpcUpdateAddStats, newPlayer, addStats);
        photonView.TargetRPC(RpcUpdateAimPosition, newPlayer, aimPosition);
    }

    protected override void OnStartLocalPlayer()
    {
        if (photonView.IsMine)
        {
            followCameraControls = FindObjectOfType<FollowCameraControls>();
            followCameraControls.target = CacheTransform;
            targetCamera = followCameraControls.CacheCamera;

            foreach (var localPlayerObject in localPlayerObjects)
            {
                localPlayerObject.SetActive(true);
            }

            StartCoroutine(DelayReady());
        }
    }

    IEnumerator DelayReady()
    {
        yield return new WaitForSeconds(0.5f);
        // Add some delay before ready to make sure that it can receive team and game rule
        // TODO: Should improve this (Or remake team system, one which made by Photon is not work well)
        var uiGameplay = FindObjectOfType<UIGameplay>();
        if (uiGameplay != null)
            uiGameplay.FadeOut();
        CmdReady();
        if (nameText != null)
            nameText.text = playerName;
    }

    protected override void Update()
    {
        base.Update();
        if (NetworkManager != null && NetworkManager.IsMatchEnded)
            return;

        if (Hp <= 0)
        {
            //if (!PhotonNetwork.IsMasterClient && photonView.IsMine && Time.unscaledTime - deathTime >= DISCONNECT_WHEN_NOT_RESPAWN_DURATION)
            //    GameNetworkManager.Singleton.LeaveRoom();

            if (photonView.IsMine)
                attackingActionId = -1;
        }

        if (PhotonNetwork.IsMasterClient && isInvincible && Time.unscaledTime - invincibleTime >= GameplayManager.Singleton.invincibleDuration)
            isInvincible = false;
        if (invincibleEffect != null)
            invincibleEffect.SetActive(isInvincible);
        if (nameText != null)
            nameText.text = playerName;
        if (hpBarContainer != null)
            hpBarContainer.gameObject.SetActive(hp > 0);
        if (hpFillImage != null)
            hpFillImage.fillAmount = (float)hp / (float)TotalHp;
        if (hpText != null)
            hpText.text = hp + "/" + TotalHp;

        UpdateViewMode();
        UpdateAimPosition();
        UpdateAnimation();
        UpdateInput();

        // Update attack signal
        if (attackSignalObject != null)
            attackSignalObject.SetActive(isPlayingAttackAnim);
        // update can boost recover
        BoostRecovery();

        // TODO: Improve team codes
        if (attackSignalObjectForTeamA != null)
            attackSignalObjectForTeamA.SetActive(isPlayingAttackAnim && playerTeam == 1);
        if (attackSignalObjectForTeamB != null)
            attackSignalObjectForTeamB.SetActive(isPlayingAttackAnim && playerTeam == 2);

        if (!previousPosition.HasValue)
            previousPosition = CacheTransform.position;
        var currentMove = CacheTransform.position - previousPosition.Value;
        currentVelocity = currentMove / Time.deltaTime;
        previousPosition = CacheTransform.position;
        UpdateMovements();
    }

    private void BoostRecovery()
    {
        if (!photonView.IsMine)
            return;

        startBoostRecovering = CheckBoostRecoverCondition();

        if (startBoostRecovering && !calledBoostRecovery)
        {
            calledBoostRecovery = true;
            CmdBoostRecovery();
        }
    }

    private bool CheckBoostRecoverCondition()
    {
        return isGrounded && BoostEnergy < TotalBoostEnergy && !inputJump && !inputJumpHolding && !inputJumpDoubleClick;
    }

    private void FixedUpdate()
    {
        if (NetworkManager != null && NetworkManager.IsMatchEnded)
            return;
    }

    protected virtual void UpdateInputDirection_TopDown(bool canAttack)
    {
        if (viewMode != ViewMode.TopDown)
            return;
        doNotLockCursor = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        followCameraControls.updateRotation = false;
        followCameraControls.updateZoom = true;
        if (isMobileInput)
        {
            inputDirection = Vector3.zero;
            inputDirection += InputManager.GetAxis("Mouse Y", false) * cameraForward;
            inputDirection += InputManager.GetAxis("Mouse X", false) * cameraRight;
            if (canAttack)
                inputAttack = inputDirection.magnitude != 0;
        }
        else
        {
            inputDirection = (InputManager.MousePosition() - targetCamera.WorldToScreenPoint(CacheTransform.position)).normalized;
            inputDirection = new Vector3(inputDirection.x, 0, inputDirection.y);
            if (canAttack)
                inputAttack = InputManager.GetButton("Fire1");
        }
    }

    protected virtual void UpdateInputDirection_ThirdPerson(bool canAttack)
    {
        if (viewMode != ViewMode.ThirdPerson)
            return;
        if (isMobileInput || doNotLockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (isMobileInput)
        {
            followCameraControls.updateRotation = InputManager.GetButton("CameraRotate");
            followCameraControls.updateZoom = true;
            inputDirection = Vector3.zero;
            inputDirection += InputManager.GetAxis("Mouse Y", false) * cameraForward;
            inputDirection += InputManager.GetAxis("Mouse X", false) * cameraRight;
            if (canAttack)
            {
                inputAttack = InputManager.GetButton("Fire1");
            }
        }
        else
        {
            followCameraControls.updateRotation = true;
            followCameraControls.updateZoom = true;
            inputDirection = (InputManager.MousePosition() - targetCamera.WorldToScreenPoint(CacheTransform.position)).normalized;
            inputDirection = new Vector3(inputDirection.x, 0, inputDirection.y);
            if (canAttack)
            {
                inputAttack = InputManager.GetButton("Fire1");
            }
        }
    }

    protected virtual bool CheckIfAngleAllowToRotateUpper(Vector3 aimDirection)
    {
        return Vector3.Angle(CacheTransform.forward, aimDirection) < maxUpperHAngle / 2;
    }

    protected virtual void UpdateViewMode(bool force = false)
    {
        if (!photonView.IsMine)
            return;

        if (force || dirtyViewMode != viewMode)
        {
            dirtyViewMode = viewMode;
            ViewModeSettings settings = viewMode == ViewMode.ThirdPerson ? thirdPersionViewModeSettings : topDownViewModeSettings;
            followCameraControls.limitXRotation = true;
            followCameraControls.limitYRotation = false;
            followCameraControls.limitZoomDistance = true;
            followCameraControls.targetOffset = settings.targetOffsets;
            followCameraControls.zoomDistance = settings.zoomDistance;
            followCameraControls.minZoomDistance = settings.minZoomDistance;
            followCameraControls.maxZoomDistance = settings.maxZoomDistance;
            followCameraControls.xRotation = settings.xRotation;
            followCameraControls.minXRotation = settings.minXRotation;
            followCameraControls.maxXRotation = settings.maxXRotation;
            followCameraControls.yRotation = settings.yRotation;
            targetCamera.fieldOfView = settings.fov;
            targetCamera.nearClipPlane = settings.nearClipPlane;
            targetCamera.farClipPlane = settings.farClipPlane;
        }
    }

    protected virtual void UpdateAimPosition()
    {
        if (!photonView.IsMine || !characterModel)
            return;

        float attackDist = 999f;
        switch (viewMode)
        {
            case ViewMode.TopDown:
                // Update aim position
                currentActionIsForLeftHand = CurrentActionIsForLeftHand();
                Transform launchTransform;
                GetDamageLaunchTransform(currentActionIsForLeftHand, out launchTransform);
                aimPosition = launchTransform.position + (CacheTransform.forward * attackDist);
                break;
            case ViewMode.ThirdPerson:
                float distanceToCharacter = Vector3.Distance(CacheTransform.position, followCameraControls.CacheCameraTransform.position);
                float distanceToTarget = attackDist;
                Vector3 lookAtCharacterPosition = targetCamera.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, distanceToCharacter));
                Vector3 lookAtTargetPosition = targetCamera.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, distanceToTarget));
                aimPosition = lookAtTargetPosition;
                if (characterUpperPart != null)
                {
                    canUpperRotate = CheckIfAngleAllowToRotateUpper(aimPosition);
                    if (canUpperRotate)
                    {
                        var limitY = Mathf.Clamp(aimPosition.y, -maxUpperVAngle, maxUpperVAngle);
                        lookRotation = Quaternion.LookRotation(new Vector3(aimPosition.x, limitY, aimPosition.z));
                    }
                    else
                    {
                        lookRotation = Quaternion.LookRotation(CacheTransform.forward);
                    }
                    rotatePartTransform = characterUpperPart;
                }
                else
                {
                    lookRotation = Quaternion.LookRotation(CacheTransform.forward);
                    rotatePartTransform = CacheTransform;
                }

                rotatePartTransform.rotation = Quaternion.RotateTowards(rotatePartTransform.rotation, lookRotation, upperRotateSpeed * Time.unscaledTime);
                RaycastHit[] hits = Physics.RaycastAll(lookAtCharacterPosition, (lookAtTargetPosition - lookAtCharacterPosition).normalized, attackDist);
                for (int i = 0; i < hits.Length; ++i)
                {
                    if (hits[i].transform.root != transform.root)
                        aimPosition = hits[i].point;
                }
                break;
        }
    }

    protected virtual void UpdateAnimation()
    {
        if (characterModel == null)
            return;
        var animator = characterModel.TempAnimator;
        if (animator == null)
            return;
        if (Hp <= 0)
        {
            animator.SetBool("IsDead", true);
            animator.SetFloat("JumpSpeed", 0);
            animator.SetFloat("MoveSpeed", 0);
            animator.SetBool("IsGround", true);
            animator.SetBool("IsDash", false);
            animator.SetBool("IsBoost", false);
            animator.SetBool("IsLanding", false);
            animator.SetBool("IsLanded", false);
        }
        else
        {
            var velocity = currentVelocity;
            var xzMagnitude = new Vector3(velocity.x, 0, velocity.z).magnitude;
            var ySpeed = velocity.y;
            bool isBoost = ySpeed > 0.1;
            bool isLanding = ySpeed < -0.1;
            bool isLanded = ySpeed < 0.1 && isGrounded;
            animator.SetBool("IsDead", false);
            animator.SetFloat("JumpSpeed", ySpeed);
            animator.SetFloat("MoveSpeed", xzMagnitude);
            animator.SetBool("IsGround", isGrounded);
            animator.SetBool("IsDash", isBoosting);
            animator.SetBool("IsBoost", isBoost);
            animator.SetBool("IsLanding", isLanding);
            animator.SetBool("IsLanded", isLanded);
        }

        if (WeaponData != null)
            animator.SetInteger("WeaponAnimId", WeaponData.weaponAnimId);

        animator.SetBool("IsIdle", !animator.GetBool("IsDead") && !animator.GetBool("DoAction") && animator.GetBool("IsGround") && inputDirection == Vector3.zero);

        if (attackingActionId >= 0 && !isPlayingAttackAnim)
            StartCoroutine(AttackRoutine(attackingActionId));
    }

    protected virtual void UpdateInput()
    {
        if (!photonView.IsMine)
            return;

        bool canControl = true;
        var fields = FindObjectsOfType<InputField>();
        foreach (var field in fields)
        {
            if (field.isFocused)
            {
                canControl = false;
                break;
            }
        }

        isMobileInput = Application.isMobilePlatform;
#if UNITY_EDITOR
        isMobileInput = GameInstance.Singleton.showJoystickInEditor;
#endif
        InputManager.useMobileInputOnNonMobile = isMobileInput;

        var canAttack = isMobileInput || !EventSystem.current.IsPointerOverGameObject();
        inputMove = Vector3.zero;
        inputDirection = Vector3.zero;
        inputAttack = false;
        if (canControl)
        {
            cameraForward = followCameraControls.CacheCameraTransform.forward;
            cameraForward.y = 0;
            cameraForward = cameraForward.normalized;
            cameraRight = followCameraControls.CacheCameraTransform.right;
            cameraRight.y = 0;
            cameraRight = cameraRight.normalized;
            inputMove = Vector3.zero;
            if (IsDead)
                return;

            inputMove += cameraForward * InputManager.GetAxis("Vertical", false);
            inputMove += cameraRight * InputManager.GetAxis("Horizontal", false);

            // Jump
            InputJump();
            InputBoost();
            UpdateInputDirection_TopDown(canAttack);
            UpdateInputDirection_ThirdPerson(canAttack);
            if (!IsDead)
            {
                if (InputManager.GetButtonDown("Reload"))
                    Reload();
                if (GameplayManager.Singleton.autoReload &&
                    CurrentEquippedWeapon.currentAmmo == 0 &&
                    CurrentEquippedWeapon.CanReload())
                    Reload();
            }
        }
    }

    private void InputBoost()
    {
        isBoosting = InputManager.GetButton("Dash") && BoostEnergy >= boostActionCost;
        if (isBoosting)
        {
            if(isGrounded)
                isGrounded = false;
            ExcuteBoostCost(boostActionCost);
        }
    }

    private void InputJump()
    {
        inputJump = InputManager.GetButtonDown("Jump") && BoostEnergy >= baseJumpEnergyCost;
        if (inputJump)
        {
            inputJumpCount += 1;
            inputJumpCooler = 0.3f;
            inputJumpHoldTimer = 0.5f;
            ExcuteBoostCost(baseJumpEnergyCost);
            if (inputJumpCooler > 0 && inputJumpCount == 2)
            {
                inputJumpDoubleClick = true;
                inputJumpHolding = false;
            }
        }
        else if (InputManager.GetButton("Jump") && inputJumpHoldTimer <= 0f && !inputJumpDoubleClick && !isGrounded && BoostEnergy >= baseJumpHoldEnergyCost)
        {
            inputJumpHolding = true;
            inputJumpDoubleClick = false;
            ExcuteBoostCost(baseJumpHoldEnergyCost);
        }

        if (inputJumpCooler > 0)
            inputJumpCooler -= 1 * Time.deltaTime;
        else
            inputJumpCount = 0;

        if (inputJumpHoldTimer > 0)
            inputJumpHoldTimer -= 1 * Time.deltaTime;

        if (InputManager.GetButtonUp("Jump") || BoostEnergy < baseJumpEnergyCost || BoostEnergy < baseJumpHoldEnergyCost)
        {
            inputJump = false;
            inputJumpHolding = false;
            if (inputJumpDoubleClick)
                inputJumpDoubleClick = false;
            StopCoroutine(boostConsumeRoutine);
        }
    }

    protected virtual float GetMoveSpeed()
    {
        return TotalMoveSpeed * GameplayManager.REAL_MOVE_SPEED_RATE;
    }

    protected virtual float GetRotateSpeed()
    {
        return TotalRotateSpeed * GameplayManager.REAL_MOVE_SPEED_RATE;
    }
    protected virtual bool CurrentActionIsForLeftHand()
    {
        if (attackingActionId >= 0)
        {
            AttackAnimation attackAnimation;
            if (WeaponData.AttackAnimations.TryGetValue(attackingActionId, out attackAnimation))
                return attackAnimation.isAnimationForLeftHandWeapon;
        }
        return false;
    }

    protected virtual void Move(Vector3 direction)
    {
        if (direction.sqrMagnitude > 1)
            direction = direction.normalized;
        direction.y = 0;

        float moveSpeed = isBoosting ? GetMoveSpeed() * baseBoostSpeed : GetMoveSpeed();
        CacheCharacterMovement.UpdateMovement(Time.deltaTime, moveSpeed, direction, inputJump, inputJumpHolding, isBoosting);
    }

    public void ExcuteBoostCost(int cost)
    {
        if (!calledBoostConsume && photonView.IsMine)
        {
            calledBoostConsume = true;
            boostConsumeRoutine = StartCoroutine(BoostConsumeRoutine(cost));
        }
    }

    IEnumerator BoostConsumeRoutine(int cost)
    {
        while (inputJump && BoostEnergy > 0 || inputJumpHolding && BoostEnergy > 0 || isBoosting)
        {
            BoostEnergy -= cost;
            yield return new WaitForSeconds(0.1f);
        }
        photonView.OthersRPC(RpcUpdateBoostEnergy, BoostEnergy);
        calledBoostConsume = false;
    }

    protected virtual void UpdateMovements()
    {
        if (!photonView.IsMine)
            return;

        var moveDirection = Vector3.zero;
        moveDirection = inputMove;

        if (isBoosting) 
        {
            if (isMobileInput)
                dashInputMove = inputMove.normalized;
            else
                dashInputMove = new Vector3(CacheTransform.forward.x, 0f, CacheTransform.forward.z).normalized;
            moveDirection = dashInputMove + (inputMove / 3f);
        }

        Move(moveDirection);
        // Turn character to move direction
        if (inputDirection.magnitude <= 0 && inputMove.magnitude > 0 || viewMode == ViewMode.ThirdPerson)
            inputDirection = inputMove;
        if (characterModel && characterModel.TempAnimator && characterModel.TempAnimator.GetBool("DoAction") && viewMode == ViewMode.ThirdPerson)
            inputDirection = cameraForward;
        if (!IsDead)
            Rotate(moveDirection);

        if (!IsDead)
        {
            if (inputAttack && GameplayManager.Singleton.CanAttack(this))
                Attack();
            else
                StopAttack();
        }
    }

    protected void Rotate(Vector3 direction)
    {
        if (direction.sqrMagnitude != 0 && currentVelocity.y <= 0)
        {
            var rotation = Quaternion.LookRotation(direction);
            CacheTransform.rotation = Quaternion.Slerp(CacheTransform.rotation, rotation, Time.deltaTime * GetRotateSpeed());
        }
    }

    public void GetDamageLaunchTransform(bool isLeftHandWeapon, out Transform launchTransform)
    {
        if (characterModel == null || !characterModel.TryGetDamageLaunchTransform(isLeftHandWeapon, out launchTransform))
            launchTransform = damageLaunchTransform;
    }

    protected void Attack()
    {
        if (photonView.IsMine)
        {
            // If attacking while reloading, determines that it is reload interrupting
            if (isReloading && FinishReloadTimeRate > 0.8f)
                hasAttackInterruptReload = true;
        }
        if (isPlayingAttackAnim || isReloading || !CurrentEquippedWeapon.CanShoot())
            return;

        if (attackingActionId < 0 && photonView.IsMine)
        {
            if (WeaponData != null)
                attackingActionId = WeaponData.GetRandomAttackAnimation().actionId;
            else
                attackingActionId = -1;
        }
    }

    protected void StopAttack()
    {
        if (attackingActionId >= 0 && photonView.IsMine)
            attackingActionId = -1;
    }

    protected void Reload()
    {
        if (isPlayingAttackAnim || isReloading || !CurrentEquippedWeapon.CanReload())
            return;
        if (photonView.IsMine)
            CmdReload();
    }

    IEnumerator AttackRoutine(int actionId)
    {
        if (!isPlayingAttackAnim &&
            !isReloading &&
            CurrentEquippedWeapon.CanShoot() &&
            Hp > 0 &&
            characterModel != null &&
            characterModel.TempAnimator != null)
        {
            isPlayingAttackAnim = true;
            var animator = characterModel.TempAnimator;
            AttackAnimation attackAnimation;
            if (WeaponData != null &&
                WeaponData.AttackAnimations.TryGetValue(actionId, out attackAnimation))
            {
                // Play attack animation
                animator.SetBool("DoAction", false);
                yield return new WaitForEndOfFrame();
                animator.SetBool("DoAction", true);
                animator.SetInteger("ActionID", attackAnimation.actionId);

                // Wait to launch damage entity
                var speed = attackAnimation.speed;
                var animationDuration = attackAnimation.animationDuration;
                var launchDuration = attackAnimation.launchDuration;
                if (launchDuration > animationDuration)
                    launchDuration = animationDuration;
                yield return new WaitForSeconds(launchDuration / speed);

                WeaponData.Launch(this, attackAnimation.isAnimationForLeftHandWeapon, aimPosition);
                // Manage ammo at master client
                if (PhotonNetwork.IsMasterClient)
                {
                    var equippedWeapon = CurrentEquippedWeapon;
                    equippedWeapon.DecreaseAmmo();
                    equippedWeapons[selectWeaponIndex] = equippedWeapon;
                    photonView.AllRPC(RpcUpdateEquippedWeaponsAmmo, selectWeaponIndex, equippedWeapon.currentAmmo, equippedWeapon.currentReserveAmmo);
                }

                // Random play shoot sounds
                if (WeaponData.attackFx != null && WeaponData.attackFx.Length > 0 && AudioManager.Singleton != null)
                    AudioSource.PlayClipAtPoint(WeaponData.attackFx[Random.Range(0, WeaponData.attackFx.Length - 1)], CacheTransform.position, AudioManager.Singleton.sfxVolumeSetting.Level);

                // Wait till animation end
                yield return new WaitForSeconds((animationDuration - launchDuration) / speed);
            }
            // If player still attacking, random new attacking action id
            if (PhotonNetwork.IsMasterClient && attackingActionId >= 0 && WeaponData != null)
                attackingActionId = WeaponData.GetRandomAttackAnimation().actionId;
            yield return new WaitForEndOfFrame();

            // Attack animation ended
            animator.SetBool("DoAction", false);
            isPlayingAttackAnim = false;
        }
    }

    IEnumerator BoostRecoveryRoutine()
    {
        yield return new WaitForSeconds(baseLandedCoolDown);
        while (startBoostRecovering && !IsDead && isGrounded && !isBoosting)
        {
            BoostEnergy += (int)BoostRecoveryPerSecond;
            if (BoostEnergy > TotalBoostEnergy)
                BoostEnergy = TotalBoostEnergy;
            yield return new WaitForSeconds(baseBoostRecoveryRate);
        }
        photonView.OthersRPC(RpcUpdateBoostEnergy, BoostEnergy);
        calledBoostRecovery = false;
    }

    IEnumerator ReloadRoutine()
    {
        hasAttackInterruptReload = false;
        if (!isReloading && CurrentEquippedWeapon.CanReload())
        {
            isReloading = true;
            if (WeaponData != null)
            {
                reloadDuration = WeaponData.reloadDuration;
                startReloadTime = Time.unscaledTime;
                if (WeaponData.clipOutFx != null && AudioManager.Singleton != null)
                    AudioSource.PlayClipAtPoint(WeaponData.clipOutFx, CacheTransform.position, AudioManager.Singleton.sfxVolumeSetting.Level);
                yield return new WaitForSeconds(reloadDuration);
                if (PhotonNetwork.IsMasterClient)
                {
                    var equippedWeapon = CurrentEquippedWeapon;
                    equippedWeapon.Reload();
                    equippedWeapons[selectWeaponIndex] = equippedWeapon;
                    photonView.AllRPC(RpcUpdateEquippedWeaponsAmmo, selectWeaponIndex, equippedWeapon.currentAmmo, equippedWeapon.currentReserveAmmo);
                }
                if (WeaponData.clipInFx != null && AudioManager.Singleton != null)
                    AudioSource.PlayClipAtPoint(WeaponData.clipInFx, CacheTransform.position, AudioManager.Singleton.sfxVolumeSetting.Level);
            }
            // If player still attacking, random new attacking action id
            if (PhotonNetwork.IsMasterClient && attackingActionId >= 0 && WeaponData != null)
                attackingActionId = WeaponData.GetRandomAttackAnimation().actionId;
            yield return new WaitForEndOfFrame();
            isReloading = false;
            if (photonView.IsMine)
            {
                // If weapon is reload one ammo at a time (like as shotgun), automatically reload more bullets
                // When there is no attack interrupt while reload
                if (WeaponData != null && WeaponData.reloadOneAmmoAtATime && CurrentEquippedWeapon.CanReload())
                {
                    if (!hasAttackInterruptReload)
                        Reload();
                    else
                        Attack();
                }
            }
        }
    }

    public virtual bool ReceiveDamage(CharacterEntity attacker, int damage)
    {
        if (Hp <= 0 || isInvincible)
            return false;

        if (!GameplayManager.Singleton.CanReceiveDamage(this, attacker))
            return false;

        int reduceHp = damage;
        reduceHp -= Mathf.CeilToInt(damage * TotalReduceDamageRate);
        if (Armor > 0)
        {
            if (Armor - damage >= 0)
            {
                // Armor absorb damage
                reduceHp -= Mathf.CeilToInt(damage * TotalArmorReduceDamage);
                Armor -= damage;
            }
            else
            {
                // Armor remaining less than 0, Reduce HP by remain damage without armor absorb
                // Armor absorb damage
                reduceHp -= Mathf.CeilToInt(Armor * TotalArmorReduceDamage);
                // Remain damage after armor broke
                reduceHp -= Mathf.Abs(Armor - damage);
                Armor = 0;
            }
        }
        // Avoid increasing hp by damage
        if (reduceHp < 0)
            reduceHp = 0;

        Hp -= reduceHp;
        if (attacker != null)
        {
            var leechHpAmount = Mathf.CeilToInt(attacker.TotalDamageRateLeechHp * reduceHp);
            attacker.Hp += leechHpAmount;
            if (Hp == 0)
            {
                if (onDead != null)
                    onDead.Invoke();
                InterruptAttack();
                InterruptReload();
                photonView.OthersRPC(RpcInterruptAttack);
                photonView.OthersRPC(RpcInterruptReload);
                attacker.KilledTarget(this);
                ++dieCount;
            }
        }
        return true;
    }

    public void KilledTarget(CharacterEntity target)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        var gameplayManager = GameplayManager.Singleton;
        var targetLevel = target.level;
        var maxLevel = gameplayManager.maxLevel;
        Exp += Mathf.CeilToInt(gameplayManager.GetRewardExp(targetLevel) * TotalExpRate);
        score += Mathf.CeilToInt(gameplayManager.GetKillScore(targetLevel) * TotalScoreRate);
        foreach (var rewardCurrency in gameplayManager.rewardCurrencies)
        {
            var currencyId = rewardCurrency.currencyId;
            var amount = rewardCurrency.amount.Calculate(targetLevel, maxLevel);
            photonView.TargetRPC(RpcTargetRewardCurrency, photonView.Owner, currencyId, amount);
        }
        ++killCount;
        GameNetworkManager.Singleton.SendKillNotify(playerName, target.playerName, WeaponData == null ? string.Empty : WeaponData.GetId());
    }

    public void Heal(int amount)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (Hp <= 0)
            return;

        Hp += amount;
    }

    public virtual float GetAttackRange()
    {
        if (WeaponData == null || WeaponData.damagePrefab == null)
            return 0;
        return WeaponData.damagePrefab.GetAttackRange();
    }

    public virtual Vector3 GetSpawnPosition()
    {
        return GameplayManager.Singleton.GetCharacterSpawnPosition(this);
    }

    public void UpdateCharacterModelHiddingState()
    {
        if (characterModel == null)
            return;
        var renderers = characterModel.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
            renderer.enabled = !IsHidding;
    }

    protected void InterruptAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            isPlayingAttackAnim = false;
        }
    }

    protected void InterruptReload()
    {
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            isReloading = false;
        }
    }

    public virtual void OnSpawn() { }

    public void ServerInvincible()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        invincibleTime = Time.unscaledTime;
        isInvincible = true;
    }

    public void ServerSpawn(bool isWatchedAds)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        if (Respawn(isWatchedAds))
        {
            ServerInvincible();
            OnSpawn();
            var position = GetSpawnPosition();
            CacheTransform.position = position;
            photonView.TargetRPC(RpcTargetSpawn, photonView.Owner, position.x, position.y, position.z);
            ServerRevive();
        }
    }

    public void ServerRespawn(bool isWatchedAds)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        if (CanRespawn(isWatchedAds))
            ServerSpawn(isWatchedAds);
    }

    public void ServerRevive()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        for (var i = 0; i < equippedWeapons.Length; ++i)
        {
            var equippedWeapon = equippedWeapons[i];
            equippedWeapon.ChangeWeaponId(equippedWeapon.defaultId, 0);
            equippedWeapon.SetMaxAmmo();
            equippedWeapons[i] = equippedWeapon;
            photonView.AllRPC(RpcUpdateEquippedWeapons, i, equippedWeapon.defaultId, equippedWeapon.weaponId, equippedWeapon.currentAmmo, equippedWeapon.currentReserveAmmo);
        }
        selectWeaponIndex = defaultWeaponIndex;

        isPlayingAttackAnim = false;
        isReloading = false;
        isDead = false;
        Hp = TotalHp;
        BoostEnergy = TotalBoostEnergy;
    }

    public void ServerReload()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        if (WeaponData != null)
        {
            reloadRoutine = StartCoroutine(ReloadRoutine());
            photonView.OthersRPC(RpcReload);
        }
    }
    public void ServerBoostRecovery()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        boostRecoveryRoutine = StartCoroutine(BoostRecoveryRoutine());
        photonView.OthersRPC(RpcBoostRecovery);
    }

    public void ServerChangeWeapon(int index)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        var gameInstance = GameInstance.Singleton;
        if (index >= 0 && index < MAX_EQUIPPABLE_WEAPON_AMOUNT && !equippedWeapons[index].IsEmpty())
        {
            selectWeaponIndex = index;
            InterruptAttack();
            InterruptReload();
            photonView.OthersRPC(RpcInterruptAttack);
            photonView.OthersRPC(RpcInterruptReload);
        }
    }

    public bool ServerChangeSelectWeapon(WeaponData weaponData, int ammoAmount)
    {
        if (!PhotonNetwork.IsMasterClient)
            return false;
        if (weaponData == null || weaponData.equipPosition < 0 || weaponData.equipPosition >= equippedWeapons.Length)
            return false;
        var equipPosition = weaponData.equipPosition;
        var equippedWeapon = equippedWeapons[equipPosition];
        var updated = equippedWeapon.ChangeWeaponId(weaponData.GetHashId(), ammoAmount);
        if (updated)
        {
            InterruptAttack();
            InterruptReload();
            photonView.OthersRPC(RpcInterruptAttack);
            photonView.OthersRPC(RpcInterruptReload);
            equippedWeapons[equipPosition] = equippedWeapon;
            if (selectWeaponIndex == equipPosition)
                RpcUpdateSelectWeaponIndex(selectWeaponIndex);
            photonView.AllRPC(RpcUpdateEquippedWeapons, equipPosition, equippedWeapon.defaultId, equippedWeapon.weaponId, equippedWeapon.currentAmmo, equippedWeapon.currentReserveAmmo);
        }
        return updated;
    }

    public bool ServerFillWeaponAmmo(WeaponData weaponData, int ammoAmount)
    {
        if (!PhotonNetwork.IsMasterClient)
            return false;
        if (weaponData == null || weaponData.equipPosition < 0 || weaponData.equipPosition >= equippedWeapons.Length)
            return false;
        var equipPosition = weaponData.equipPosition;
        var equippedWeapon = equippedWeapons[equipPosition];
        var updated = false;
        if (equippedWeapon.weaponId == weaponData.GetHashId())
        {
            updated = equippedWeapon.AddReserveAmmo(ammoAmount);
            if (updated)
            {
                equippedWeapons[equipPosition] = equippedWeapon;
                photonView.AllRPC(RpcUpdateEquippedWeaponsAmmo, equipPosition, equippedWeapon.currentAmmo, equippedWeapon.currentReserveAmmo);
            }
        }
        return updated;
    }

    public void CmdInit(int selectHead, int selectCharacter, int[] selectWeapons, int[] selectCustomEquipments, string extra)
    {
        photonView.MasterRPC(RpcServerInit, selectHead, selectCharacter, selectWeapons, selectCustomEquipments, extra);
    }

    [PunRPC]
    protected void RpcServerInit(int selectHead, int selectCharacter, int[] selectWeapons, int[] selectCustomEquipments, string extra)
    {
        var alreadyInit = false;
        var networkManager = BaseNetworkGameManager.Singleton;
        if (networkManager != null)
        {
            networkManager.RegisterCharacter(this);
            var gameRule = networkManager.gameRule;
            if (gameRule != null && gameRule is IONetworkGameRule)
            {
                var ioGameRule = gameRule as IONetworkGameRule;
                ioGameRule.NewPlayer(this, selectHead, selectCharacter, selectWeapons, selectCustomEquipments, extra);
                alreadyInit = true;
            }
        }
        if (!alreadyInit)
        {
            this.selectHead = selectHead;
            this.selectCharacter = selectCharacter;
            this.selectWeapons = selectWeapons;
            this.selectCustomEquipments = selectCustomEquipments;
            this.extra = extra;
        }
        Hp = TotalHp;
        BoostEnergy = TotalBoostEnergy;
    }

    public void CmdReady()
    {
        photonView.MasterRPC(RpcServerReady);
    }

    [PunRPC]
    protected void RpcServerReady()
    {
        if (!isReady)
        {
            ServerSpawn(false);
            isReady = true;
        }
    }

    public void CmdRespawn(bool isWatchedAds)
    {
        photonView.MasterRPC(RpcServerRespawn, isWatchedAds);
    }

    [PunRPC]
    protected void RpcServerRespawn(bool isWatchedAds)
    {
        ServerRespawn(isWatchedAds);
    }

    public void CmdReload()
    {
        photonView.MasterRPC(RpcServerReload);
    }

    [PunRPC]
    protected void RpcServerReload()
    {
        ServerReload();
    }


    public void CmdBoostRecovery()
    {
        photonView.MasterRPC(RpcServerBoostRecovery);
    }

    [PunRPC]
    protected void RpcServerBoostRecovery()
    {
        ServerBoostRecovery();
    }

    public void CmdAddAttribute(string name)
    {
        photonView.MasterRPC(RpcServerAddAttribute, name);
    }

    [PunRPC]
    protected void RpcServerAddAttribute(string name)
    {
        if (statPoint > 0)
        {
            CharacterAttributes attribute;
            if (GameplayManager.Singleton.attributes.TryGetValue(name, out attribute))
            {
                addStats += attribute.stats;
                --statPoint;
            }
        }
    }

    public void CmdChangeWeapon(int index)
    {
        photonView.MasterRPC(RpcServerChangeWeapon, index);
    }

    [PunRPC]
    protected void RpcServerChangeWeapon(int index)
    {
        ServerChangeWeapon(index);
    }

    public void CmdPickup(int viewId)
    {
        photonView.MasterRPC(RpcServerPickup, viewId);
    }

    [PunRPC]
    protected void RpcServerPickup(int viewId)
    {
        var go = PhotonView.Find(viewId);
        if (go == null)
            return;
        var pickup = go.GetComponent<PickupEntity>();
        if (pickup == null)
            return;
        pickup.Pickup(this);
    }

    [PunRPC]
    public void RpcReload()
    {
        if (!PhotonNetwork.IsMasterClient)
            reloadRoutine = StartCoroutine(ReloadRoutine());
    }

    [PunRPC]
    public void RpcBoostRecovery()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            boostRecoveryRoutine = StartCoroutine(BoostRecoveryRoutine());
        }
    }

    [PunRPC]
    public void RpcInterruptAttack()
    {
        if (!PhotonNetwork.IsMasterClient)
            InterruptAttack();
    }

    [PunRPC]
    public void RpcInterruptReload()
    {
        if (!PhotonNetwork.IsMasterClient)
            InterruptReload();
    }

    [PunRPC]
    protected void RpcTargetDead()
    {
        deathTime = Time.unscaledTime;
    }

    [PunRPC]
    public void RpcTargetSpawn(float x, float y, float z)
    {
        transform.position = new Vector3(x, y, z);
    }

    [PunRPC]
    protected void RpcTargetRewardCurrency(string currencyId, int amount)
    {
        MonetizationManager.Save.AddCurrency(currencyId, amount);
    }

    #region Update RPCs
    [PunRPC]
    protected virtual void RpcUpdateHp(int hp)
    {
        _hp = hp;
    }
    [PunRPC]
    protected virtual void RpcUpdateArmor(int armor)
    {
        _armor = armor;
    }
    [PunRPC]
    protected virtual void RpcUpdateBoostEnergy(int boostEnergy)
    {
        _boostEnergy = boostEnergy;
    }
    [PunRPC]
    protected virtual void RpcUpdateExp(int exp)
    {
        _exp = exp;
    }
    [PunRPC]
    protected virtual void RpcUpdateLevel(int level)
    {
        _level = level;
    }
    [PunRPC]
    protected virtual void RpcUpdateStatPoint(int statPoint)
    {
        _statPoint = statPoint;
    }
    [PunRPC]
    protected virtual void RpcUpdateWatchAdsCount(int watchAdsCount)
    {
        _watchAdsCount = watchAdsCount;
    }
    [PunRPC]
    protected virtual void RpcUpdateSelectCharacter(int selectCharacter)
    {
        _selectCharacter = selectCharacter;

        if (characterModel != null)
            Destroy(characterModel.gameObject);

        characterData = GameInstance.GetCharacter(selectCharacter);
        if (characterData == null || characterData.modelObject == null)
            return;
        baseLandedCoolDown = characterData.baseStandingRecover;
        baseBoostRecoveryRate = characterData.baseBoostRecoveryRate;
        landedPauseMovementDuration = characterData.landedPauseMovementDuration;

        characterModel = Instantiate(characterData.modelObject, characterModelTransform);
        characterModel.transform.localPosition = Vector3.zero;
        characterModel.transform.localEulerAngles = Vector3.zero;
        characterModel.transform.localScale = Vector3.one;
        if (characterModel.upperContainer != null)
            characterUpperPart = characterModel.upperContainer;
        if (headData != null)
            characterModel.SetHeadModel(headData.modelObject);
        if (WeaponData != null)
            characterModel.SetWeaponModel(WeaponData.rightHandObject, WeaponData.leftHandObject, WeaponData.shieldObject);
        if (customEquipmentDict != null)
        {
            characterModel.ClearCustomModels();
            foreach (var value in customEquipmentDict.Values)
            {
                characterModel.SetCustomModel(value.containerIndex, value.modelObject);
            }
        }
        characterModel.gameObject.SetActive(true);
        UpdateCharacterModelHiddingState();
    }
    [PunRPC]
    protected virtual void RpcUpdateSelectHead(int selectHead)
    {
        _selectHead = selectHead;
        headData = GameInstance.GetHead(selectHead);
        if (characterModel != null && headData != null)
            characterModel.SetHeadModel(headData.modelObject);
        UpdateCharacterModelHiddingState();
    }
    [PunRPC]
    protected virtual void RpcUpdateSelectWeapons(int[] selectWeapons)
    {
        _selectWeapons = selectWeapons;
        // Changes weapon list, equip first weapon equipped position
        var minEquipPos = int.MaxValue;
        for (var i = 0; i < _selectWeapons.Length; ++i)
        {
            var weaponData = GameInstance.GetWeapon(_selectWeapons[i]);

            if (weaponData == null)
                continue;

            var equipPos = weaponData.equipPosition;
            if (minEquipPos > equipPos)
            {
                defaultWeaponIndex = equipPos;
                minEquipPos = equipPos;
            }

            var equippedWeapon = new EquippedWeapon();
            equippedWeapon.defaultId = weaponData.GetHashId();
            equippedWeapon.weaponId = weaponData.GetHashId();
            equippedWeapon.SetMaxAmmo();
            equippedWeapons[equipPos] = equippedWeapon;
            if (PhotonNetwork.IsMasterClient)
                photonView.AllRPC(RpcUpdateEquippedWeapons, equipPos, equippedWeapon.defaultId, equippedWeapon.weaponId, equippedWeapon.currentAmmo, equippedWeapon.currentReserveAmmo);
        }
        selectWeaponIndex = defaultWeaponIndex;
    }
    [PunRPC]
    protected virtual void RpcUpdateSelectCustomEquipments(int[] selectCustomEquipments)
    {
        _selectCustomEquipments = selectCustomEquipments;
        if (characterModel != null)
            characterModel.ClearCustomModels();
        customEquipmentDict.Clear();
        if (_selectCustomEquipments != null)
        {
            for (var i = 0; i < _selectCustomEquipments.Length; ++i)
            {
                var customEquipmentData = GameInstance.GetCustomEquipment(_selectCustomEquipments[i]);
                if (customEquipmentData != null &&
                    !customEquipmentDict.ContainsKey(customEquipmentData.containerIndex))
                {
                    customEquipmentDict[customEquipmentData.containerIndex] = customEquipmentData;
                    if (characterModel != null)
                        characterModel.SetCustomModel(customEquipmentData.containerIndex, customEquipmentData.modelObject);
                }
            }
        }
        UpdateCharacterModelHiddingState();
    }
    [PunRPC]
    protected virtual void RpcUpdateSelectWeaponIndex(int selectWeaponIndex)
    {
        _selectWeaponIndex = selectWeaponIndex;
        if (selectWeaponIndex < 0 || selectWeaponIndex >= equippedWeapons.Length)
            return;
        if (characterModel != null && WeaponData != null)
            characterModel.SetWeaponModel(WeaponData.rightHandObject, WeaponData.leftHandObject, WeaponData.shieldObject);
        UpdateCharacterModelHiddingState();
    }
    [PunRPC]
    protected virtual void RpcUpdateIsInvincible(bool isInvincible)
    {
        _isInvincible = isInvincible;
    }
    [PunRPC]
    protected virtual void RpcUpdateIsGrounded(bool isGrounded)
    {
        _isGrounded = isGrounded;
    }
    [PunRPC]
    protected virtual void RpcUpdateAttackingActionId(int attackingActionId)
    {
        _attackingActionId = attackingActionId;
    }
    [PunRPC]
    protected virtual void RpcUpdateAddStats(CharacterStats addStats)
    {
        _addStats = addStats;
    }
    [PunRPC]
    protected virtual void RpcUpdateExtra(string extra)
    {
        _extra = extra;
    }
    [PunRPC]
    protected virtual void RpcUpdateAimPosition(Vector3 aimPosition)
    {
        _aimPosition = aimPosition;
    }

    [PunRPC]
    protected virtual void RpcUpdateEquippedWeapons(int index, int defaultId, int weaponId, int currentAmmo, int currentReserveAmmo)
    {
        if (index < 0 || index >= equippedWeapons.Length)
            return;
        var weapon = new EquippedWeapon();
        weapon.defaultId = defaultId;
        weapon.weaponId = weaponId;
        weapon.currentAmmo = currentAmmo;
        weapon.currentReserveAmmo = currentReserveAmmo;
        equippedWeapons[index] = weapon;
        if (index == selectWeaponIndex)
            RpcUpdateSelectWeaponIndex(selectWeaponIndex);
    }
    [PunRPC]
    protected virtual void RpcUpdateEquippedWeaponsAmmo(int index, int currentAmmo, int currentReserveAmmo)
    {
        if (index < 0 || index >= equippedWeapons.Length)
            return;
        var weapon = equippedWeapons[index];
        weapon.currentAmmo = currentAmmo;
        weapon.currentReserveAmmo = currentReserveAmmo;
        equippedWeapons[index] = weapon;
    }

    #endregion
}
