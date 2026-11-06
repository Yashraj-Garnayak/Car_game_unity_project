using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CarController))]
public class PlayerDriver : MonoBehaviour
{
    CarController car;
    float steer;

    void Awake() { car = GetComponent<CarController>(); }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null || RaceManager.I.State != RaceState.Racing) { steer = 0f; return; }
        float t = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);
        float s = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
        steer = Mathf.MoveTowards(steer, s, Time.deltaTime * (s == 0f ? 6f : 4f));
        car.Throttle = t;
        car.Steer = steer;
        car.Handbrake = k.spaceKey.isPressed;
        car.NitroOn = k.leftShiftKey.isPressed;
        if (k.rKey.wasPressedThisFrame) RaceManager.I.ResetCar(car);
    }
}
