
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private const string BEST_SCORE_KEY = "EndlessRunner_BestScore";

    [Header("Game Settings")]
    public float startSpeed = 8f;
    public float maxSpeed = 18f;
    public float speedIncrease = 0.25f;

    public float CurrentSpeed { get; private set; }
    public bool IsPlaying { get; private set; }

    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public int Coins { get; private set; }

    [Header("Main Menu UI")]
    public GameObject mainMenuPanel;
    public TMP_Text menuBestScoreText;

    [Header("HUD UI")]
    public GameObject hudPanel;
    public TMP_Text scoreText;
    public TMP_Text bestScoreHUDText;
    public TMP_Text coinText;

    [Header("Pause UI")]
    public GameObject pausePanel;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TMP_Text finalScoreText;
    public TMP_Text bestScoreText;
    public TMP_Text newRecordText;

    private bool isPaused;
    private bool newRecordThisRun;
    private float scoreTimer;

    private static bool startImmediatelyAfterReload;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BestScore = PlayerPrefs.GetInt(BEST_SCORE_KEY, 0);
    }

    private void Start()
    {
        Time.timeScale = 1f;

        if (startImmediatelyAfterReload)
        {
            startImmediatelyAfterReload = false;
            StartGame();
        }
        else
        {
            ShowMainMenu();
        }

        UpdateBestScoreUI();
    }

    private void Update()
    {
        if (!IsPlaying || isPaused)
            return;

        CurrentSpeed = Mathf.Min(
            CurrentSpeed + speedIncrease * Time.deltaTime,
            maxSpeed
        );

        // Tang 10 diem moi giay
        scoreTimer += Time.deltaTime;

        if (scoreTimer >= 1f)
        {
            Score += 10;
            scoreTimer -= 1f;
            CheckBestScore();
            UpdateScoreUI();
        }
    }

    public void StartGame()
    {
        Time.timeScale = 1f;

        Score = 0;
        Coins = 0;
        scoreTimer = 0f;
        CurrentSpeed = startSpeed;

        IsPlaying = true;
        isPaused = false;
        newRecordThisRun = false;

        SetPanel(mainMenuPanel, false);
        SetPanel(gameOverPanel, false);
        SetPanel(pausePanel, false);
        SetPanel(hudPanel, true);

        if (newRecordText != null)
            newRecordText.gameObject.SetActive(false);

        UpdateScoreUI();
        UpdateBestScoreUI();
        UpdateCoinUI();
    }

    public void AddScore(int amount)
    {
        if (!IsPlaying || isPaused)
            return;

        Score += amount;
        CheckBestScore();
        UpdateScoreUI();
    }

    public void AddCoin(int amount)
    {
        if (!IsPlaying || isPaused)
            return;

        Coins += amount;
        AddScore(amount * 10);
        UpdateCoinUI();
    }

    private void CheckBestScore()
    {
        if (Score > BestScore)
        {
            BestScore = Score;
            newRecordThisRun = true;

            PlayerPrefs.SetInt(BEST_SCORE_KEY, BestScore);
            PlayerPrefs.Save();

            UpdateBestScoreUI();
        }
    }

    public void GameOver()
    {
        if (!IsPlaying)
            return;

        IsPlaying = false;
        isPaused = false;

        PlayerPrefs.Save();

        SetPanel(hudPanel, false);
        SetPanel(pausePanel, false);
        SetPanel(gameOverPanel, true);

        if (finalScoreText != null)
            finalScoreText.text = "YOUR SCORE: " + Score;

        if (bestScoreText != null)
            bestScoreText.text = "BEST SCORE: " + BestScore;

        if (newRecordText != null)
        {
            newRecordText.gameObject.SetActive(newRecordThisRun);
            newRecordText.text = "NEW RECORD!";
        }
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameOver();
        UpdateBestScoreUI();

        Time.timeScale = 0f;
    }

    public void PauseGame()
    {
        if (!IsPlaying || isPaused)
            return;

        isPaused = true;
        SetPanel(pausePanel, true);
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        if (!isPaused)
            return;

        isPaused = false;
        SetPanel(pausePanel, false);
        Time.timeScale = 1f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        startImmediatelyAfterReload = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void BackToMenu()
    {
        Time.timeScale = 1f;
        startImmediatelyAfterReload = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ExitGame()
    {
        PlayerPrefs.Save();

    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }

    private void ShowMainMenu()
    {
        IsPlaying = false;
        isPaused = false;

        SetPanel(mainMenuPanel, true);
        SetPanel(hudPanel, false);
        SetPanel(pausePanel, false);
        SetPanel(gameOverPanel, false);
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = "SCORE: " + Score;
    }

    private void UpdateBestScoreUI()
    {
        if (menuBestScoreText != null)
            menuBestScoreText.text = "BEST SCORE: " + BestScore;

        if (bestScoreHUDText != null)
            bestScoreHUDText.text = "BEST: " + BestScore;
    }

    private void UpdateCoinUI()
    {
        if (coinText != null)
            coinText.text = "COINS: " + Coins;
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }
}
