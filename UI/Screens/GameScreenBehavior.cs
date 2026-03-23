using OctoberStudio.Abilities;
using OctoberStudio.Abilities.UI;
using OctoberStudio.Audio;
using OctoberStudio.Bossfight;
using OctoberStudio.Easing;
using OctoberStudio.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace OctoberStudio
{
    public class GameScreenBehavior : MonoBehaviour
    {
        public static GameScreenBehavior Instance { get; private set; }

        private Canvas canvas;

        [SerializeField] BackgroundTintUI blackgroundTint;
        [SerializeField] JoystickBehavior joystick;

        [Header("Abilities")]
        // === 1. 双人窗口 ===
        public AbilitiesWindowBehavior abilitiesWindowP1;
        public AbilitiesWindowBehavior abilitiesWindowP2;

        // === 2. 双人确认状态 ===
        private bool p1FinishedUpgrade = true;
        private bool p2FinishedUpgrade = true;

        [SerializeField] ChestWindowBehavior chestWindow;
        [SerializeField] List<AbilitiesIndicatorsListBehavior> abilitiesLists;

        public ChestWindowBehavior ChestWindow => chestWindow;

        [Header("Top UI")]
        [SerializeField] CanvasGroup topUI;

        [Header("Pause")]
        [SerializeField] Button pauseButton;
        [SerializeField] PauseWindowBehavior pauseWindow;

        [Header("Bossfight")]
        [SerializeField] CanvasGroup bossfightWarning;
        [SerializeField] BossfightHealthbarBehavior bossHealthbar;

        private void Awake()
        {
            Instance = this;
            canvas = GetComponent<Canvas>();

            // === 3. 分别绑定 P1 和 P2 窗口的关闭事件 ===
            if (abilitiesWindowP1 != null)
            {
                abilitiesWindowP1.onPanelClosed += OnAbilitiesPanelClosedP1;
                abilitiesWindowP1.onPanelStartedClosing += CheckAbilitiesStartedClosing;
            }
            if (abilitiesWindowP2 != null)
            {
                abilitiesWindowP2.onPanelClosed += OnAbilitiesPanelClosedP2;
                abilitiesWindowP2.onPanelStartedClosing += CheckAbilitiesStartedClosing;
            }

            pauseButton.onClick.AddListener(PauseButtonClick);
            pauseWindow.OnStartedClosing += OnPauseWindowStartedClosing;
            pauseWindow.OnClosed += OnPauseWindowClosed;
            chestWindow.OnClosed += OnChestWindowClosed;
        }

        private void Start()
        {
            // === 4. 初始化两个窗口 ===
            if (abilitiesWindowP1 != null) abilitiesWindowP1.Init();
            if (abilitiesWindowP2 != null) abilitiesWindowP2.Init();

            GameController.InputManager.InputAsset.UI.Settings.performed += OnSettingsInputClicked;
        }

        private void OnSettingsInputClicked(InputAction.CallbackContext context)
        {
            pauseButton.onClick?.Invoke();
        }

        public void Show(Action onFinish = null)
        {
            canvas.enabled = true;
            onFinish?.Invoke();
        }

        public void Hide(Action onFinish = null)
        {
            canvas.enabled = false;
            onFinish?.Invoke();
        }

        public void ShowBossfightWarning()
        {
            bossfightWarning.gameObject.SetActive(true);
            bossfightWarning.alpha = 0;
            bossfightWarning.DoAlpha(1f, 0.3f);
        }

        public void HideBossFightWarning()
        {
            bossfightWarning.DoAlpha(0f, 0.3f).SetOnFinish(() => bossfightWarning.gameObject.SetActive(false));
            topUI.DoAlpha(0, 0.3f);
        }

        public void ShowBossHealthBar(BossfightData data)
        {
            bossHealthbar.Init(data);
            bossHealthbar.Show();
        }

        public void HideBossHealthbar()
        {
            bossHealthbar.Hide();
            topUI.DoAlpha(1, 0.3f);
        }

        public void LinkBossToHealthbar(EnemyBehavior enemy)
        {
            bossHealthbar.SetBoss(enemy);
        }

        // === 5. 打开双人升级面板的核心逻辑 ===
        public void ShowAbilitiesPanel(List<AbilityData> abilities, bool isLevelUp)
        {
            p1FinishedUpgrade = false;
            p2FinishedUpgrade = false;

            // 给两边都塞入技能数据
            if (abilitiesWindowP1 != null) abilitiesWindowP1.SetData(abilities);
            if (abilitiesWindowP2 != null) abilitiesWindowP2.SetData(abilities);

            EasingManager.DoAfter(0.2f, () =>
            {
                for (int i = 0; i < abilitiesLists.Count; i++)
                {
                    var abilityList = abilitiesLists[i];
                    abilityList.Show();
                    abilityList.Refresh();
                }
            }, true);

            blackgroundTint.Show();

            // 两边同时弹出
            if (abilitiesWindowP1 != null) abilitiesWindowP1.Show(isLevelUp);
            if (abilitiesWindowP2 != null) abilitiesWindowP2.Show(isLevelUp);

            GameController.InputManager.InputAsset.UI.Settings.performed -= OnSettingsInputClicked;
        }

        // === 6. 检查是否两人都选完了的逻辑 ===
        private void CheckAbilitiesStartedClosing()
        {
            // 当一个人选完时，检查另一个人是不是也选完了。只有两人都选完，才隐藏黑色遮罩
            if (p1FinishedUpgrade && p2FinishedUpgrade)
            {
                for (int i = 0; i < abilitiesLists.Count; i++)
                {
                    var abilityList = abilitiesLists[i];
                    abilityList.Hide();
                }
                blackgroundTint.Hide();
            }
        }

        private void OnAbilitiesPanelClosedP1()
        {
            p1FinishedUpgrade = true;
            CheckAbilitiesFullyClosed();
        }

        private void OnAbilitiesPanelClosedP2()
        {
            p2FinishedUpgrade = true;
            CheckAbilitiesFullyClosed();
        }

        private void CheckAbilitiesFullyClosed()
        {
            // 只有两人都彻底关掉面板，才允许再次呼出设置菜单（游戏恢复）
            if (p1FinishedUpgrade && p2FinishedUpgrade)
            {
                GameController.InputManager.InputAsset.UI.Settings.performed += OnSettingsInputClicked;
            }
        }

        // ============================================

        public void ShowChestWindow(int tierId, List<AbilityData> abilities, List<AbilityData> selectedAbilities)
        {
            chestWindow.OpenWindow(tierId, abilities, selectedAbilities);
            GameController.InputManager.InputAsset.UI.Settings.performed -= OnSettingsInputClicked;
        }

        private void OnChestWindowClosed()
        {
            GameController.InputManager.InputAsset.UI.Settings.performed += OnSettingsInputClicked;
        }

        private void PauseButtonClick()
        {
            GameController.AudioManager.PlaySound(AudioManager.BUTTON_CLICK_HASH);

            joystick.Disable();

            blackgroundTint.Show();
            pauseWindow.Open();

            GameController.InputManager.InputAsset.UI.Settings.performed -= OnSettingsInputClicked;
        }

        private void OnPauseWindowClosed()
        {
            if (GameController.InputManager.ActiveInput == Input.InputType.UIJoystick)
            {
                joystick.Enable();
            }

            GameController.InputManager.InputAsset.UI.Settings.performed += OnSettingsInputClicked;
        }

        private void OnPauseWindowStartedClosing()
        {
            blackgroundTint.Hide();
        }
    }
}
