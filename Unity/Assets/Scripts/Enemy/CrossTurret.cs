using Unity.Netcode;
using UnityEngine;

public class CrossTurret : EnemyBase
{
    // (전역 변수 선언부에 아래 변수를 추가하십시오.)
    [Header("Turret Settings")]
    public int damage = 5; 
    public float fireRate = 1f; 
    public Transform[] firePoints;

    [Header("Audio Settings")]
    public AudioClip fireSoundClip; 

    private float nextFireTime = 0f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
        }
    }

    protected override void Update()
    {
        base.Update(); 

        if (!IsServer || isDead) return;

        if (Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + (1f / fireRate);
            Shoot();
        }
    }

    private void Shoot()
    {
        foreach (Transform t in firePoints)
        {
            EnemySpawner.Instance.SpawnEnemyBullet(t.position, t.rotation, damage);
        }
        
        PlayShootSoundRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayShootSoundRpc()
    {
        // ★ 중앙 사운드 관리자로 출력 권한 위임
        if (SFXManager.Instance != null && fireSoundClip != null)
        {
            SFXManager.Instance.PlaySFX(fireSoundClip, transform.position, 0.035f);
        }
    }

    protected override void Die()
    {
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
        }
    }
}