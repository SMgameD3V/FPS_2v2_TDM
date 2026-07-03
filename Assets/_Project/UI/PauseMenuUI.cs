using UnityEngine;
using Unity.Netcode;

public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private UnityEngine.UI.Button resumeButton;
    [SerializeField] private UnityEngine.UI.Button exitButton;

    private bool _isPaused = false;

    void Start()
    {
        pausePanel.SetActive(false);
        resumeButton.onClick.AddListener(ResumeGame);
        exitButton.onClick.AddListener(ExitToMenu);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_isPaused) ResumeGame();
            else PauseGame();
        }
    }

    void PauseGame()
    {
        _isPaused = true;
        pausePanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Disable local player input while paused
        var localObj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (localObj != null)
        {
            var controller = localObj.GetComponent<PlayerController>();
            if (controller != null) controller.enabled = false;
        }
    }

    void ResumeGame()
    {
        _isPaused = false;
        pausePanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var localObj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (localObj != null)
        {
            var controller = localObj.GetComponent<PlayerController>();
            if (controller != null) controller.enabled = true;
        }
    }

    void ExitToMenu()
    {
        SessionManager.Instance?.LeaveSession();
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}