using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

public class OrbitalDroneWeapon : WeaponBase
{
    [Header("Orbit Settings")]
    public Transform droneVisual; // 궤도를 도는 실제 드론 모델 객체
    public float orbitRadius = 3f;
    public float orbitHeight = 1f; // 플레이어보다 높은 위치를 위한 Y축 오프셋
    public float orbitSpeed = 120f; 

    [Header("Combat Settings")] 
    public Transform firePoint;
    public GameObject projectilePrefab; // BulletProjectile이 부착된 기존 총알 프리팹 사용 가능
    public float baseFireRate = 1.5f;
    public float scanRadius = 15f; 
    public float scanAngle = 60f; // 진행 방향 기준 전방 시야각
    public LayerMask enemyLayer;

    [Header("Audio Settings")]
    public AudioClip fireSoundClip;
    private AudioSource fireAudioSource;

    private float currentFireRate;
    private float nextFireTime = 0f;
    private ObjectPool<GameObject> projectilePool;

    private void Awake()
    {
        projectilePool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(projectilePrefab),
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: Destroy,
            defaultCapacity: 15,
            maxSize: 50
        );
        fireAudioSource = GetComponent<AudioSource>();
    }

    public override void Setup(PlayerStats stats)
    {
        base.Setup(stats); 
        
        // 피벗 자체는 플레이어 중심축(로컬 0,0,0)에 고정합니다.
        transform.localPosition = Vector3.zero;
        
        // 드론 모델을 지정된 반경과 높이로 밀어내어 공전 궤도를 형성합니다.
        if (droneVisual != null)
        {
            droneVisual.localPosition = new Vector3(orbitRadius, orbitHeight, 0f);
        }
    }

    protected override void ApplyLevelEffect()
    {
        int level = currentLevel.Value;
        
        // 레벨업 시 발사 주기 단축 및 궤도 공전 속도 증가
        if (level == 1) { currentFireRate = baseFireRate; orbitSpeed = 120f; }
        else if (level == 2) { currentFireRate = baseFireRate * 0.8f; orbitSpeed = 150f; }
        else if (level == 3) { currentFireRate = baseFireRate * 0.6f; orbitSpeed = 180f; }
        else if (level >= 4) { currentFireRate = baseFireRate * 0.45f; orbitSpeed = 220f; }
    }

    void Update()
    {
        if (!IsOwner) return;
        if (playerStats != null && playerStats.isDown.Value) return;

        // 1. 피벗 회전을 통한 드론 공전 연산
        transform.Rotate(Vector3.up * orbitSpeed * Time.deltaTime);

        // 2. 스캔 및 사격 로직
        if (Time.time >= nextFireTime)
        {
            Transform target = FindTargetInFront();
            if (target != null)
            {
                nextFireTime = Time.time + currentFireRate;

                // 타겟을 향한 방향 벡터 산출 (드론이 더 높으므로 자연스럽게 하향 조준 각도가 형성됨)
                Vector3 fireDirection = (target.position - firePoint.position).normalized;
                Quaternion correctedRotation = Quaternion.LookRotation(fireDirection);

                FireRpc(firePoint.position, correctedRotation);
            }
        }
    }

    private Transform FindTargetInFront()
    {
        Collider[] hitEnemies = Physics.OverlapSphere(droneVisual.position, scanRadius, enemyLayer);
        Transform bestTarget = null;
        float closestDistance = Mathf.Infinity;

        // 드론의 순간 이동 방향(접선 벡터) 산출
        Vector3 dirFromCenter = (droneVisual.position - transform.position).normalized;
        dirFromCenter.y = 0; 
        Vector3 tangentForward = Vector3.Cross(Vector3.up, dirFromCenter).normalized;

        foreach (var col in hitEnemies)
        {
            if (col.TryGetComponent<EnemyBase>(out var enemy) && enemy.currentHealth.Value > 0)
            {
                Vector3 dirToEnemy = (col.transform.position - droneVisual.position).normalized;
                
                // 접선 벡터(진행 방향)를 기준으로 설정된 시야각(scanAngle) 내에 적이 있는지 판별
                float angle = Vector3.Angle(tangentForward, dirToEnemy);
                if (angle <= scanAngle)
                {
                    float dist = Vector3.Distance(droneVisual.position, col.transform.position);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        bestTarget = col.transform;
                    }
                }
            }
        }
        return bestTarget;
    }

    [Rpc(SendTo.Server)]
    private void FireRpc(Vector3 spawnPos, Quaternion spawnRot)
    {
        GameObject projObj = projectilePool.Get();
        
        projObj.transform.position = spawnPos;
        projObj.transform.rotation = spawnRot;

        // 기존의 일직선 투사체 클래스를 호환 사용
        BulletProjectile projectile = projObj.GetComponent<BulletProjectile>();
        if (projectile != null)
        {
            projectile.Setup(projectilePool, GetFinalDamage());
        }

        projObj.GetComponent<NetworkObject>().Spawn();
        PlayFireSoundRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayFireSoundRpc()
    {
        if (fireAudioSource != null && fireSoundClip != null)
        {
            fireAudioSource.PlayOneShot(fireSoundClip);
        }
    }
}