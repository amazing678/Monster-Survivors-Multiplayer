using OctoberStudio.Abilities;
using OctoberStudio.Extensions;
using OctoberStudio.Pool;
using OctoberStudio.Timeline.Bossfight;
using OctoberStudio.UI;
using UnityEngine;
using UnityEngine.Playables;

namespace OctoberStudio
{

    public class StageController : MonoBehaviour
    {
        private static StageController instance;
        

        [SerializeField] StagesDatabase database;
        [SerializeField] PlayableDirector director;
        [SerializeField] EnemiesSpawner spawner;
        [SerializeField] StageFieldManager fieldManager;
        [SerializeField] ExperienceManager experienceManager;
        [SerializeField] DropManager dropManager;
        [SerializeField] AbilityManager abilityManager;
        [SerializeField] PoolsManager poolsManager;
        [SerializeField] WorldSpaceTextManager worldSpaceTextManager;
        [SerializeField] CameraManager cameraManager;
        //private float gamePlayTime = 0f; // 游戏运行时间（秒）
        //private bool isGameCompletedByTime = false; // 是否已通过时间触发完成



        public static EnemiesSpawner EnemiesSpawner => instance.spawner;
        public static ExperienceManager ExperienceManager => instance.experienceManager;
        public static AbilityManager AbilityManager => instance.abilityManager;
        public static StageFieldManager FieldManager => instance.fieldManager;
        public static PlayableDirector Director => instance.director;
        public static PoolsManager PoolsManager => instance.poolsManager;
        public static WorldSpaceTextManager WorldSpaceTextManager => instance.worldSpaceTextManager;
        public static CameraManager CameraController => instance.cameraManager;
        public static DropManager DropManager => instance.dropManager;

        public static StagesDatabase Database => instance.database;

        [Header("UI")]
        [SerializeField] GameScreenBehavior gameScreen;
        [SerializeField] StageFailedScreen stageFailedScreen;
        [SerializeField] StageCompleteScreen stageCompletedScreen;

        [Header("Testing")]
        [SerializeField] PresetData testingPreset;

        public static StageCompleteScreen StageCompletedScreen => instance.stageCompletedScreen;
        public static StageFailedScreen StageFailedScreen => instance.stageFailedScreen;
        public static GameScreenBehavior GameScreen => instance.gameScreen;

        public static StageData Stage { get; private set; }

        private StageSave stageSave;

        private void Awake()
        {
            instance = this;

            stageSave = GameController.SaveManager.GetSave<StageSave>("Stage");
        }

        private void Start()
        {
            Stage = database.GetStage(stageSave.SelectedStageId);

            director.playableAsset = Stage.Timeline;

            spawner.Init(director);
            experienceManager.Init(testingPreset);
            dropManager.Init();
            fieldManager.Init(Stage, director);
            abilityManager.Init(testingPreset, PlayerBehavior.Player.Data);
            cameraManager.Init(Stage);

            PlayerBehavior.Player.onPlayerDied += OnGameFailed;

            director.stopped += TimelineStopped;
            if (testingPreset != null) {
                director.time = testingPreset.StartTime; 
            } else
            {
                var time = stageSave.Time;

                var bossClips = director.GetClips<BossTrack, Boss>();

                for(int i = 0; i < bossClips.Count; i++)
                {
                    var bossClip = bossClips[i];

                    if(time >= bossClip.start && time <= bossClip.end)
                    {
                        time = (float) bossClip.start;
                        break;
                    }
                }

                director.time = time;
            }

            director.Play();
        }

        private void Update()
        {
            // 仅在游戏进行中且未通过时间完成时检查
            if (stageSave.IsPlaying)
            {
                // 直接使用Director.time作为判断依据（与UITimer显示的时间一致）
                if (director.time >= 300f)
                {
                    ForceCompleteStage(); // 强制完成关卡
                }
            }
        }

        private void ForceCompleteStage()
        {
            if (gameObject.activeSelf)
            {
                // 更新最大解锁关卡ID（如果当前关卡是未解锁的）
                if (stageSave.MaxReachedStageId < stageSave.SelectedStageId + 1 && stageSave.SelectedStageId + 1 < database.StagesCount)
                {
                    stageSave.SetMaxReachedStageId(stageSave.SelectedStageId + 1);
                }

                // 标记游戏为非运行状态并保存
                stageSave.IsPlaying = false;
                GameController.SaveManager.Save(true);

                // 隐藏游戏界面，显示通关界面，暂停时间
                gameScreen.Hide();
                stageCompletedScreen.Show();
                Time.timeScale = 0;
            }
        }

        private void TimelineStopped(PlayableDirector director)
        {
            if (gameObject.activeSelf)
            {
                if (stageSave.MaxReachedStageId < stageSave.SelectedStageId + 1 && stageSave.SelectedStageId + 1 < database.StagesCount)
                {
                    stageSave.SetMaxReachedStageId(stageSave.SelectedStageId + 1);
                }

                stageSave.IsPlaying = false;
                GameController.SaveManager.Save(true);

                gameScreen.Hide();
                stageCompletedScreen.Show();
                Time.timeScale = 0;
            }
        }

        private void OnGameFailed()
        {
            Time.timeScale = 0;

            stageSave.IsPlaying = false;
            GameController.SaveManager.Save(true);

            gameScreen.Hide();
            stageFailedScreen.Show();
        }

        public static void ResurrectPlayer()
        {
            EnemiesSpawner.DealDamageToAllEnemies(PlayerBehavior.Player.Damage * 1000);

            GameScreen.Show();
            PlayerBehavior.Player.Revive();
            Time.timeScale = 1;
        }

        public static void ReturnToMainMenu()
        {
            GameController.LoadMainMenu();
        }

        private void OnDisable()
        {
            director.stopped -= TimelineStopped;
        }
    }
}