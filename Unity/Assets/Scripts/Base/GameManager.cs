using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : NetworkBehaviour // 혹은 일반 NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Flow Settings")]
    public NetworkVariable<float> gameTimer = new NetworkVariable<float>(0f);
    public float maxGameTime = 60f; // 시연용 1분 설정
    public bool isGameEnded = false;

    [Header("Shared Progress")]
    public NetworkVariable<int> sharedLevel = new NetworkVariable<int>(1);
    public NetworkVariable<int> sharedExp = new NetworkVariable<int>(0);
    public NetworkVariable<int> requiredExp = new NetworkVariable<int>(100);
    public NetworkVariable<int> availablePodPodTokens = new NetworkVariable<int>(0); // 파티 전체 보상 요청권 개수

    [Header("EXP Pool Settings")]
    public GameObject expPrefab;
    private ObjectPool<GameObject> expPool;

    [Header("Pod Management")]
    // 서버가 공인하는 "현재까지 지급된 보상 선택지 총 횟수"
    public NetworkVariable<int> globalRewardCount = new NetworkVariable<int>(0);
    
    public GameObject rewardPodPrefab;
    public GameObject healPodPrefab;

    [Header("Pod Pool Settings")]
    private ObjectPool<GameObject> rewardPodPool;
    private ObjectPool<GameObject> healPodPool;
    public float spawnRadius = 3f;
    private int playersReadyCount = 0; // 보상 선택을 완료한 인원 수
    private bool isPaused = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 1. 경험치 오브젝트 풀 초기화
        expPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(expPrefab),
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: Destroy,
            defaultCapacity: 50,
            maxSize: 300
        );

        // 2. 보상 포드 풀 초기화 (최대 2~5개 순환)
        rewardPodPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(rewardPodPrefab),
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: Destroy,
            defaultCapacity: 2,
            maxSize: 5
        );

        // 3. 회복 포드 풀 초기화
        healPodPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(healPodPrefab),
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: Destroy,
            defaultCapacity: 2,
            maxSize: 5
        );
    }

    private void Update()
    {
        if (!IsServer || isGameEnded) return;

        // 게임 시간 증가
        gameTimer.Value += Time.deltaTime;
    }

    // 플레이어들이 경험치를 먹었을 때 서버로 보냄
    [ServerRpc(RequireOwnership = false)]
    public void AddExperienceServerRpc(int amount)
    {
        sharedExp.Value += amount;
        if (sharedExp.Value >= requiredExp.Value)
        {
            sharedExp.Value -= requiredExp.Value;
            sharedLevel.Value++;
            availablePodPodTokens.Value++; // 레벨업 시 보상 요청권 지급
            requiredExp.Value = Mathf.RoundToInt(requiredExp.Value * 1.2f); // 필요 경험치 증가
            ApplyLevelUpBonusRpc();
        }
    }
    [Rpc(SendTo.ClientsAndHost)]
    private void ApplyLevelUpBonusRpc()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            PlayerStats localStats = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerStats>();
            if (localStats != null)
            {
                // 기본 배율(1.0)에 0.05(5%)씩 누적 합산합니다.
                localStats.attackPowerMultiplier += 0.05f; 
            }
        }
    }

    // 몬스터가 죽을 때 호출할 경험치 스폰 함수
    public void SpawnExp(Vector3 position, int amount)
    {
        if (!IsServer) return;

        GameObject expObj = expPool.Get();
        expObj.transform.position = position;

        ExpObject expScript = expObj.GetComponent<ExpObject>();
        if (expScript != null)
        {
            expScript.Setup(expPool, amount);
        }

        expObj.GetComponent<NetworkObject>().Spawn();
    }

    // 플레이어가 포드 호출을 요청했을 때 서버에서 판정 및 스폰
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestPodSpawnRpc(Vector3 playerPosition, int playerCallCount)
    {
        // 토큰이 없으면 실행 불가
        if (availablePodPodTokens.Value <= 0) return;
        
        // 토큰 1개 소비
        availablePodPodTokens.Value--;

        ObjectPool<GameObject> targetPool = null;

        // 판정에 따라 어떤 풀에서 꺼낼지 결정
        if (playerCallCount > globalRewardCount.Value)
        {
            globalRewardCount.Value = playerCallCount;
            targetPool = rewardPodPool;
        }
        else
        {
            targetPool = healPodPool;
        }

        if (targetPool == null) return;

        // Instantiate 대신 풀에서 꺼내옴
        GameObject podObj = targetPool.Get();
        // 플레이어 주변 작은 원 범위 내 랜덤 위치 계산 (Y축은 하늘 위로 고정)
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPosition = playerPosition + new Vector3(randomCircle.x, 20f, randomCircle.y);

        podObj.transform.position = spawnPosition;
        podObj.transform.rotation = Quaternion.identity;

        // 포드 스크립트 초기화 (재사용 준비)
        DropPod podScript = podObj.GetComponent<DropPod>();
        if (podScript != null)
        {
            podScript.Setup(targetPool);
        }
        podObj.GetComponent<NetworkObject>().Spawn();
        
    }

    // 보상 포드를 먹었을 때 호출 (DropPod.cs 에서 트리거함)
    public void StartRewardSequence()
    {
        playersReadyCount = 0;
        
        // 서버에서만 시간을 멈추던 로직을 지우고, 클라이언트 전원에게 시간 정지 및 UI 오픈을 지시합니다.
        OpenRewardUIRpc();
    }

    public void GameClear()
    {
        if (isGameEnded) return;
        isGameEnded = true;
        
        // ★ 씬 전환 전 마우스 잠금 해제
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        Debug.Log("서버: 엘리트 몹 처치. 5초 후 클리어 씬으로 전환합니다.");
        StartCoroutine(LoadSceneAfterDelay("ClearScene", 5f));
    }

    public void CheckGameOver()
    {
        if (isGameEnded) return;

        bool isAllDown = true;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                PlayerStats stats = client.PlayerObject.GetComponent<PlayerStats>();
                if (stats != null && !stats.isDown.Value)
                {
                    isAllDown = false;
                    break;
                }
            }
        }

        if (isAllDown)
        {
            isGameEnded = true;
            
            // ★ 씬 전환 전 마우스 잠금 해제
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("서버: 플레이어 전원 사망. 페이드아웃 후 패배 씬으로 전환합니다.");
            FadeOutRpc();
            StartCoroutine(LoadSceneAfterDelay("GameOverScene", 5f));
        }
    }
    public IEnumerator LoadSceneAfterDelay(string sceneName, float delay)
    {
        yield return new WaitForSecondsRealtime(delay); 

        // 1. 순수 클라이언트(2P)들에게 먼저 네트워크 종료 및 씬 이동을 명령합니다.
        OrderClientsToDisconnectRpc(sceneName);

        // 2. 호스트(서버)는 해당 RPC 패킷이 클라이언트들에게 무사히 도착할 수 있도록 0.5초 대기합니다.
        yield return new WaitForSecondsRealtime(0.5f);

        // 3. 호스트 자신도 네트워크 세션을 닫고 씬을 이동합니다.
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        // 넷코드 씬 매니저가 아닌 유니티 기본 씬 매니저를 호출합니다.
        SceneManager.LoadScene(sceneName);
    }
    
   [Rpc(SendTo.ClientsAndHost)]
    public void OpenRewardUIRpc()
    {
        Time.timeScale = 0f;

        if (RewardUIManager.Instance != null)
        {
            RewardUIManager.Instance.OpenRewardUI();
        }
    }
    public void PlayerFinishedRewardSelection()
    {
        playersReadyCount++;

        if (playersReadyCount >= NetworkManager.Singleton.ConnectedClientsIds.Count)
        {
            playersReadyCount = 0;
            Debug.Log("서버: 모든 플레이어 보상 선택 완료. 게임 재개 명령을 하달합니다.");
            
            // 서버에서만 시간을 재개하던 로직을 지우고, 클라이언트 전원에게 시간 재개를 지시합니다.
            ResumeGameRpc();
        }
    }
    
    [Rpc(SendTo.ClientsAndHost)]
    public void FadeOutRpc()
    {
        if (PlayerHUD.Instance != null)
        {
            // ★ 신규 추가: 화면이 어두워지기 시작함과 동시에 게임 오버 대사 출력
            PlayerHUD.Instance.ShowGameOverAlert(); 
            PlayerHUD.Instance.StartFadeOut(5f);
        }
    }
    private void ResumeGameRpc()
    {
        Time.timeScale = 1f;
    }
    [Rpc(SendTo.NotServer)] // 호스트(서버)를 제외한 클라이언트들에게만 발송되는 RPC
    private void OrderClientsToDisconnectRpc(string sceneName)
    {
        // 수신한 클라이언트는 즉시 네트워크 세션을 강제 종료합니다. (플레이어 객체 자동 삭제)
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        // 로컬 환경의 결과 씬으로 이동합니다.
        SceneManager.LoadScene(sceneName);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestTogglePauseRpc()
    {
        if (isGameEnded) return;
        isPaused = !isPaused;
        ApplyPauseStateRpc(isPaused);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ApplyPauseStateRpc(bool pauseState)
    {
        Time.timeScale = pauseState ? 0f : 1f;
        if (PlayerHUD.Instance != null)
        {
            PlayerHUD.Instance.SetPauseUI(pauseState);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestAbortGameRpc()
    {
        if (isGameEnded) return;
        isGameEnded = true;
        Time.timeScale = 1f; // 페이드아웃 및 씬 전환 코루틴의 정상 작동을 위해 시간 흐름 재개

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("서버: 작전 중단 요청 접수. 게임 오버 씬으로 전환합니다.");
        FadeOutRpc();
        StartCoroutine(LoadSceneAfterDelay("GameOverScene", 5f));
    }
    
    
    

    
}