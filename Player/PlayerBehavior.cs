using OctoberStudio.Easing;
using OctoberStudio.Extensions;
using OctoberStudio.Upgrades;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace OctoberStudio
{
    public class PlayerBehavior : MonoBehaviour
    {
        [Header("Multiplayer Settings")]
        public int playerId = 1; // 在 Unity 面板里区分 P1 和 P2

        [Header("Player Stats (独立数据)")]
        public int gold = 0;       // 玩家自己的金币
        public int kills = 0;      // 玩家自己的击杀数
        public int xp = 0;         // 玩家自己的经验
        public int level = 1;      // 玩家等级
        public int maxXp = 5;      // 升下一级所需经验

        // 双人玩家花名册，用于怪物寻路和地图生成
        public static List<PlayerBehavior> ActivePlayers = new List<PlayerBehavior>();
        // === 加上这两行，让外部UI可以直接找到对应的玩家 ===
        public static PlayerBehavior Player1;
        public static PlayerBehavior Player2;

        // 所有原有变量定义保持不变...
        private static readonly int DEATH_HASH = "Death".GetHashCode();
        private static readonly int REVIVE_HASH = "Revive".GetHashCode();
        private static readonly int RECEIVING_DAMAGE_HASH = "Receiving Damage".GetHashCode();

        private static PlayerBehavior instance;
        public static PlayerBehavior Player => instance;

        [SerializeField] CharactersDatabase charactersDatabase;

        [Header("Stats")]
        [SerializeField, Min(0.01f)] float speed = 2;
        [SerializeField, Min(0.1f)] float defaultMagnetRadius = 0.75f;
        [SerializeField, Min(1f)] float xpMultiplier = 1;
        [SerializeField, Range(0.1f, 1f)] float cooldownMultiplier = 1;
        [SerializeField, Range(0, 100)] int initialDamageReductionPercent = 0;
        [SerializeField, Min(1f)] float initialProjectileSpeedMultiplier = 1;
        [SerializeField, Min(1f)] float initialSizeMultiplier = 1f;
        [SerializeField, Min(1f)] float initialDurationMultiplier = 1f;
        [SerializeField, Min(1f)] float initialGoldMultiplier = 1;

        [Header("References")]
        [SerializeField] public HealthbarBehavior healthbar;
        [SerializeField] Transform centerPoint;
        [SerializeField] PlayerEnemyCollisionHelper collisionHelper;

        public static Transform CenterTransform => instance.centerPoint;
        public static Vector2 CenterPosition => instance.centerPoint.position;

        [Header("Death and Revive")]
        [SerializeField] ParticleSystem reviveParticle;

        [Space]
        [SerializeField] SpriteRenderer reviveBackgroundSpriteRenderer;
        [SerializeField, Range(0, 1)] float reviveBackgroundAlpha;
        [SerializeField, Range(0, 1)] float reviveBackgroundSpawnDelay;
        [SerializeField, Range(0, 1)] float reviveBackgroundHideDelay;

        [Space]
        [SerializeField] SpriteRenderer reviveBottomSpriteRenderer;
        [SerializeField, Range(0, 1)] float reviveBottomAlpha;
        [SerializeField, Range(0, 1)] float reviveBottomSpawnDelay;
        [SerializeField, Range(0, 1)] float reviveBottomHideDelay;

        [Header("Other")]
        [SerializeField] Vector2 fenceOffset;
        [SerializeField] Color hitColor;
        [SerializeField] float enemyInsideDamageInterval = 2f;

        [Header("经验物品拾取设置")]
        [SerializeField] private float gemDetectionRadius = 5f;
        [SerializeField] private float avoidEnemyPriority = 1.5f;
        [SerializeField] private float collectGemPriority = 1f;
        [SerializeField] private string gemNameKeyword = "Gem";

        [Header("自动移动平滑设置")]
        [SerializeField] private float directionSmoothTime = 0.1f;
        [SerializeField] private float directionChangeThreshold = 0.2f;
        [SerializeField] private float minDirectionSwitchInterval = 0.2f;

        [Header("BOSS战墙壁躲避设置")]
        [SerializeField] private string bossNameKeyword = "BOSS";
        [SerializeField] private string wallNameKeyword = "Wall Spike Link";
        //[SerializeField] private float wallDetectionDistance = 1f;
        //[SerializeField] private float wallAvoidPriority = 2f;
        [SerializeField] private float bossAvoidPriority = 1.8f;
        [SerializeField] private float bossOrbitRadius = 4f; // 环绕BOSS的半径
        [SerializeField] private float orbitSpeed = 2f; // 环绕速度
        [SerializeField] private float wallCheckRadius = 0.8f; // 射线检测半径

        public event UnityAction onPlayerDied;

        public float Damage { get; private set; }
        public float MagnetRadiusSqr { get; private set; }
        public float Speed { get; private set; }

        public float XPMultiplier { get; private set; }
        public float CooldownMultiplier { get; private set; }
        public float DamageReductionMultiplier { get; private set; }
        public float ProjectileSpeedMultiplier { get; private set; }
        public float SizeMultiplier { get; private set; }
        public float DurationMultiplier { get; private set; }
        public float GoldMultiplier { get; private set; }

        public Vector2 LookDirection { get; private set; }
        public bool IsMovingAlowed { get; set; }

        // 自动移动相关变量
        private bool isAutoMoving = false;
        public float moveSpeed = 3f; // 移动速度（确保在Inspector赋值）
        private float lastTimeSwitchedDirection = 0;
        private EnemyBehavior closestEnemy;
        public InputAction autoMoveHoldAction;
        private Vector2 smoothedDirection;
        private Vector2 directionVelocity;
        private EnemyBehavior _lockedEnemy; // 锁定的敌人目标（避免频繁切换）
        private float enemyLockThreshold = 2f; // 敌人锁定阈值（距离差小于此值不切换）

        // BOSS和墙壁检测相关变量
        private Transform closestBoss;
        //private List<Transform> wallSpikes = new List<Transform>();
        private bool isClockwise = true; // 环绕方向：顺时针/逆时针
        //private float lastWallCheckTime = 0;
        //private float wallCheckInterval = 0.2f;
        private float lastOrbitDirectionChange = 0;
        private float minOrbitDirectionChangeInterval = 1f; // 最小方向切换间隔


        [SerializeField] private float bossSafeDistance = 4f; // 与BOSS保持的安全距离
        [SerializeField] private float bossFarAvoidPriority = 1.2f; // 距离BOSS较远时的躲避优先级
        [SerializeField] private float wallAvoidWeightFactor = 0.8f; // 墙壁躲避权重系数，降低过度躲避墙壁的影响


        private bool invincible = false;
        private List<EnemyBehavior> enemiesInside = new List<EnemyBehavior>();
        private CharactersSave charactersSave;
        public CharacterData Data { get; set; }
        private CharacterBehavior Character { get; set; }

        private void Awake()
        {
            // === 加上这两行身份登记 ===
            if (playerId == 1) Player1 = this;
            else if (playerId == 2) Player2 = this;

            ActivePlayers.Add(this); // 加入花名册
            charactersSave = GameController.SaveManager.GetSave<CharactersSave>("Characters");
            Data = charactersDatabase.GetCharacterData(charactersSave.SelectedCharacterId);

            Character = Instantiate(Data.Prefab).GetComponent<CharacterBehavior>();
            Character.transform.SetParent(transform);
            Character.transform.ResetLocal();

            instance = this;
            healthbar.Init(Data.BaseHP);
            healthbar.SetAutoHideWhenMax(true);
            healthbar.SetAutoShowOnChanged(true);

            RecalculateMagnetRadius(1);
            RecalculateMoveSpeed(1);
            RecalculateDamage(1);
            RecalculateMaxHP(1);
            RecalculateXPMuliplier(1);
            RecalculateCooldownMuliplier(1);
            RecalculateDamageReduction(0);
            RecalculateProjectileSpeedMultiplier(1f);
            RecalculateSizeMultiplier(1f);
            RecalculateDurationMultiplier(1);
            RecalculateGoldMultiplier(1);

            LookDirection = Vector2.right;
            IsMovingAlowed = true;

            InitializeInputActions();
        }

        private void InitializeInputActions()
        {
            var inputActionMap = new InputActionMap("PlayerAutoMoveActions");
            autoMoveHoldAction = inputActionMap.AddAction(
                "HoldAutoMove",
                InputActionType.Button,
                "<Keyboard>/p"
            );
            autoMoveHoldAction.Enable();
        }

        private void Update()
        {
            if (healthbar.IsZero) return;

            foreach (var enemy in enemiesInside)
            {
                if (Time.time - enemy.LastTimeDamagedPlayer > enemyInsideDamageInterval)
                {
                    TakeDamage(enemy.GetDamage());
                    enemy.LastTimeDamagedPlayer = Time.time;
                }
            }

            if (!IsMovingAlowed) return;

            // 定期更新墙壁列表（只包含启用的物体）
            //if (Time.time - lastWallCheckTime > wallCheckInterval)
            //{
            //    UpdateWallSpikesList();
            //    lastWallCheckTime = Time.time;
            //}

            bool currentBrainFocus = false;
            if (SseListenerMono.Instance != null)
            {
                currentBrainFocus = (playerId == 1) ? SseListenerMono.P1_Focused : SseListenerMono.P2_Focused;
            }

            // 判断是否进入自动移动状态（按下键盘操作键，或者当前玩家脑电专注度达标）
            if (autoMoveHoldAction != null)
            {
                if (autoMoveHoldAction.IsPressed() || currentBrainFocus)
                {
                    isAutoMoving = true;
                }
                else
                {
                    isAutoMoving = false;
                }
            }

            if (isAutoMoving)
            {
                HandleAutoMovement();
            }
            else
            {
                HandleManualMovement();
                smoothedDirection = Vector2.zero;
                directionVelocity = Vector2.zero;
            }
            //if (Input.GetKeyDown(KeyCode.P))
            //{
            //    isAutoMoving = !isAutoMoving;
            //    // 激活时强制唤醒移动组件（解决初始不动问题）
            //    if (isAutoMoving)
            //    {
            //        Rigidbody2D rb = GetComponent<Rigidbody2D>();
            //        if (rb != null) rb.WakeUp(); // 唤醒休眠的刚体
            //    }
            //}
        }

        // 玩家独立的获取经验与升级逻辑
        public void AddXP(int amount)
        {
            xp += amount;
            if (xp >= maxXp)
            {
                xp -= maxXp;
                level++;
                maxXp = Mathf.RoundToInt(maxXp * 1.5f); // 每级经验递增公式

                // 触发对应玩家的升级UI
                //if (GameScreenBehavior.Instance != null)
                //{
                //    if (playerId == 1) GameScreenBehavior.Instance.abilitiesWindowP1.Show(true);
                //    else GameScreenBehavior.Instance.abilitiesWindowP2.Show(true);
                //}
                var abilityManager = FindObjectOfType<OctoberStudio.Abilities.AbilityManager>();
                if (abilityManager != null)
                {
                    // 呼叫发牌器，把随机技能直接发给当前升级的这名玩家 (this)
                    abilityManager.GiveRandomAbility(level, this);
                }
            }
        }

        private void HandleAutoMovement()
        {
            if (!isAutoMoving) return; // 未开启自动移动则退出
            // 检测最近的敌人（原有逻辑）
            closestEnemy = StageController.EnemiesSpawner.GetClosestEnemy(transform.position.XY());
            // 检测最近的BOSS（只检测启用的）
            closestBoss = GetClosestBoss();

            // 检测最近的经验物品（只检测启用的）
            Transform closestGem = GetClosestGemInRangeByName();

            // 修复问题2：锁定敌人目标（避免频繁切换）
            if (closestEnemy != null)
            {
                if (_lockedEnemy == null)
                {
                    _lockedEnemy = closestEnemy; // 首次锁定最近敌人
                }
                else
                {
                    // 计算当前敌人与锁定敌人的距离差
                    float currentEnemyDist = Vector2.Distance(transform.position.XY(), closestEnemy.transform.position.XY());
                    float lockedEnemyDist = Vector2.Distance(transform.position.XY(), _lockedEnemy.transform.position.XY());
                    // 若距离差小于阈值，保持锁定（不切换）
                    if (Mathf.Abs(currentEnemyDist - lockedEnemyDist) < enemyLockThreshold)
                    {
                        closestEnemy = _lockedEnemy;
                    }
                    else
                    {
                        _lockedEnemy = closestEnemy; // 超过阈值则更新锁定
                    }
                }
            }
            else
            {
                _lockedEnemy = null; // 无敌人时解锁
            }


            // 基础方向向量
            Vector2 rawDirection = Vector2.zero;

            // 检测BOSS是否存在
            bool isBossPresent = closestBoss != null;

            // BOSS战逻辑
            if (isBossPresent)
            {
                // BOSS战：圆周运动逻辑
                rawDirection = CalculateOrbitDirection();
            }
            //else
            //{
            //    // 保留非BOSS战逻辑...
            //    Vector2 avoidDirection = Vector2.zero;
            //    if (closestEnemy != null)
            //    {
            //        avoidDirection = (transform.position.XY() - closestEnemy.transform.position.XY()).normalized;
            //    }

            //    Vector2 collectDirection = Vector2.zero;
            //    if (closestGem != null)
            //    {
            //        collectDirection = (closestGem.position.XY() - transform.position.XY()).normalized;
            //    }

            //    if (closestGem != null)
            //    {
            //        rawDirection = collectDirection;
            //        _lockedEnemy = null;
            //    }
            //    else if (closestEnemy != null)
            //    {
            //        rawDirection = avoidDirection;
            //    }
            //    else
            //    {
            //        rawDirection = Vector2.zero;
            //    }
            //}
            // 普通模式逻辑
            else
            {
                Vector2 avoidDirection = Vector2.zero;
                if (closestEnemy != null)
                {
                    avoidDirection = (transform.position.XY() - closestEnemy.transform.position.XY()).normalized;
                }

                Vector2 collectDirection = Vector2.zero;
                if (closestGem != null)
                {
                    collectDirection = (closestGem.position.XY() - transform.position.XY()).normalized;
                }

                //if (closestEnemy != null && closestGem != null)
                //{
                //    rawDirection = (avoidDirection * avoidEnemyPriority + collectDirection * collectGemPriority).normalized;
                //}
                //else if (closestEnemy != null)
                //{
                //    rawDirection = avoidDirection;
                //}
                //else if (closestGem != null)
                //{
                //    rawDirection = collectDirection;
                //}

                // 核心逻辑调整：有激活宝石时优先收集，否则才躲避小怪
                if (closestGem != null)
                {
                    // 存在激活的宝石：只向宝石移动，忽略小怪
                    rawDirection = collectDirection;
                    _lockedEnemy = null; // 有宝石时解锁敌人锁定
                }
                else if (closestEnemy != null)
                {
                    // 无激活宝石但有小怪：按原逻辑躲避
                    rawDirection = avoidDirection;
                }
                else
                {
                    // 既无宝石也无小怪：保持不动
                    rawDirection = Vector2.zero;
                }

                // 优先判断是否有激活的经验点
                //if (closestGem != null)
                //{
                //    // 有激活的经验点：移动到经验点
                //    rawDirection = (closestGem.position.XY() - transform.position.XY()).normalized;
                //}
                //else if (closestEnemy != null)
                //{
                //    // 没有激活的经验点但有小怪：移动到小怪的反方向（原逻辑）
                //    rawDirection = (transform.position.XY() - closestEnemy.transform.position.XY()).normalized;
                //}
                //else
                //{
                //    // 既没有经验点也没有小怪：保持不动
                //    rawDirection = Vector2.zero;
                //}
            }

            // 平滑方向变化
            if (rawDirection != Vector2.zero)
            {
                smoothedDirection = Vector2.SmoothDamp(
                    smoothedDirection,
                    rawDirection,
                    ref directionVelocity,
                    directionSmoothTime
                );
            }
            else
            {
                // 无目标时快速归零（避免残留方向导致抖动）
                smoothedDirection = Vector2.SmoothDamp(
                    smoothedDirection,
                    Vector2.zero,
                    ref directionVelocity,
                    0.1f
                );
            }

            // 应用移动
            if (smoothedDirection != Vector2.zero)
            {
                float frameMovement = Time.deltaTime * Speed;
                Vector3 movement = new Vector3(
                    smoothedDirection.x * frameMovement,
                    smoothedDirection.y * frameMovement,
                    0
                );

                if (StageController.FieldManager.ValidatePosition(transform.position + Vector3.right * movement.x, fenceOffset))
                {
                    transform.position += Vector3.right * movement.x;
                }
                if (StageController.FieldManager.ValidatePosition(transform.position + Vector3.up * movement.y, fenceOffset))
                {
                    transform.position += Vector3.up * movement.y;
                }

                collisionHelper.transform.localPosition = Vector3.zero;
                UpdateLookDirection(smoothedDirection);
                Character.SetSpeed(1f);
            }
            else
            {
                Character.SetSpeed(0f);
            }
        }

        public static PlayerBehavior GetClosestPlayer(Vector3 position)
        {
            if (ActivePlayers.Count == 0) return Player;
            PlayerBehavior closest = null;
            float minDistance = float.MaxValue;
            foreach (var p in ActivePlayers)
            {
                if (p == null || p.healthbar.IsZero) continue;
                float dist = (p.transform.position - position).sqrMagnitude;
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = p;
                }
            }
            return closest != null ? closest : Player;
        }

        // 计算环绕BOSS的移动方向
        private Vector2 CalculateOrbitDirection()
        {
            if (closestBoss == null) return Vector2.zero;

            // 计算玩家到BOSS的向量
            Vector2 toBoss = (closestBoss.position.XY() - transform.position.XY());
            float currentDistance = toBoss.magnitude;

            // 1. 距离调整：保持在环绕半径附近
            Vector2 distanceAdjustment = Vector2.zero;
            if (Mathf.Abs(currentDistance - bossOrbitRadius) > 0.5f)
            {
                // 过远则靠近，过近则远离
                distanceAdjustment = toBoss.normalized * (currentDistance > bossOrbitRadius ? -0.3f : 0.3f);
            }

            // 2. 检测墙壁遮挡（射线检测）
            bool isBlocked = IsPathBlockedByWall(transform.position.XY(), closestBoss.position.XY());

            // 3. 处理遮挡：切换环绕方向
            if (isBlocked && Time.time - lastOrbitDirectionChange > minOrbitDirectionChangeInterval)
            {
                isClockwise = !isClockwise; // 反向
                lastOrbitDirectionChange = Time.time;
            }

            // 4. 计算圆周切线方向
            Vector2 tangentDirection = isClockwise
                ? new Vector2(-toBoss.y, toBoss.x).normalized  // 顺时针切线
                : new Vector2(toBoss.y, -toBoss.x).normalized; // 逆时针切线

            // 5. 结合切线方向和距离调整，形成最终移动方向
            return (tangentDirection * orbitSpeed + distanceAdjustment).normalized;
        }

        // 检测玩家到BOSS的路径是否被墙壁遮挡
        private bool IsPathBlockedByWall(Vector2 from, Vector2 to)
        {
            // 1. 计算玩家到BOSS的向量和距离
            Vector2 direction = to - from;
            float distance = direction.magnitude;
            Vector2 center = from + direction / 2f; // 线段中点（检测区域中心）

            // 2. 定义检测区域（包围玩家到BOSS的线段的矩形）
            // 宽度：略大于角色半径，高度：线段长度
            Vector2 boxSize = new Vector2(wallCheckRadius * 2, distance);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg; // 矩形旋转角度（与线段方向一致）

            // 3. 检测该区域内是否有墙壁碰撞体（需确保墙壁在"Wall"层）
            int wallLayer = LayerMask.NameToLayer("Wall"); // 假设墙壁在"Wall"层
            Collider2D[] colliders = Physics2D.OverlapBoxAll(center, boxSize, angle, 1 << wallLayer);

            // 4. 过滤出BOSS战的墙壁（根据名称关键字）
            foreach (var collider in colliders)
            {
                if (collider.name.Contains(wallNameKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true; // 检测到墙壁遮挡
                }
            }
            return false; // 无遮挡
        }

        // 保留原有BOSS检测方法...
        private Transform GetClosestBoss()
        {
            List<Transform> allBosses = new List<Transform>();
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.activeInHierarchy)
                {
                    FindBossesByName(root.transform, allBosses);
                }
            }

            if (allBosses.Count == 0) return null;

            return allBosses.OrderBy(boss =>
                Vector2.Distance(transform.position.XY(), boss.position.XY())
            ).First();
        }

        private void FindBossesByName(Transform parent, List<Transform> resultList)
        {
            if (parent.gameObject.activeInHierarchy &&
                parent.name.Contains(bossNameKeyword, StringComparison.OrdinalIgnoreCase))
            {
                resultList.Add(parent);
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                FindBossesByName(parent.GetChild(i), resultList);
            }
        }

        

        private void HandleManualMovement()
        {
            var input = GameController.InputManager.MovementValue;
            float joysticPower = input.magnitude;
            Character.SetSpeed(joysticPower);

            if (!Mathf.Approximately(joysticPower, 0) && Time.timeScale > 0)
            {
                var frameMovement = input * Time.deltaTime * Speed;

                if (StageController.FieldManager.ValidatePosition(transform.position + Vector3.right * frameMovement.x, fenceOffset))
                {
                    transform.position += Vector3.right * frameMovement.x;
                }

                if (StageController.FieldManager.ValidatePosition(transform.position + Vector3.up * frameMovement.y, fenceOffset))
                {
                    transform.position += Vector3.up * frameMovement.y;
                }

                collisionHelper.transform.localPosition = Vector3.zero;
                UpdateLookDirection(input.normalized);
            }
            else
            {
                Character.SetSpeed(0f);
            }
        }

        private void UpdateLookDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0.1f)
            {
                LookDirection = direction;
                var scale = transform.localScale;
                float targetScaleX = direction.x > 0 ? 1 : -1;

                bool directionChanged = Mathf.Abs(targetScaleX - Mathf.Sign(scale.x)) > directionChangeThreshold;
                bool canSwitch = Time.unscaledTime - lastTimeSwitchedDirection > minDirectionSwitchInterval;

                if (directionChanged && canSwitch)
                {
                    scale.x = targetScaleX;
                    transform.localScale = scale;
                    lastTimeSwitchedDirection = Time.unscaledTime;
                }

                Character.SetLocalScale(new Vector3(Mathf.Sign(scale.x), 1, 1));
            }
        }

        // 通过名称关键词查找范围内最近的经验物品（只检测启用的）
        private Transform GetClosestGemInRangeByName()
        {
            Transform closestGem = null;
            float closestDistance = Mathf.Infinity;
            float gemDetectionRadius = 20f;

            foreach (var gem in GameObject.FindObjectsOfType<Transform>())
            {
                // 只检测激活的宝石（掉落状态）
                if (gem.name.Contains("Gem") && gem.gameObject.activeInHierarchy)
                {
                    float distance = Vector2.Distance(transform.position.XY(), gem.position.XY());
                    if (distance <= gemDetectionRadius && distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestGem = gem;
                    }
                }
            }
            return closestGem;
        }

        // 递归查找所有名称含有关键词的物体（只添加启用状态的）
        private void FindGemsByName(Transform parent, List<Transform> resultList)
        {
            // 检查当前物体是否启用且名称匹配
            if (parent.gameObject.activeInHierarchy &&
                parent.name.Contains(gemNameKeyword, StringComparison.OrdinalIgnoreCase))
            {
                resultList.Add(parent);
            }

            // 递归检查子物体
            for (int i = 0; i < parent.childCount; i++)
            {
                FindGemsByName(parent.GetChild(i), resultList);
            }
        }

        // 其他原有方法保持不变...
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInsideMagnetRadius(Transform target)
        {
            return (transform.position - target.position).sqrMagnitude <= MagnetRadiusSqr;
        }

        public void RecalculateMagnetRadius(float magnetRadiusMultiplier)
        {
            MagnetRadiusSqr = Mathf.Pow(defaultMagnetRadius * magnetRadiusMultiplier, 2);
        }

        public void RecalculateMoveSpeed(float moveSpeedMultiplier)
        {
            Speed = speed * moveSpeedMultiplier;
        }

        public void RecalculateDamage(float damageMultiplier)
        {
            Damage = Data.BaseDamage * damageMultiplier;
            if (GameController.UpgradesManager.IsUpgradeAquired(UpgradeType.Damage))
            {
                Damage *= GameController.UpgradesManager.GetUpgadeValue(UpgradeType.Damage);
            }
        }

        public void RecalculateMaxHP(float maxHPMultiplier)
        {
            var upgradeValue = GameController.UpgradesManager.GetUpgadeValue(UpgradeType.Health);
            healthbar.ChangeMaxHP((Data.BaseHP + upgradeValue) * maxHPMultiplier);
        }

        public void RecalculateXPMuliplier(float xpMultiplier)
        {
            XPMultiplier = this.xpMultiplier * xpMultiplier;
        }

        public void RecalculateCooldownMuliplier(float cooldownMultiplier)
        {
            CooldownMultiplier = this.cooldownMultiplier * cooldownMultiplier;
        }

        public void RecalculateDamageReduction(float damageReductionPercent)
        {
            DamageReductionMultiplier = (100f - initialDamageReductionPercent - damageReductionPercent) / 100f;

            if (GameController.UpgradesManager.IsUpgradeAquired(UpgradeType.Armor))
            {
                DamageReductionMultiplier *= GameController.UpgradesManager.GetUpgadeValue(UpgradeType.Armor);
            }
        }

        public void RecalculateProjectileSpeedMultiplier(float projectileSpeedMultiplier)
        {
            ProjectileSpeedMultiplier = initialProjectileSpeedMultiplier * projectileSpeedMultiplier;
        }

        public void RecalculateSizeMultiplier(float sizeMultiplier)
        {
            SizeMultiplier = initialSizeMultiplier * sizeMultiplier;
        }

        public void RecalculateDurationMultiplier(float durationMultiplier)
        {
            DurationMultiplier = initialDurationMultiplier * durationMultiplier;
        }

        public void RecalculateGoldMultiplier(float goldMultiplier)
        {
            GoldMultiplier = initialGoldMultiplier * goldMultiplier;
        }

        public void RestoreHP(float hpPercent)
        {
            healthbar.AddPercentage(hpPercent);
        }

        public void Heal(float hp)
        {
            healthbar.AddHP(hp + GameController.UpgradesManager.GetUpgadeValue(UpgradeType.Healing));
        }

        public void Revive()
        {
            Character.PlayReviveAnimation();
            reviveParticle.Play();

            invincible = true;
            IsMovingAlowed = false;
            healthbar.ResetHP(1f);

            Character.SetSortingOrder(102);

            reviveBackgroundSpriteRenderer.DoAlpha(0f, 0.3f, reviveBottomHideDelay).SetUnscaledTime(true).SetOnFinish(() => reviveBackgroundSpriteRenderer.gameObject.SetActive(false));
            reviveBottomSpriteRenderer.DoAlpha(0f, 0.3f, reviveBottomHideDelay).SetUnscaledTime(true).SetOnFinish(() => reviveBottomSpriteRenderer.gameObject.SetActive(false));
        }

        public void TakeDamage(float damage)
        {
            if (invincible || healthbar.IsZero) return;

            float actualDamage = damage * DamageReductionMultiplier;
            healthbar.Subtract(actualDamage);

            Character.FlashHit();

            if (healthbar.IsZero)
            {
                Die();
            }
        }

        private void Die()
        {
            IsMovingAlowed = false;
            Character.PlayDefeatAnimation();
            onPlayerDied?.Invoke();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<EnemyBehavior>(out var enemy))
            {
                enemiesInside.Add(enemy);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent<EnemyBehavior>(out var enemy))
            {
                enemiesInside.Remove(enemy);
            }
        }

        public void CheckTriggerEnter2D(Collider2D other)
        {
            OnTriggerEnter2D(other);
        }

        public void CheckTriggerExit2D(Collider2D other)
        {
            OnTriggerExit2D(other);
        }

        private void OnDestroy()
        {
            autoMoveHoldAction?.Disable();
            ActivePlayers.Remove(this); // 移出花名册
        }
    }
}
