using UnityEngine;
using UnityEngine.InputSystem;

public enum RaceState { Menu, Countdown, Racing, Paused, Finished }

public class RaceManager : MonoBehaviour
{
    public static RaceManager I;
    public CarController player, ai;
    public Transform playerGrid, aiGrid;
    public int laps = 2;

    public RaceState State { get; private set; } = RaceState.Menu;
    public float Countdown { get; private set; }
    public float RaceTime { get; private set; }
    public float GoFlash { get; private set; }
    public CarController Winner { get; private set; }
    public float WinMargin { get; private set; }
    public System.Action<CarController> CarReset;

    int aiStuckCount;

    void Awake() { I = this; }

    void Start()
    {
        ToGrid();
        SetFrozen(true);
    }

    public float Progress(CarController c) => c.Lap * Track.I.Count + c.Index;
    public bool PlayerLeads => Progress(player) >= Progress(ai);

    void ToGrid()
    {
        Time.timeScale = 1f;
        foreach (var (car, grid) in new[] { (player, playerGrid), (ai, aiGrid) })
        {
            car.Teleport(grid.position, grid.rotation);
            car.ResetRaceState();
            car.Index = Track.I.Count - 2;
        }
        RaceTime = 0f; GoFlash = 0f; Winner = null; aiStuckCount = 0;
        CarReset?.Invoke(player);
    }

    void SetFrozen(bool f) { player.Freeze(f); ai.Freeze(f); }

    public void Play()
    {
        ToGrid();
        SetFrozen(true);
        Countdown = 3f;
        State = RaceState.Countdown;
    }

    public void ToMenu()
    {
        ToGrid();
        SetFrozen(true);
        State = RaceState.Menu;
    }

    public void TogglePause()
    {
        if (State == RaceState.Racing) { State = RaceState.Paused; Time.timeScale = 0f; }
        else if (State == RaceState.Paused) { State = RaceState.Racing; Time.timeScale = 1f; }
    }

    public void ResetCar(CarController car)
    {
        var t = Track.I;
        car.Teleport(t.P(car.Index) + Vector3.up * 0.1f, Quaternion.LookRotation(t.Dir(car.Index)));
        if (car == player) CarReset?.Invoke(car);
    }

    public void CountStuck(CarController car)
    {
        if (++aiStuckCount >= 3) { aiStuckCount = 0; ResetCar(car); }
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.escapeKey.wasPressedThisFrame) TogglePause();

        if (State == RaceState.Countdown)
        {
            Countdown -= Time.deltaTime;
            if (Countdown <= 0f)
            {
                State = RaceState.Racing;
                GoFlash = 1f;
                SetFrozen(false);
            }
        }
        else if (State == RaceState.Racing)
        {
            RaceTime += Time.deltaTime;
            GoFlash -= Time.deltaTime;
        }
    }

    void FixedUpdate()
    {
        if (State != RaceState.Racing) return;
        Track1(player);
        Track1(ai);
    }

    void Track1(CarController c)
    {
        var t = Track.I;
        int next = t.Advance(c.Index, c.transform.position);
        if (next >= t.Count) { next -= t.Count; c.Lap++; }
        c.Index = next;
        if (c.Lap >= laps && State == RaceState.Racing) Finish(c);
    }

    void Finish(CarController c)
    {
        Winner = c;
        var other = c == player ? ai : player;
        float gapPoints = Progress(c) - Progress(other);
        WinMargin = gapPoints * 12f / Mathf.Max(other.Speed, 10f);
        State = RaceState.Finished;
        player.Throttle = ai.Throttle = 0f;
        player.NitroOn = ai.NitroOn = false;
        player.Handbrake = ai.Handbrake = true;
    }
}
