using Unity.Netcode;
using UnityEngine;

public class NetworkConnectUI : MonoBehaviour
{
    private void OnGUI()
    {
        if (NetworkManager.Singleton == null) return; 

        GUILayout.BeginArea(new Rect(10, 10, 300, 300));

        // 아직 네트워크 연결이 안 된 상태일 때만 버튼 표시
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("호스트로 시작 (서버+클라이언트 / 싱글플레이)", GUILayout.Width(250), GUILayout.Height(50)))
            {
                NetworkManager.Singleton.StartHost();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("클라이언트로 접속 (2P 모드)", GUILayout.Width(250), GUILayout.Height(50)))
            {
                NetworkManager.Singleton.StartClient();
            }
        }
        else
        {
            // 연결된 후에는 현재 상태 표시
            GUILayout.Label($"현재 모드: {(NetworkManager.Singleton.IsHost ? "호스트 (1P)" : "클라이언트 (2P)")}");
            
            if (GUILayout.Button("연결 종료", GUILayout.Width(100), GUILayout.Height(30)))
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        GUILayout.EndArea();
    }
}