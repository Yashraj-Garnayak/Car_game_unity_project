using UnityEngine;
using UnityEngine.InputSystem;

public class ChaseCamera : MonoBehaviour
{
    public CarController target;
    public Vector3 orbitCentre;

    static readonly Vector3[] Views = { new Vector3(0f, 2f, -6f), new Vector3(0f, 3.3f, -9.5f), new Vector3(0f, 1.05f, 0.9f) };
    int view;
    float shake, orbit;
    Vector3 vel;
    Camera cam;

    void Awake() { cam = GetComponent<Camera>(); }

    void Start()
    {
        target.Impact += s => shake = Mathf.Max(shake, Mathf.Clamp(s * 0.018f, 0f, 0.5f));
        RaceManager.I.CarReset += c => Snap();
    }

    public void Snap() { vel = Vector3.zero; Place(1f); }

    void LateUpdate()
    {
        var k = Keyboard.current;
        if (k != null && k.cKey.wasPressedThisFrame) { view = (view + 1) % Views.Length; Snap(); }

        if (RaceManager.I.State == RaceState.Menu)
        {
            orbit += Time.unscaledDeltaTime * 8f;
            Vector3 p = target.transform.position + Quaternion.Euler(0f, orbit, 0f) * new Vector3(0f, 2.2f, 9f);
            transform.position = p;
            transform.LookAt(orbitCentre + Vector3.up * 0.7f);
            cam.fieldOfView = 45f;
            return;
        }
        Place(0f);
        float fov = 62f + Mathf.Clamp01(target.Speed / 55f) * 18f + (target.Boosting ? 7f : 0f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-5f * Time.deltaTime));
        if (shake > 0.001f)
        {
            transform.position += Random.insideUnitSphere * shake;
            shake = Mathf.Lerp(shake, 0f, 1f - Mathf.Exp(-7f * Time.deltaTime));
        }
    }

    void Place(float snap)
    {
        Transform t = target.transform;
        Vector3 o = Views[view];
        if (view == 2)
        {
            transform.SetPositionAndRotation(t.TransformPoint(o), t.rotation);
            return;
        }
        Vector3 back = t.forward;
        Vector3 goal = t.position + Vector3.up * o.y + back * o.z;
        transform.position = snap > 0f ? goal : Vector3.SmoothDamp(transform.position, goal, ref vel, 0.08f);
        Quaternion look = Quaternion.LookRotation(t.position + Vector3.up * 1.1f + t.forward * 4f - transform.position);
        transform.rotation = snap > 0f ? look : Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-12f * Time.deltaTime));
    }
}
