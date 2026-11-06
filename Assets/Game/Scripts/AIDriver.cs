using UnityEngine;

// Pure-pursuit along the racing line, braking for the corners ahead.
[RequireComponent(typeof(CarController))]
public class AIDriver : MonoBehaviour
{
    public CarController rival;
    public float pace = 0.93f;
    public float brakeDecel = 9f;

    const float WheelBase = 2.6f;

    CarController car;
    float stuck, reversing, sideOffset;

    void Awake() { car = GetComponent<CarController>(); }

    void FixedUpdate()
    {
        if (RaceManager.I.State != RaceState.Racing) return;
        var t = Track.I;
        float v = car.Speed, dt = Time.fixedDeltaTime;

        if (reversing > 0f)
        {
            reversing -= dt;
            car.Throttle = -1f; car.NitroOn = false;
            car.Steer = -SteerTo(t.P(car.Index + 2));
            return;
        }

        // Rubber band: stay a threat without running away.
        float gap = RaceManager.I.Progress(rival) - RaceManager.I.Progress(car);
        float paceNow = pace * Mathf.Clamp(1f + gap * 0.012f, 0.92f, 1.12f);

        float desired = 60f;
        float dist = 0f;
        float horizon = 12f + v * v / (2f * brakeDecel);
        for (int i = 0; dist < horizon && i < 24; i++)
        {
            float allowed = t.safeSpeed[(car.Index + i) % t.Count] * paceNow;
            desired = Mathf.Min(desired, Mathf.Sqrt(allowed * allowed + 2f * brakeDecel * dist));
            dist += (t.P(car.Index + i + 1) - t.P(car.Index + i)).magnitude;
        }

        // Pass the rival on the side it is not on.
        float wantSide = 0f;
        if (rival)
        {
            Vector3 r = transform.InverseTransformPoint(rival.transform.position);
            if (r.z > 0f && r.z < 9f + v * 0.5f && Mathf.Abs(r.x) < 2.6f) wantSide = r.x >= 0f ? -3.2f : 3.2f;
        }
        sideOffset = Mathf.MoveTowards(sideOffset, wantSide, dt * 4f);

        float look = 6f + v * 0.3f;
        int idx = car.Index + 1;
        float walked = 0f;
        while (walked < look) { walked += (t.P(idx + 1) - t.P(idx)).magnitude; idx++; }
        Vector3 d = t.Dir(idx);
        Vector3 target = t.P(idx) + new Vector3(d.z, 0f, -d.x) * sideOffset;

        car.Steer = SteerTo(target);
        float err = desired - v;
        car.Throttle = err > 0f ? Mathf.Clamp01(err * 0.5f + 0.2f) : Mathf.Clamp(err * 0.35f, -1f, 0f);
        car.NitroOn = desired > 44f && err > 5f && Mathf.Abs(car.Steer) < 0.2f && car.Nitro > 0.25f;
        car.Handbrake = false;

        stuck = v < 2.5f ? stuck + dt : 0f;
        if (stuck > 2f) { stuck = 0f; reversing = 1.3f; RaceManager.I.CountStuck(car); }
    }

    // Pure-pursuit: the wheel angle whose arc passes through the target point.
    float SteerTo(Vector3 p)
    {
        Vector3 l = transform.InverseTransformPoint(p);
        float dist = Mathf.Max(l.magnitude, 3f);
        float wheelAngle = Mathf.Atan(2f * WheelBase * (l.x / dist) / dist) * Mathf.Rad2Deg * 1.25f;
        if (l.z < 0f) wheelAngle = Mathf.Sign(l.x) * 30f;
        float max = Mathf.Lerp(car.steerLow, car.steerHigh, car.Speed / 45f);
        return Mathf.Clamp(wheelAngle / max, -1f, 1f);
    }
}
