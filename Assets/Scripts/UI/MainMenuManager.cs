using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject modeSelectPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject creditsPanel;

    private void Start()
    {
        GameManager.OnGameStateChanged += HandleGameStateChanged;
        ShowMainMenu();
    }

    private void OnDestroy()
    {
        GameManager.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameState state)
    {
        // When the game starts, turn off the ENTIRE static canvas to save draw calls!
        if (state == GameState.Countdown || state == GameState.ActiveMatch)
        {
            gameObject.SetActive(false);
        }
        else if (state == GameState.Lobby)
        {
            gameObject.SetActive(true);
            ShowMainMenu();
        }
    }

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        modeSelectPanel.SetActive(false); // Hide it
        optionsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        UIManager.Instance.HideLobby(); // Ensure lobby is hidden when looking at main menu
    }

    public void ShowOptions()
    {
        mainMenuPanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    public void ShowCredits()
    {
        mainMenuPanel.SetActive(false);
        creditsPanel.SetActive(true);
    }

    public void OnPlayClicked()
    {
        // Hide the main menu panels, and tell UIManager to show the Lobby panel
        mainMenuPanel.SetActive(false);
        modeSelectPanel.SetActive(true);
    }
    public void OnSelectPCMode()
    {
        GameManager.Instance.SetPlayMode(GameManager.PlayMode.PCLocal);
        modeSelectPanel.SetActive(false);
        UIManager.Instance.ShowLobby(GameManager.PlayMode.PCLocal);
    }

    public void OnSelectAirConsoleMode()
    {
        GameManager.Instance.SetPlayMode(GameManager.PlayMode.AirConsole);
        modeSelectPanel.SetActive(false);
        UIManager.Instance.ShowLobby(GameManager.PlayMode.AirConsole);
    }

    public void OnQuitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}