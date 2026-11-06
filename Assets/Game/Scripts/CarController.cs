using UnityEngine;

// Raycast arcade car: four suspension rays, per-wheel grip, drift, nitro, wheel visuals and effects.
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [System.Serializable]
    public class Wheel
    {
        public Transform pivot, mesh;
        public bool front;
        public float radius = 0.34f;
        public ParticleSystem smoke;
        public TrailRenderer skid;
        [System.NonSerialized] public Vector3 rest;
        [System.NonSerialized] public bool grounded;
        [System.NonSerialized] public float lastX, offset, spinDeg, degPerSec;
    }

    public Wheel[] wheels;
    public ParticleSystem[] flames;

    [Header("Engine")]
    public float acceleration = 8.5f;
    public float topSpeed = 44f;
    public float reverseSpeed = 13f;
    public float brakeForce = 17f;
    [Header("Nitro")]
    public float nitroAccel = 1.75f;
    public float nitroTopSpeed = 10f;
    public float nitroDrain = 0.3f;
    [Header("Grip")]
    public float grip = 2f;
    public float latStiffness = 14f;
    public float driftGrip = 0.74f;
    public float driftYaw = 1.3f;
    public float downforce = 4f;
    [Header("Steering")]
    public float steerLow = 34f;
    public float steerHigh = 10f;

    [HideInInspector] public float Throttle, Steer;
    [HideInInspector] public bool Handbrake, NitroOn;
    [HideInInspector] public int Lap = -1, Index;
    public float Nitro { get; private set; } = 1f;
    public float DriftScore { get; private set; }
    public bool IsDrifting { get; private set; }
    public bool Boosting { get; private set; }
    public float Speed { get; private set; }
    public float SteerAngle { get; private set; }
    public System.Action<float> Impact;

    const float TravelUp = 0.15f, Droop = 0.12f;
    const int Mask = Physics.DefaultRaycastLayers;
    Rigidbody rb;
    float gripBlend = 1f, sag, springK, damper;
    int groundedCount;
    bool offRoad;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 1300f;
        rb.centerOfMass = new Vector3(0f, 0.3f, 0f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.angularDamping = 0.6f;
        springK = rb.mass * 38f;
        damper = rb.mass * 3.5f;
        sag = -Physics.gravity.y * rb.mass * 0.25f / springK;
        foreach (var w in wheels) w.rest = w.pivot.localPosition;
    }

    public void Freeze(bool frozen)
    {
        if (!frozen) { rb.isKinematic = false; return; }
        if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        rb.isKinematic = true;
        Throttle = Steer = 0f; Handbrake = NitroOn = false;
    }

    public void Teleport(Vector3 pos, Quaternion rot)
    {
        bool wasKinematic = rb.isKinematic;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        rb.isKinematic = wasKinematic;
        rb.position = pos; rb.rotation = rot;
        transform.SetPositionAndRotation(pos, rot);
        foreach (var w in wheels) { w.lastX = sag; w.offset = 0f; w.degPerSec = 0f; if (w.skid) w.skid.Clear(); }
        gripBlend = 1f; IsDrifting = false; SteerAngle = 0f;
    }

    public void ResetRaceState()
    {
        Nitro = 1f; DriftScore = 0f; Lap = -1;
    }

    void FixedUpdate()
    {
        if (rb.isKinematic) { Speed = 0f; return; }
        float dt = Time.fixedDeltaTime;
        Vector3 up = transform.up, v = rb.linearVelocity;
        Vector3 planar = Vector3.ProjectOnPlane(v, up);
        Speed = v.magnitude;
        float fwd = Vector3.Dot(v, transform.forward);
        float slip = planar.magnitude > 3f ? Vector3.SignedAngle(transform.forward, planar, up) : 0f;
        if (fwd < 0f) slip = 0f;
        float absSlip = Mathf.Abs(slip);

        float wantLoose = Handbrake || (IsDrifting && Throttle > 0f && absSlip > 14f) ? 1f : 0f;
        gripBlend = Mathf.MoveTowards(gripBlend, 1f - wantLoose, dt / 0.35f);
        float loose = 1f - gripBlend;

        float maxSteer = Mathf.Lerp(steerLow, steerHigh, Speed / 45f) + loose * 5f;
        SteerAngle = Mathf.MoveTowards(SteerAngle, Steer * maxSteer, 130f * dt);

        Boosting = NitroOn && Nitro > 0.01f && groundedCount > 0 && Throttle >= 0f;
        Nitro = Mathf.Clamp01(Nitro + (Boosting ? -nitroDrain : IsDrifting ? 0.14f : 0.05f) * dt);

        float limit = topSpeed + (Boosting ? nitroTopSpeed : 0f);
        float accel = acceleration * (Boosting ? nitroAccel : 1f);
        float drive = 0f, brake = 0f;
        float throttle = Boosting ? 1f : Throttle;
        if (throttle > 0f)
        {
            if (fwd < -0.5f) brake = brakeForce * throttle;
            else drive = accel * throttle * Mathf.Clamp01(1f - fwd * fwd / (limit * limit));
        }
        else if (throttle < 0f)
        {
            if (fwd > 0.5f) brake = brakeForce * -throttle;
            else drive = 6f * throttle * Mathf.Clamp01(1f + fwd / reverseSpeed);
        }

        int grounded = 0;
        bool anyOffRoad = false;
        float quarter = rb.mass * 0.25f;
        foreach (var w in wheels)
        {
            Vector3 top = transform.TransformPoint(w.rest + Vector3.up * TravelUp);
            bool hitGround = Physics.Raycast(top, -up, out RaycastHit hit, TravelUp + sag + Droop + w.radius, Mask, QueryTriggerInteraction.Ignore);
            w.grounded = hitGround;
            if (!hitGround) { w.offset = Mathf.MoveTowards(w.offset, -Droop, dt); w.lastX = 0f; continue; }
            grounded++;

            float len = hit.distance - w.radius;
            float x = TravelUp + sag - len;
            float load = Mathf.Clamp(springK * x + damper * (x - w.lastX) / dt, 0f, rb.mass * 12f);
            w.lastX = x;
            w.offset = TravelUp - len;
            rb.AddForceAtPosition(hit.normal * load, top);

            bool grass = Track.I && hit.collider == Track.I.terrain;
            anyOffRoad |= grass;
            Quaternion steer = w.front ? Quaternion.AngleAxis(SteerAngle, up) : Quaternion.identity;
            Vector3 wf = Vector3.ProjectOnPlane(steer * transform.forward, hit.normal).normalized;
            Vector3 wr = Vector3.Cross(hit.normal, wf);
            Vector3 pos = hit.point + up * 0.3f;
            Vector3 pv = rb.GetPointVelocity(pos);
            float vLat = Vector3.Dot(pv, wr), vLong = Vector3.Dot(pv, wf);

            float mu = grip * (grass ? 0.7f : 1f);
            if (!w.front) mu *= Mathf.Lerp(1f, driftGrip, loose);
            float lat = Mathf.Clamp(-vLat * quarter * latStiffness, -mu * load, mu * load);

            float lon = 0f;
            if (groundedCount > 0) lon = rb.mass * drive / groundedCount;
            if (brake > 0f) lon -= Mathf.Sign(vLong) * Mathf.Min(quarter * brake, Mathf.Abs(vLong) * quarter / dt);
            if (!w.front && Handbrake) lon -= Mathf.Sign(vLong) * Mathf.Min(mu * load * 0.35f, Mathf.Abs(vLong) * quarter / dt);
            if (Mathf.Approximately(throttle, 0f)) lon -= vLong * quarter * 0.25f;
            if (grass) lon -= vLong * quarter * 0.5f;

            rb.AddForceAtPosition(wr * lat + wf * lon, pos);
        }
        groundedCount = grounded;
        offRoad = anyOffRoad;

        if (grounded > 0)
        {
            rb.AddForce(-up * (Speed * Speed * downforce));
            if (loose > 0.05f) rb.AddTorque(up * (Steer * driftYaw * loose), ForceMode.Acceleration);
            if (absSlip > 32f) rb.AddTorque(up * (Mathf.Sign(slip) * (absSlip - 32f) * 0.35f), ForceMode.Acceleration);
        }

        IsDrifting = grounded >= 2 && Speed > 9f && absSlip > 11f && !offRoad;
        if (IsDrifting) DriftScore += Speed * absSlip * 0.05f * dt * 10f;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        float fwd = rb.isKinematic ? 0f : Vector3.Dot(rb.linearVelocity, transform.forward);
        bool burn = IsDrifting || (Handbrake && Speed > 4f);
        foreach (var w in wheels)
        {
            float target = fwd / w.radius * Mathf.Rad2Deg;
            if (!w.front && burn) target = Mathf.Max(Speed * 1.3f, 20f) / w.radius * Mathf.Rad2Deg;
            w.degPerSec = Mathf.Lerp(w.degPerSec, target, 1f - Mathf.Exp(-14f * dt));
            w.spinDeg = Mathf.Repeat(w.spinDeg + w.degPerSec * dt, 360f);
            w.mesh.localRotation = Quaternion.Euler(w.spinDeg, 0f, 0f);
            w.pivot.localPosition = w.rest + Vector3.up * w.offset;
            if (w.front) w.pivot.localRotation = Quaternion.Euler(0f, SteerAngle, 0f);

            bool marks = burn && w.grounded && !w.front;
            if (w.skid) w.skid.emitting = marks;
            if (w.smoke) { var e = w.smoke.emission; e.rateOverTime = marks ? 45f : 0f; }
        }
        foreach (var f in flames) { var e = f.emission; e.rateOverTime = Boosting ? 90f : 0f; }
    }

    void OnCollisionEnter(Collision c)
    {
        Impact?.Invoke(c.relativeVelocity.magnitude);
    }
}
