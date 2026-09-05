using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class BladeDrone : WeaponBase
{
    [Header("Blade Drone Settings")]
    public GameObject dronePrefab; // 자식으로 생성될 실제 칼날 드론 프리팹
    public float orbitRadius = 3f; // 공전 반경
    public float baseRotationSpeed = 150f; // 기본 회전 속도

    private List<GameObject> activeDrones = new List<GameObject>();
    private float currentRotationSpeed;

    public override void Setup(PlayerStats stats)
    {
        base.Setup(stats);
    }

    protected override void ApplyLevelEffect()
    {
        int level = currentLevel.Value;
        
        // 1~4단계에서 드론 개수 산출, 5단계 이상은 4개로 고정
        int droneCount = Mathf.Clamp(level, 1, 4); 

        // 2단계 이상이면 회전 속도 1.5배 증가 기믹
        currentRotationSpeed = level >= 2 ? baseRotationSpeed * 1.5f : baseRotationSpeed;

        UpdateDroneCount(droneCount);
    }

    private void UpdateDroneCount(int count)
    {
        // 1. 기존 드론 초기화
        foreach (var drone in activeDrones)
        {
            if (drone != null) Destroy(drone);
        }
        activeDrones.Clear();

        // 2. 드론 균등 배치
        float angleStep = 360f / count;
        for (int i = 0; i < count; i++)
        {
            // 부모(본체)의 자식으로 드론 인스턴스화
            GameObject newDrone = Instantiate(dronePrefab, transform);
            
            // X, Z 축을 활용하여 평면 반경 좌표 계산
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbitRadius;
            
            newDrone.transform.localPosition = offset;
            
            // 드론 콜라이더 스크립트에 본체 권한 위임
            BladeDroneCollider colliderScript = newDrone.GetComponent<BladeDroneCollider>();
            if (colliderScript == null) colliderScript = newDrone.AddComponent<BladeDroneCollider>();
            
            colliderScript.Setup(this);
            
            activeDrones.Add(newDrone);
        }
    }

    void Update()
    {
        // 부모 객체 자체를 Y축 기준으로 회전시킵니다.
        transform.Rotate(Vector3.up * currentRotationSpeed * Time.deltaTime);
    }
}