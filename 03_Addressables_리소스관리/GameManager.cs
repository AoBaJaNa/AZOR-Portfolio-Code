using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Texture2D cursor;

    [Header("Player Settings")]
    public GameObject playerPrefab;
    private readonly string playerTag = "Player";

    private StageManager stageManager;

    private void Awake()
    {
        Cursor.SetCursor(cursor, Vector2.zero, CursorMode.ForceSoftware);

        if (!GameSession.IsGameplayScene(SceneManager.GetActiveScene().name))
            return;

        if (!GameSession.Instance.IsGameplayCacheReady)
        {
            Debug.LogError("GameManager: gameplay cache is not ready. LoadingScene flow may have been skipped.");
            return;
        }

        GameObject existingPlayer = GameObject.FindGameObjectWithTag(playerTag);

        if (existingPlayer == null)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("GameManager: playerPrefab is not assigned.");
                return;
            }

            GameObject playerObject = Instantiate(playerPrefab, transform.position, Quaternion.identity);
            PlayerInfo playerInfo = playerObject.GetComponentInChildren<PlayerInfo>();
            if (playerInfo != null)
                playerInfo.InitializeFromSession(GameSession.Instance);
            else
                Debug.LogError("GameManager: spawned player prefab is missing PlayerInfo.");
        }
        else
        {
            PlayerInfo playerInfo = existingPlayer.GetComponentInChildren<PlayerInfo>();
            if (playerInfo != null && !playerInfo.isProfileLoaded)
                playerInfo.InitializeFromSession(GameSession.Instance);
        }

        stageManager = FindFirstObjectByType<StageManager>();
        if (stageManager != null)
            stageManager.StageSelect(true);
    }
}

