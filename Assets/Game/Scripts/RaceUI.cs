using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Builds the HUD and menus in code, so there is no UI prefab to maintain.
public class RaceUI : MonoBehaviour
{
    static readonly Color Accent = new Color(1f, 0.32f, 0.12f), Boost = new Color(0.2f, 0.75f, 1f), Panel = new Color(0.03f, 0.04f, 0.07f, 0.72f);

    Font font;
    GameObject hud, menu, pause, results;
    Text speed, timer, lap, status, drift, count, resultTitle, resultBody;
    Image nitro;

    void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject("RaceCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = go.transform;
        if (!FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        hud = Layer(root, "HUD");
        var left = Box(hud.transform, new Vector2(0, 1), new Vector2(30, -30), new Vector2(330, 118));
        timer = Label(left, "0:00.00", 44, TextAnchor.UpperLeft, new Vector2(18, -8), new Vector2(300, 56), new Vector2(0, 1));
        lap = Label(left, "LAP 1/2", 26, TextAnchor.UpperLeft, new Vector2(18, -68), new Vector2(300, 36), new Vector2(0, 1));
        var right = Box(hud.transform, new Vector2(1, 1), new Vector2(-30, -30), new Vector2(340, 118));
        status = Label(right, "", 26, TextAnchor.UpperLeft, new Vector2(18, -12), new Vector2(310, 96), new Vector2(0, 1));
        var dash = Box(hud.transform, new Vector2(1, 0), new Vector2(-30, 30), new Vector2(340, 170));
        speed = Label(dash, "0", 84, TextAnchor.LowerRight, new Vector2(-104, 52), new Vector2(220, 100), new Vector2(1, 0));
        Label(dash, "KM/H", 26, TextAnchor.LowerRight, new Vector2(-20, 60), new Vector2(80, 34), new Vector2(1, 0));
        Label(dash, "NITRO", 20, TextAnchor.LowerLeft, new Vector2(20, 26), new Vector2(80, 26), new Vector2(0, 0));
        var bar = Rect(dash, new Vector2(0, 0), new Vector2(100, 28), new Vector2(218, 18));
        bar.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.14f);
        var fill = Rect(bar, new Vector2(0, 0), Vector2.zero, new Vector2(218, 18));
        nitro = fill.gameObject.AddComponent<Image>();
        nitro.color = Boost;
        drift = Label(hud.transform, "", 40, TextAnchor.LowerLeft, new Vector2(40, 40), new Vector2(520, 60), new Vector2(0, 0));
        count = Label(hud.transform, "", 170, TextAnchor.MiddleCenter, new Vector2(0, 120), new Vector2(700, 240), new Vector2(0.5f, 0.5f));

        menu = Layer(root, "Menu");
        Title(menu.transform, "DRIFT CIRCUIT", 110, 150);
        Label(menu.transform, "MERCEDES-AMG GT3  VS  PORSCHE 992 GT3 R", 28, TextAnchor.MiddleCenter, new Vector2(0, 66), new Vector2(1000, 44), new Vector2(0.5f, 0.5f));
        Button(menu.transform, "PLAY", new Vector2(0, -30), () => RaceManager.I.Play());
        Label(menu.transform, "WASD / ARROWS DRIVE     SPACE DRIFT     SHIFT NITRO     C CAMERA     R RESET     ESC PAUSE", 22, TextAnchor.MiddleCenter, new Vector2(0, -130), new Vector2(1300, 40), new Vector2(0.5f, 0.5f));

        pause = Layer(root, "Pause", true);
        Title(pause.transform, "PAUSED", 90, 150);
        Button(pause.transform, "RESUME", new Vector2(0, 40), () => RaceManager.I.TogglePause());
        Button(pause.transform, "RESTART", new Vector2(0, -50), () => RaceManager.I.Play());
        Button(pause.transform, "MAIN MENU", new Vector2(0, -140), () => RaceManager.I.ToMenu());

        results = Layer(root, "Results", true);
        resultTitle = Title(results.transform, "", 100, 170);
        resultBody = Label(results.transform, "", 32, TextAnchor.MiddleCenter, new Vector2(0, 50), new Vector2(900, 130), new Vector2(0.5f, 0.5f));
        Button(results.transform, "RESTART", new Vector2(0, -70), () => RaceManager.I.Play());
        Button(results.transform, "MAIN MENU", new Vector2(0, -160), () => RaceManager.I.ToMenu());
    }

    void Update()
    {
        var m = RaceManager.I;
        var s = m.State;
        hud.SetActive(s == RaceState.Countdown || s == RaceState.Racing);
        menu.SetActive(s == RaceState.Menu);
        pause.SetActive(s == RaceState.Paused);
        results.SetActive(s == RaceState.Finished);

        var p = m.player;
        speed.text = Mathf.RoundToInt(p.Speed * 3.6f).ToString();
        nitro.rectTransform.sizeDelta = new Vector2(218f * p.Nitro, 18f);
        nitro.color = p.Boosting ? Color.white : Boost;
        timer.text = Clock(m.RaceTime);
        lap.text = "LAP " + Mathf.Clamp(p.Lap + 1, 1, m.laps) + "/" + m.laps;
        status.text = (m.PlayerLeads ? "1ST  YOU" : "2ND  YOU") + "  L" + Mathf.Clamp(p.Lap + 1, 1, m.laps)
            + "\n" + (m.PlayerLeads ? "2ND  AI" : "1ST  AI") + "  L" + Mathf.Clamp(m.ai.Lap + 1, 1, m.laps);
        drift.text = p.DriftScore >= 1f ? (p.IsDrifting ? "DRIFT  " : "SCORE  ") + Mathf.FloorToInt(p.DriftScore) : "";
        drift.color = p.IsDrifting ? Accent : Color.white;

        if (s == RaceState.Countdown) count.text = Mathf.CeilToInt(m.Countdown).ToString();
        else count.text = s == RaceState.Racing && m.GoFlash > 0f ? "GO!" : "";

        if (s == RaceState.Finished)
        {
            bool won = m.Winner == p;
            resultTitle.text = won ? "YOU WIN!" : "AI WINS";
            resultTitle.color = won ? Accent : Color.white;
            resultBody.text = "TIME  " + Clock(m.RaceTime) + "\nMARGIN  " + m.WinMargin.ToString("0.0") + " S\nDRIFT SCORE  " + Mathf.FloorToInt(p.DriftScore);
        }
    }

    static string Clock(float t) => (int)(t / 60f) + ":" + (t % 60f).ToString("00.00");

    GameObject Layer(Transform parent, string name, bool dim = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var r = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        if (dim) go.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);
        return go;
    }

    RectTransform Rect(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var r = new GameObject("Rect", typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = anchor;
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    RectTransform Box(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var r = Rect(parent, anchor, pos, size);
        r.gameObject.AddComponent<Image>().color = Panel;
        return r;
    }

    Text Label(Transform parent, string text, int size, TextAnchor align, Vector2 pos, Vector2 box, Vector2 anchor)
    {
        var t = Rect(parent, anchor, pos, box).gameObject.AddComponent<Text>();
        t.font = font; t.text = text; t.fontSize = size; t.alignment = align; t.fontStyle = FontStyle.Bold;
        t.color = Color.white; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        var sh = t.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0, 0, 0, 0.7f); sh.effectDistance = new Vector2(2, -2);
        return t;
    }

    Text Title(Transform parent, string text, int size, float y) =>
        Label(parent, text, size, TextAnchor.MiddleCenter, new Vector2(0, y), new Vector2(1300, 150), new Vector2(0.5f, 0.5f));

    void Button(Transform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        var r = Rect(parent, new Vector2(0.5f, 0.5f), pos, new Vector2(340, 74));
        var img = r.gameObject.AddComponent<Image>();
        img.color = Accent;
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(action);
        Label(r, text, 34, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(340, 74), new Vector2(0.5f, 0.5f));
    }
}
