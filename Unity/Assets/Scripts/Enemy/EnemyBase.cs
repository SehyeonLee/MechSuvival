using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;
using UnityEngine.UI; 
using System.Collections; 

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBase : NetworkBehaviour
{
    [Header("Base Stats")]
    public int maxHealth = 50;
    public int expReward = 15;
    public float targetUpdateInterval = 0.5f;

    [Header("UI Settings")]
    public GameObject hpCanvas; 
    public Slider hpSlider;     
    public float hpVisibleDuration = 3f; 

    public NetworkVariable<int> currentHealth = new NetworkVariable<int>();
    public NetworkVariable<int> currentMaxHealth = new NetworkVariable<int>(); 
    
    protected NavMeshAgent agent;
    protected Transform currentTarget;
    protected ObjectPool<GameObject> myPool;
    
    private float nextTargetUpdateTime = 0f;
    private float lastDamageTime = -10f;
    protected bool isDead = false; 

    [Header("Elite Settings")]
    public bool isElite = false;

    [Header("Hit Feedback")]
    private Renderer[] renderers;
    private Color[] originalColors;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        
        // 모델링의 초기 색상 데이터를 수집합니다.
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
        }
    }

    public override void OnNetworkSpawn()
    {
        currentHealth.OnValueChanged += OnHealthChanged;
        
        if (!IsServer)
        {
            agent.enabled = false;
        }

        if (hpCanvas != null)
        {
            hpCanvas.SetActive(false);
        }
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;
    }

    public virtual void Setup(ObjectPool<GameObject> pool, float hpMultiplier)
    {
        myPool = pool;
        isDead = false;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        transform.rotation = Quaternion.identity;

        // 풀에서 꺼낼 때 피격 색상을 원본으로 복구합니다.
        if (renderers != null && originalColors != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].material.color = originalColors[i];
            }
        }

        if (IsServer)
        {
            // 서버의 경우 비활성화했던 NavMeshAgent를 다시 가동합니다.
            if (agent != null) 
            {
                agent.enabled = true;
                if (agent.isOnNavMesh) agent.isStopped = false;
            }
            
            int scaledMaxHealth = Mathf.RoundToInt(maxHealth * hpMultiplier);
            currentMaxHealth.Value = scaledMaxHealth;
            currentHealth.Value = scaledMaxHealth;
        }
    }

    protected virtual void Update()
    {
        if (IsServer && !isDead)
        {
            UpdateTarget();
        }

        if (hpCanvas != null && hpCanvas.activeSelf)
        {
            if (Time.time - lastDamageTime > hpVisibleDuration)
            {
                hpCanvas.SetActive(false);
            }
        }
    }

    private void UpdateTarget()
    {
        if (Time.time < nextTargetUpdateTime) return;
        nextTargetUpdateTime = Time.time + targetUpdateInterval;

        float shortestDistance = Mathf.Infinity;
        Transform closestPlayer = null;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            PlayerStats stats = client.PlayerObject.GetComponent<PlayerStats>();
            if (stats == null || stats.isDown.Value) continue;

            float distance = Vector3.Distance(transform.position, stats.transform.position);
            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                closestPlayer = stats.transform;
            }
        }
        currentTarget = closestPlayer;
    }

    public void TakeDamage(int damage)
    {
        if (!IsServer || isDead) return;

        currentHealth.Value -= damage;
        
        if (currentHealth.Value > 0)
        {
            // 생존 시 피격 깜빡임 연출을 호출합니다.
            TriggerHitFlashRpc();
        }
        else
        {
            isDead = true;
            Die();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerHitFlashRpc()
    {
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(HitFlashRoutine());
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].material.color = Color.red;
        }
        
        yield return new WaitForSeconds(0.1f);
        
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].material.color = originalColors[i];
        }
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = currentMaxHealth.Value; 
            hpSlider.value = newValue;
        }
        
        if (hpCanvas != null && !isDead)
        {
            hpCanvas.SetActive(true);
        }
        lastDamageTime = Time.time;
    }

    protected virtual void Die()
    {
        if (IsServer)
        {
            if (isElite) 
            {
                GameManager.Instance.GameClear();
            }
            else 
            {
                GameManager.Instance.SpawnExp(transform.position, expReward);
            }

            TriggerDeathSequenceRpc();
            StartCoroutine(DeathRoutine());
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerDeathSequenceRpc()
    {
        // 1. NavMeshAgent의 강제 기립 연산을 차단합니다.
        if (agent != null) agent.enabled = false; 
        
        // 2. 물리 충돌 콜라이더를 비활성화합니다.
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 3. 체력바를 은닉합니다.
        if (hpCanvas != null) hpCanvas.SetActive(false);

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(FallOverRoutine());
        }
    }

    private IEnumerator FallOverRoutine()
    {
        float elapsed = 0f;
        float duration = 0.25f; 
        
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = startRot * Quaternion.Euler(90f, 0f, 0f); 

        while(elapsed < duration)
        {
            transform.rotation = Quaternion.Slerp(startRot, targetRot, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.rotation = targetRot;
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(1.5f); 

        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj.IsSpawned)
        {
            netObj.Despawn(false);
        }
        
        if (myPool != null)
        {
            myPool.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}