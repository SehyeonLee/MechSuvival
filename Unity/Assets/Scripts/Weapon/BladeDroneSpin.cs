using UnityEngine;

public class BladeDroneSpin : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(0, 360 * Time.deltaTime,0);
    }
}
