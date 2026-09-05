using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResultSceneManager : MonoBehaviour
{
    private void Start()
    {
        // 결과 화면 진입 시 마우스 커서 잠금을 확실하게 해제
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ReturnToMainMenu()
    {
        // 활성화된 네트워크 세션 강제 종료
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // 인덱스 0번인 메인 메뉴 씬으로 복귀
        SceneManager.LoadScene("MainMenuScene");
    }
}