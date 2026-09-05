using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class EnemyElite : EnemyBase
{
    [Header("Elite Spawn Settings")]
    public int mobSpawnCountPerPhase = 8;

    [Header("Pattern Objects & Effects")]
    public Transform firePoint;
    public GameObject crossTurretPrefab; 
    public Transform rotationalBeamPivot;
    public GameObject explosionEffectPrefab; 

    [Header("Audio Settings")]
    public AudioSource localAudioSource; 
    public AudioClip dropSoundClip;       
    public AudioClip aimSoundClip;        
    public AudioClip fireSoundClip;       
    public AudioClip telegraphSoundClip;  
    public AudioClip laserLoopSoundClip;  
    public AudioClip explosionSoundClip;  

    private bool hp75Triggered = false;
    private bool hp50Triggered = false;
    private bool hp25Triggered = false;

    private bool isExecutingPattern = false;
    private float patternCooldown = 4f; 
    private bool hasLanded = false;
    public int bulletDamage = 10;

    // ★ 패턴 제어 전용 코루틴 추적 변수
    private Coroutine mainPatternLoop;
    private Coroutine activeSubPattern;

    protected override void Awake()
    {
        base.Awake();
        isElite = true; 
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            if (agent != null) agent.enabled = false;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            // 패턴 메인 루프 코루틴을 변수에 할당하여 시작
            mainPatternLoop = StartCoroutine(PatternLoop());
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || hasLanded) return;

        if (collision.gameObject.CompareTag("Ground"))
        {
            hasLanded = true;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true; 
            }

            if (agent != null)
            {
                agent.enabled = true;
                agent.isStopped = true; 
            }

            TriggerLandingEffectsRpc();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerLandingEffectsRpc()
    {
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.6f, 0.4f);
        if (localAudioSource != null && dropSoundClip != null) localAudioSource.PlayOneShot(dropSoundClip);
    }

    protected override void Update()
    {
        base.Update();
        if (!IsServer || isDead) return;

        CheckHPPhase();
    }

    private void CheckHPPhase()
    {
        if (currentMaxHealth.Value <= 0) return;

        float hpPercent = (float)currentHealth.Value / currentMaxHealth.Value;

        if (hpPercent <= 0.75f && !hp75Triggered) { TriggerPhaseSpawn(); hp75Triggered = true; }
        if (hpPercent <= 0.50f && !hp50Triggered) { TriggerPhaseSpawn(); hp50Triggered = true; }
        if (hpPercent <= 0.25f && !hp25Triggered) { TriggerPhaseSpawn(); hp25Triggered = true; }
    }

    private void TriggerPhaseSpawn()
    {
        EnemySpawner.Instance.SpawnMeleeEnemiesAround(transform.position, mobSpawnCountPerPhase);
    }

    private IEnumerator PatternLoop()
    {
        while (!hasLanded) yield return null;

        yield return new WaitForSeconds(3f); 

        while (!isDead)
        {
            if (!isExecutingPattern && currentTarget != null)
            {
                isExecutingPattern = true;
                int randomPattern = Random.Range(1, 4); 

                // ★ 실행되는 서브 패턴 코루틴을 추적 변수에 할당
                switch (randomPattern)
                {
                    case 1: activeSubPattern = StartCoroutine(Pattern1_ContinuousFire()); break;
                    case 2: activeSubPattern = StartCoroutine(Pattern2_CrossTurrets()); break;
                    case 3: activeSubPattern = StartCoroutine(Pattern3_RotationalBeam()); break;
                }

                if (activeSubPattern != null) yield return activeSubPattern;

                isExecutingPattern = false;
                yield return new WaitForSeconds(patternCooldown);
            }
            yield return null;
        }
    }

    // [패턴 1: 연속 사격]
    private IEnumerator Pattern1_ContinuousFire()
    {
        ToggleLoopSoundRpc(true, 0); 

        float trackTime = 2.5f; 
        
        while (trackTime > 0)
        {
            if (currentTarget != null) LookAtTarget(currentTarget.position, 5f);
            trackTime -= Time.deltaTime;
            yield return null;
        }

        ToggleLoopSoundRpc(false, 0); 

        float fireDuration = 3f;
        float fireRate = 0.15f; 
        float nextFire = 0f;

        while (fireDuration > 0)
        {
            // 몸체는 수평으로만 플레이어를 지속 추적합니다.
            if (currentTarget != null) LookAtTarget(currentTarget.position, 8f);

            if (Time.time >= nextFire)
            {
                nextFire = Time.time + fireRate;
                
                Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.up * 2f;
                Quaternion spawnRot = transform.rotation;

                if (currentTarget != null)
                {
                    // 1. 플레이어 기체 스케일에 맞춰 타격점을 1.0f에서 0.5f로 하향 조정했습니다.
                    Vector3 targetPos = currentTarget.position;
                    
                    // 2. 애니메이터의 간섭을 받지 않도록 총구와 타겟 간의 절대 각도를 산출합니다.
                    Vector3 aimDir = (targetPos - spawnPos).normalized;
                    spawnRot = Quaternion.LookRotation(aimDir);
                }
                else if (firePoint != null)
                {
                    spawnRot = firePoint.rotation;
                }
                
                EnemySpawner.Instance.SpawnEnemyBullet(spawnPos, spawnRot, bulletDamage);
                PlayOneShotRpc(1);
            }
            fireDuration -= Time.deltaTime;
            yield return null;
        }
        
        yield return new WaitForSeconds(1f); 
    }

    // [패턴 2: 십자 포탑 배치]
    private IEnumerator Pattern2_CrossTurrets()
    {
        if (crossTurretPrefab != null)
        {
            Vector3[] offsets = { Vector3.forward * 5f, Vector3.back * 5f, Vector3.left * 5f, Vector3.right * 5f };
            foreach (var offset in offsets)
            {
                Vector3 spawnPos = transform.position + offset;
                GameObject turret = Instantiate(crossTurretPrefab, spawnPos, Quaternion.identity);
                turret.GetComponent<NetworkObject>().Spawn();
            }
        }
        yield return new WaitForSeconds(1f);
    }

    // [패턴 3: 방사형 회전 레이저]
    private IEnumerator Pattern3_RotationalBeam()
    {
        PlayOneShotRpc(2); 
        yield return new WaitForSeconds(2f); 

        SetPatternObjectActiveRpc(true); 
        ToggleLoopSoundRpc(true, 1); 
        
        float duration = 7f;
        while (duration > 0)
        {
            rotationalBeamPivot.Rotate(Vector3.up * 45f * Time.deltaTime);
            duration -= Time.deltaTime;
            yield return null;
        }

        SetPatternObjectActiveRpc(false);
        ToggleLoopSoundRpc(false, 1); 
    }

    private void LookAtTarget(Vector3 targetPos, float speed)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, speed * Time.deltaTime);
        }
    }

    // --- 오디오 및 시각 제어 RPC ---

    [Rpc(SendTo.ClientsAndHost)]
    private void SetPatternObjectActiveRpc(bool state)
    {
        if (rotationalBeamPivot != null) rotationalBeamPivot.gameObject.SetActive(state);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayOneShotRpc(int clipIndex)
    {
        if (localAudioSource == null) return;
        switch (clipIndex)
        {
            case 1: if (fireSoundClip != null) localAudioSource.PlayOneShot(fireSoundClip); break;
            case 2: if (telegraphSoundClip != null) localAudioSource.PlayOneShot(telegraphSoundClip); break;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ToggleLoopSoundRpc(bool play, int clipIndex)
    {
        if (localAudioSource == null) return;
        if (play)
        {
            AudioClip targetClip = null;
            if (clipIndex == 0) targetClip = aimSoundClip;
            else if (clipIndex == 1) targetClip = laserLoopSoundClip;

            if (targetClip != null)
            {
                localAudioSource.clip = targetClip;
                localAudioSource.loop = true;
                localAudioSource.Play();
            }
        }
        else
        {
            localAudioSource.Stop();
            localAudioSource.loop = false;
        }
    }

    // --- 엘리트 사망 오버라이드 ---

    protected override void Die()
    {
        if (IsServer)
        {
            // ★ 불특정 다수의 코루틴 정지(StopAllCoroutines)를 폐기하고 패턴 코루틴만 정확히 정지
            if (mainPatternLoop != null) StopCoroutine(mainPatternLoop);
            if (activeSubPattern != null) StopCoroutine(activeSubPattern);

            GameManager.Instance.GameClear();
            TriggerEliteDeathRpc();
            StartCoroutine(EliteDeathRoutine());
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerEliteDeathRpc()
    {
        if (agent != null) agent.enabled = false;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 패턴 강제 종료에 따른 시각 잔재 초기화 (회전 레이저 빔 비활성화)
        SetPatternObjectActiveRpc(false);

        // 루프 사운드 강제 종료
        if (localAudioSource != null)
        {
            localAudioSource.Stop();
            localAudioSource.loop = false;
        }

        // 폭발 이펙트 및 사운드 호출
        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        if (localAudioSource != null && explosionSoundClip != null) localAudioSource.PlayOneShot(explosionSoundClip);

        // 시체 숨김 처리
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = false;

        if (hpCanvas != null) hpCanvas.SetActive(false);

        // ★ 신규 추가: 오퍼레이터 작전 완료 메시지 출력
        if (PlayerHUD.Instance != null)
        {
            PlayerHUD.Instance.ShowEliteDefeatAlert();
        }
    }

    private IEnumerator EliteDeathRoutine()
    {
        yield return new WaitForSeconds(3f); 
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj.IsSpawned) netObj.Despawn(false);
        Destroy(gameObject);
    }
}