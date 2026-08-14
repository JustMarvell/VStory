using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace VRGame.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] AssetReference mainMenuScene;
        [SerializeField] AssetReference levelSelectScene;

        AsyncOperationHandle<SceneInstance> currentHandle;
        bool hasLoadedScene;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start() => LoadMainMenu();

        public void LoadMainMenu() => LoadSceneAdditive(mainMenuScene);
        public void LoadLevelSelect() => LoadSceneAdditive(levelSelectScene);

        public void LoadChapter(ChapterDefinition chapter)
        {
            LoadSceneAdditive(chapter.sceneRef, () =>
            {
                var checkpointManager = FindFirstObjectByType<CheckpointManager>();
                checkpointManager?.RespawnAtCheckpoint(chapter);
            });
        }

        void LoadSceneAdditive(AssetReference sceneRef, Action onComplete = null)
        {
            if (hasLoadedScene)
                Addressables.UnloadSceneAsync(currentHandle).Completed += _ => LoadNew(sceneRef, onComplete);
            else
                LoadNew(sceneRef, onComplete);
        }

        void LoadNew(AssetReference sceneRef, Action onComplete)
        {
            currentHandle = sceneRef.LoadSceneAsync(UnityEngine.SceneManagement.LoadSceneMode.Additive);
            currentHandle.Completed += _ =>
            {
                hasLoadedScene = true;
                onComplete?.Invoke();
            };
        }
    }
}