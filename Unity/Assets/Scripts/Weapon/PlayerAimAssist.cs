using UnityEngine;

public class PlayerAimAssist : MonoBehaviour
{
    [Header("Aim Assist Settings")]
    public LayerMask targetLayers;     // 조준 감지 대상 레이어 (Enemy, Ground 등)
    public float maxAimDistance = 50f; // 최대 조준 보정 거리

    /// <summary>
    /// 화면 정중앙(크로스헤어) 기준으로 레이케스트를 수행하여 조준된 오브젝트와 좌표를 반환합니다.
    /// </summary>
    /// <param name="targetPoint">최종 조준 보정 좌표 출력 변수</param>
    /// <returns>조준된 Gameobject (빗나갈 시 null 반환)</returns>
    public GameObject GetTargetedObject(out Vector3 targetPoint)
    {
        if (Camera.main == null)
        {
            targetPoint = transform.position + transform.forward * maxAimDistance;
            return null;
        }

        Transform camTransform = Camera.main.transform;
        
        // 화면 정중앙에서 정면을 향하는 레이 생성
        Ray ray = new Ray(camTransform.position, camTransform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxAimDistance, targetLayers))
        {
            targetPoint = hit.point; // 충돌 지점을 타겟 포인트로 설정
            return hit.collider.gameObject; // 충돌한 개체 반환
        }
        else
        {
            // 빗나갔을 경우 카메라 정면 멀리 있는 가상의 지점을 지정하여 본래 사격 각도 유지
            targetPoint = camTransform.position + camTransform.forward * maxAimDistance;
            return null;
        }
    }
}