using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(NetworkObject))]
public class ExpObject : NetworkBehaviour
{
    [Header("Magnet Settings")]
    public float magnetRadius = 5f; // 자석처럼 끌려가기 시작하는 반경
    public float initialMoveSpeed = 5f; // 초기 이동 속도
    public float acceleration = 20f; // 초당 가속도

    private int expAmount = 10;
    private ObjectPool<GameObject> myPool;
    private Transform targetPlayer = null;
    private float currentMoveSpeed;

    // GameManager가 풀에서 꺼내 배치할 때 호출
    public void Setup(ObjectPool<GameObject> pool, int amount)
    {
        myPool = pool;
        expAmount = amount;
        targetPlayer = null; // 재사용 시 타겟 초기화
        currentMoveSpeed = initialMoveSpeed;
    }

    void Update()
    {
        // 젬이 제자리에서 회전하는 시각적 효과 (서버/클라이언트 공통)
        transform.Rotate(Vector3.up, 90 * Time.deltaTime);

        // 이동 및 추적 연산은 서버에서 전담합니다.
        if (!IsServer) return;

        if (targetPlayer == null)
        {
            FindClosestPlayerInRadius();
        }
        else
        {
            // 타겟을 향해 이동
            Vector3 targetPos = targetPlayer.position;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, currentMoveSpeed * Time.deltaTime);
            
            // 끌려가는 동안 속도가 점점 빨라지는 가속도 연산
            currentMoveSpeed += acceleration * Time.deltaTime;
        }
    }

    private void FindClosestPlayerInRadius()
    {
        float closestDist = magnetRadius;
        Transform closest = null;

        // 접속 중인 모든 플레이어와의 거리를 계산하여 가장 가까운 대상 탐색
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                // 플레이어가 다운 상태면 경험치가 끌려가지 않도록 예외 처리
                if (client.PlayerObject.TryGetComponent<PlayerStats>(out var stats) && stats.isDown.Value) 
                    continue;

                float dist = Vector3.Distance(transform.position, client.PlayerObject.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = client.PlayerObject.transform;
                }
            }
        }

        // 반경 내에 플레이어가 확인되면 타겟으로 고정
        if (closest != null)
        {
            targetPlayer = closest;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            if (other.TryGetComponent<PlayerStats>(out var playerStats))
            {
                // 다운된 플레이어에게는 경험치가 흡수되지 않고 통과하도록 처리
                if (!playerStats.isDown.Value)
                {
                    playerStats.EarnExp(expAmount);
                    ReturnToPool();
                }
            }
        }
    }

    private void ReturnToPool()
    {
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj.IsSpawned)
        {
            netObj.Despawn(false); 
        }

        if (myPool != null)
        {
            myPool.Release(gameObject);
        }
    }
}