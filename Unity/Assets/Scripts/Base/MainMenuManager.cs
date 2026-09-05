using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject modeSelectPanel; 
    public GameObject roleSelectPanel; 
    public GameObject statusPanel;     
    [Header("Tutorial UI")]
    public GameObject tutorialPanel; // 조작법 안내 패널
    [Header("Credits UI")]
    public GameObject creditsPanel;  // ★ 신규 추가: 에셋 출처 표기 패널

    [Header("UI Elements")]
    public TextMeshProUGUI statusText;

    private bool isSinglePlayer = false;
    public GameObject MainSceneCamera;

    private void Start()
    {
        // 씬 진입 시 커서 활성화 보장
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ShowPanel(modeSelectPanel);
    }

    private void OnDestroy()
    {
        // 씬이 전환되거나 파괴될 때 이벤트 구독 해제 (메모리 누수 방지)
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    // --- 1단계: 모드 선택 ---

    public void OnClickSinglePlayer()
    {
        isSinglePlayer = true;
        if (NetworkManager.Singleton.StartHost())
        {
            ShowPanel(statusPanel);
            // 싱글플레이는 대기 없이 즉시 카운트다운 돌입
            StartCoroutine(CountdownAndStart());
        }
    }

    public void OnClickMultiPlayer()
    {
        isSinglePlayer = false;
        ShowPanel(roleSelectPanel);
    }

    // --- 2단계: 멀티플레이 역할 선택 ---

    public void OnClickHost()
    {
        if (NetworkManager.Singleton.StartHost())
        {
            ShowPanel(statusPanel);
            statusText.text = "클라이언트(2P) 접속 대기 중...";
            
            // 클라이언트 접속 감지 이벤트 구독
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    public void OnClickClient()
    {
        if (NetworkManager.Singleton.StartClient())
        {
            ShowPanel(statusPanel);
            // 클라이언트는 카운트다운을 따로 연산하지 않고 호스트가 씬을 로드할 때까지 대기합니다.
            statusText.text = "서버에 접속 중... 호스트의 시작을 대기합니다.";
        }
    }

    // --- 네트워크 콜백 및 시작 연산 ---

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer && !isSinglePlayer)
        {
            if (clientId != NetworkManager.ServerClientId)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected; 
                StartCoroutine(CountdownAndStart());
            }
        }
    }

    private IEnumerator CountdownAndStart()
    {
        MainSceneCamera.SetActive(false); 
        for (int i = 3; i > 0; i--)
        {
            statusText.text = $"게임이 {i}초 뒤에 시작됩니다...";
            yield return new WaitForSeconds(1f);
        }

        statusText.text = "시스템 가동. 전장으로 진입합니다.";
        yield return new WaitForSeconds(0.5f);

        NetworkManager.Singleton.SceneManager.LoadScene("InGameScene", LoadSceneMode.Single);
    }

    // 패널 스위칭 보조 함수
    private void ShowPanel(GameObject panelToShow)
    {
        modeSelectPanel.SetActive(false);
        tutorialPanel.SetActive(false);
        roleSelectPanel.SetActive(false);
        statusPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false); // ★ 크레딧 패널 예외 처리 추가

        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
        }
    }

    public void OnClickOpenTutorial()
    {
        ShowPanel(tutorialPanel);
    }

    public void OnClickCloseTutorial()
    {
        ShowPanel(modeSelectPanel);
    }

    public void OnClickStartTutorial()
    {
        if (NetworkManager.Singleton.StartHost())
        {
            statusText.text = "튜토리얼 세션 가동 중...";
            ShowPanel(statusPanel);
            NetworkManager.Singleton.SceneManager.LoadScene("TutorialScene", LoadSceneMode.Single);
        }
    }

    // ★ 신규 추가: 크레딧 열기 버튼 이벤트
    public void OnClickOpenCredits()
    {
        ShowPanel(creditsPanel);
    }

    // ★ 신규 추가: 크레딧 닫기(뒤로가기) 버튼 이벤트
    public void OnClickCloseCredits()
    {
        ShowPanel(modeSelectPanel);
    }

    public void OnClickQuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}