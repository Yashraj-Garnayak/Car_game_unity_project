using UnityEngine;

// Closed racing line. Index 0 is the start/finish line.
public class Track : MonoBehaviour
{
    public static Track I;
    public Vector3[] points;
    public Collider terrain;
    public float lateralGrip = 10f;
    [HideInInspector] public float[] safeSpeed;

    public int Count => points.Length;
    public Vector3 P(int i) => points[((i % Count) + Count) % Count];
    public Vector3 Dir(int i) => (P(i + 1) - P(i)).normalized;

    void Awake()
    {
        I = this;
        safeSpeed = new float[Count];
        for (int i = 0; i < Count; i++)
            safeSpeed[i] = Mathf.Clamp(Mathf.Sqrt(lateralGrip * Mathf.Min(Radius(i, 1), Radius(i, 2))), 12f, 60f);
    }

    float Radius(int i, int span)
    {
        Vector3 a = P(i - span), b = P(i), c = P(i + span);
        float area2 = Vector3.Cross(b - a, c - a).magnitude;
        return area2 < 0.01f ? 999f : (b - a).magnitude * (c - b).magnitude * (c - a).magnitude / (2f * area2);
    }

    // Moves forward only, so cutting across the infield never counts as progress.
    public int Advance(int index, Vector3 pos)
    {
        int best = index;
        float bestD = (P(index) - pos).sqrMagnitude;
        for (int k = 1; k <= 6; k++)
        {
            float d = (P(index + k) - pos).sqrMagnitude;
            if (d < bestD) { bestD = d; best = index + k; }
        }
        return bestD < 35f * 35f ? best : index;
    }
}
