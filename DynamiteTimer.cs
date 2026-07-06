using UnityEngine;

public class DynamiteTimer : MonoBehaviour
{
    public float EndTime;

    public float RemainingTime
    {
        get
        {
            return Mathf.Max(0, EndTime - Time.time);
        }
    }

    private void Update()
    {
        if (RemainingTime <= 0)
        {
            Destroy(this);
        }
    }
}