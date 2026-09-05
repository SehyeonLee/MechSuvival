using Unity.Burst.Intrinsics;
using Unity.Netcode;
using UnityEngine;

public class PlasmaEmitter : WeaponBase
{
    [Header("Plasma Settings")]
    public Transform visualTransform; // 납작한 원기둥(장판)의 Transform
    public LayerMask enemyLayer;      // Enemy 레이어 지정

    private float currentRadius = 3f;
    private float tickRate = 1f;
    private float nextTickTime = 0f;

    public override void Setup(PlayerStats stats)
    {
        base.Setup(stats);
        transform.localPosition = new Vector3(0f, -0.45f, 0f); // 지면에서 약간 띄운 위치로 설정
    }

    protected override void ApplyLevelEffect()
    {
        int level = currentLevel.Value;

        // 레벨별 기믹 설정
        if (level == 1) 
        { 
            currentRadius = 3f; 
            tickRate = 1f; 
        }
        else if (level == 2) 
        { 
            currentRadius = 4f; 
            tickRate = 1f; 
        }
        else if (level == 3) 
        { 
            currentRadius = 4f; 
            tickRate = 0.7f; 
        }
        else if (level >= 4) 
        { 
            currentRadius = 6f; 
            tickRate = 0.7f; 
            // 둔화 효과는 추후 EnemyBase의 이동속도 제어 기능이 추가되면 연동 가능합니다.
        }

        // 시각적 모델의 스케일 조절 
        // (Y축은 0.05로 납작하게 고정, X와 Z축은 반경의 2배인 지름으로 설정)
        if (visualTransform != null)
        {
            visualTransform.localScale = new Vector3(currentRadius * 2f, 0.05f, currentRadius * 2f);
        }
    }

    void Update()
    {
        // 데미지 연산은 서버에서만 전담합니다.
        if (!IsServer) return;

        if (Time.time >= nextTickTime)
        {
            nextTickTime = Time.time + tickRate;
            ApplyTickDamage();
        }
    }

    private void ApplyTickDamage()
    {
        // 내 위치를 중심으로 반경(currentRadius) 내의 모든 적 스캔
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, currentRadius, enemyLayer);
        
        foreach (var col in hitEnemies)
        {
            if (col.TryGetComponent<EnemyBase>(out var enemy))
            {
                // 부모 클래스의 GetFinalDamage()를 호출하여 계산된 최종 데미지 적용
                enemy.TakeDamage(GetFinalDamage());
            }
        }
    }
}