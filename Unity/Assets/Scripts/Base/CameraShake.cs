using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private Vector3 originalLocalPos;
    private Coroutine shakeCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 외부에서 호출할 공용 함수 (시간, 세기 지정 가능)
    public void Shake(float duration, float magnitude)
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }
        shakeCoroutine = StartCoroutine(ShakeCoroutine(duration, magnitude));
    }

    private IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        originalLocalPos = transform.localPosition;
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            // 무작위 좌표 변위 계산
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            transform.localPosition = new Vector3(originalLocalPos.x + x, originalLocalPos.y + y, originalLocalPos.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalLocalPos;
        shakeCoroutine = null;
    }
}