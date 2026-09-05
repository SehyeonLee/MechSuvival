using UnityEngine;
using UnityEngine.UI;

public class RadarSystem : MonoBehaviour
{
    [Header("Radar Settings")]
    public float radarRadius = 15f; // 레이더 감지 반경 (미터)
    public LayerMask enemyLayer;    // Enemy 레이어만 스캔하도록 설정

    [Header("Radar UI Images")]
    public Image frontRadar;
    public Image backRadar;
    public Image leftRadar;
    public Image rightRadar;

    private Transform playerTransform;

    public void Setup(Transform player)
    {
        playerTransform = player;
    }

    void Update()
    {
        if (playerTransform == null) return;

        // 1. 위협도 초기화 (0: 안전/투명, 1: 매우 근접/진한 빨강)
        float frontThreat = 0f;
        float backThreat = 0f;
        float leftThreat = 0f;
        float rightThreat = 0f;

        // 2. 반경 내 적 탐색
        Collider[] hitEnemies = Physics.OverlapSphere(playerTransform.position, radarRadius, enemyLayer);

        foreach (var enemy in hitEnemies)
        {
            // 플레이어에서 적을 향하는 벡터 계산
            Vector3 dirToEnemy = enemy.transform.position - playerTransform.position;
            float distance = dirToEnemy.magnitude;
            
            if (distance > radarRadius) continue;

            // 거리에 따른 위협도 계산 (가까울수록 1에 가까워짐)
            float threat = 1f - (distance / radarRadius);

            // 3. 상대 각도 계산 (Y축 높낮이는 무시하고 평면 기준으로만 계산)
            dirToEnemy.y = 0; 
            // 플레이어의 정면(forward)을 기준으로 적이 어느 각도에 있는지 (-180도 ~ 180도)
            float angle = Vector3.SignedAngle(playerTransform.forward, dirToEnemy.normalized, Vector3.up);

            // 4. 각도에 따라 4방향 위협도 갱신 (해당 방향에 여러 적이 있다면 가장 가까운 위협도를 유지)
            if (angle >= -45f && angle <= 45f)
                frontThreat = Mathf.Max(frontThreat, threat);
            else if (angle > 45f && angle < 135f)
                rightThreat = Mathf.Max(rightThreat, threat);
            else if (angle < -45f && angle > -135f)
                leftThreat = Mathf.Max(leftThreat, threat);
            else // -135 이하 또는 135 이상 (후방)
                backThreat = Mathf.Max(backThreat, threat);
        }

        // 5. UI 색상(투명도) 적용
        ApplyRadarColor(frontRadar, frontThreat);
        ApplyRadarColor(backRadar, backThreat);
        ApplyRadarColor(leftRadar, leftThreat);
        ApplyRadarColor(rightRadar, rightThreat);
    }

    private void ApplyRadarColor(Image radarImage, float threatLevel)
    {
        if (radarImage == null) return;
        
        // 투명도(Alpha)를 위협도 수치로 조절
        Color color = Color.red;
        color.a = threatLevel; 
        radarImage.color = color;
    }
}