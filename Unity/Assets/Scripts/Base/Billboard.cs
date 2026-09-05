using UnityEngine;

public class Billboard : MonoBehaviour
{
    private void LateUpdate()
    {
        if (Camera.main != null)
        {
            // UI가 항상 메인 카메라를 향하도록 회전
            transform.LookAt(transform.position + Camera.main.transform.forward);
        }
    }
}
