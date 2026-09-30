using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>Always enter through the menu; gameplay requires Start or Restart.</summary>
public class RunGameFlow : MonoBehaviour
{
    public static RunGameFlow Instance { get; private set; }
    public const string MenuScene = "MainMenu";
    public const string GameScene = "SampleScene";
    private static bool gameStartRequested;
    private Transform canvas;
    private GameObject page;
    private AutoRunner runner;
    private float startedAt;
    private float resultTime;
    private int resultMisses;
    private int resultCollisionMisses;
    private bool dead;
    private int lastSubmitFrame = -1;
    public bool IsStopped => completed || dead || loading || paused;
    public bool IsPaused => paused;
    private bool paused;
    private float resumeTimeScale = 1f;
    private bool completed;
    private bool loading;
    private Text hud;
    private GameObject gameHud;
    private LongNoteSliderHud chargeSlider;
    private AudioSource bgm;
    private Font font;
    private InputActionAsset menuActions;
    private InputActionReference menuMove;
    private InputActionReference menuSubmit;
    private bool collectionVisible;
    private int collectionIndex;
    private Image collectionPortrait;
    private Text collectionName;
    private Text collectionStatus;
    private Text collectionPlaceholder;
    private Button collectionPrevious;
    private Button collectionNext;
    private Button[] menuButtons;
    private GameObject highlightedButton;
    private bool waitingForKey;
    private bool rebindingGravity;
    private int rebindStartedFrame;
    private Text keySettingsHint;
    private readonly Color cyan = new Color(0.15f, 0.95f, 1f);
    private readonly Color pink = new Color(1f, 0.22f, 0.62f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        Instance = null;
        gameStartRequested = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MenuScene && scene.name != GameScene) return;
        if (scene.name == GameScene && !gameStartRequested)
        {
            // Direct Play in the gameplay scene must not start the runner, music,
            // or scene Start methods while the menu is loading.
            Time.timeScale = 0f;
            foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
            SceneManager.LoadSceneAsync(MenuScene);
            return;
        }
        gameStartRequested = false;
        if (FindAnyObjectByType<RunGameFlow>() == null)
            new GameObject("Run Game Flow").AddComponent<RunGameFlow>();
    }

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        // WebGL cannot use fonts installed on the player's operating system.
        font = Resources.Load<Font>("Fonts/NanumGothic-Regular");
        if (font == null) Debug.LogError("Bundled UI font Fonts/NanumGothic-Regular is missing.");
        RunMenuAudio.Ensure();
        var ui = new GameObject("Neon UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        ui.transform.SetParent(transform);
        var c = ui.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 1000;
        var scaler = ui.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;
        canvas = ui.transform;
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("UI Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
        ConfigureMenuInput();

        if (SceneManager.GetActiveScene().name == MenuScene)
        {
            gameObject.AddComponent<NeonMenuCity>();
            StartMenuMusic();
            ShowMenu();
            return;
        }

        var settings = Resources.Load<RunGameSettings>("RunGameSettings");
        var candidates = FindObjectsByType<AutoRunner>(FindObjectsInactive.Include);
        string playerName = settings != null ? settings.playerObjectName : "Player";
#if UNITY_EDITOR
        // Temporary map editing target; never changes the built game's settings.
        playerName = UnityEditor.EditorPrefs.GetString(Application.dataPath + ".MapPreviewPlayer", playerName);
#endif
        foreach (var candidate in candidates)
            if (candidate.name == playerName) { runner = candidate; break; }
        if (runner == null)
        {
            Debug.LogError($"Main player '{playerName}' with AutoRunner was not found. Check RunGameSettings.");
            return;
        }
        // The scene keeps disabled copies for section testing. A real run always starts
        // with the original Player, even when a test copy was active in the editor.
        foreach (var candidate in candidates)
            if (candidate != runner) candidate.gameObject.SetActive(false);
        for (Transform parent = runner.transform.parent; parent != null; parent = parent.parent)
            parent.gameObject.SetActive(true);
        runner.gameObject.SetActive(true);
        runner.enabled = true;
        // Section-test player copies are disabled above; attack triggers must
        // detect and calculate tile collapse relative to the actual runner.
        foreach (var attack in FindObjectsByType<AttackTriggerFollow>(FindObjectsInactive.Include))
            attack.SetTargetPlayer(runner.transform);
        foreach (var camera in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include))
        {
            if (camera.Follow != null && camera.Follow.GetComponent<AutoRunner>() != null)
                camera.Follow = runner.transform;
            if (camera.Target.CustomLookAtTarget && camera.LookAt != null && camera.LookAt.GetComponent<AutoRunner>() != null)
                camera.LookAt = runner.transform;
        }
        startedAt = Time.time;
        var music = GameObject.Find("BGM");
        if (music != null) bgm = music.GetComponent<AudioSource>();
        if (bgm != null && settings != null && settings.fullTrack != null)
        {
            bgm.Stop();
            bgm.clip = settings.fullTrack;
            bgm.loop = false;
            bgm.pitch = 1f;
            bgm.Play();
        }
        foreach (var map in FindObjectsByType<Tilemap>())
            if (map.name == "Finish" && map.GetComponent<FinishLine>() == null)
                map.gameObject.AddComponent<FinishLine>();
        CreateGameHud(settings);
        if (runner != null && bgm != null && bgm.clip != null)
            Debug.Log($"[Course] Start {runner.transform.position}, speed {runner.RunSpeed:F2} units/s, " +
                $"BGM {bgm.clip.name} {bgm.clip.length:F3}s. Teleports and Long paths change actual travel time.");
    }

    private void StartMenuMusic()
    {
        var settings = Resources.Load<RunGameSettings>("RunGameSettings");
        if (settings == null || settings.fullTrack == null) return;
        // Scene-owned music continues across menu pages and ends on scene unload.
        // Persistent menu sound effects use their own source and remain uninterrupted.
        bgm = gameObject.AddComponent<AudioSource>();
        bgm.playOnAwake = false;
        bgm.clip = settings.fullTrack;
        bgm.loop = true;
        bgm.spatialBlend = 0f;
        bgm.volume = 0f;
        bgm.Play();
        StartCoroutine(FadeInMenuMusic(Mathf.Clamp01(settings.menuMusicVolume)));
    }

    private System.Collections.IEnumerator FadeInMenuMusic(float targetVolume)
    {
        const float duration = 1.5f;
        float elapsed = 0f;
        while (bgm != null && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            bgm.volume = targetVolume * Mathf.SmoothStep(0f, 1f, elapsed / duration);
            yield return null;
        }
        if (bgm != null) bgm.volume = targetVolume;
    }

    private void Update()
    {
        if (waitingForKey)
        {
            CaptureBinding();
            return;
        }
        if (runner != null && !completed && !dead && !loading &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (paused) ResumeRun(); else PauseRun();
        }
        UpdateMenuSelection();
        if (collectionVisible && !loading)
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if ((keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.dpad.left.wasPressedThisFrame))
            { RunMenuAudio.Move(); ChangeCollection(-1); }
            else if ((keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.dpad.right.wasPressedThisFrame))
            { RunMenuAudio.Move(); ChangeCollection(1); }
        }
        if (hud == null || runner == null || IsStopped) return;
        RefreshGameHud();
    }

    private void PauseRun()
    {
        if (IsStopped) return;
        paused = true;
        resumeTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        // Keep the menu above the teleport overlay (sorting order 1100).
        canvas.GetComponent<Canvas>().sortingOrder = 2000;
        NewPage(true);
        Label(page.transform, "Title", "일시정지", 66, new Vector2(0, 210), new Vector2(1100, 110), cyan);
        ActionButton("재시작", 10, () => Load(SceneManager.GetActiveScene().name), true);
        ActionButton("메인화면", -90, () => Load(MenuScene));
        Label(page.transform, "Resume hint", "ESC  계속하기", 24, new Vector2(0, -220), new Vector2(900, 60), Color.white);
        FocusFirstButton();
    }

    private void ResumeRun()
    {
        if (!paused || loading) return;
        if (page != null) { page.SetActive(false); Destroy(page); page = null; }
        menuButtons = null;
        highlightedButton = null;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        canvas.GetComponent<Canvas>().sortingOrder = 1000;
        paused = false;
        Time.timeScale = resumeTimeScale;
        AudioListener.pause = false;
    }

    private void CreateGameHud(RunGameSettings settings)
    {
        gameHud = new GameObject("Gameplay HUD", typeof(RectTransform));
        gameHud.transform.SetParent(canvas, false);
        var root = (RectTransform)gameHud.transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.sizeDelta = Vector2.zero;
        hud = Label(root, "Run status", "", 18, Vector2.zero, new Vector2(400, 96), cyan);
        hud.alignment = TextAnchor.UpperLeft;
        hud.horizontalOverflow = HorizontalWrapMode.Wrap;
        hud.verticalOverflow = VerticalWrapMode.Overflow;
        hud.rectTransform.anchorMin = hud.rectTransform.anchorMax = new Vector2(0, 1);
        hud.rectTransform.pivot = new Vector2(0, 1);
        hud.rectTransform.anchoredPosition = new Vector2(24, -24);
        if (settings != null && (settings.chargeFrameTexture != null || settings.longNoteSliderTexture != null))
        {
            var slider = new GameObject("Long Note Charge Slider", typeof(RectTransform), typeof(LongNoteSliderHud));
            slider.transform.SetParent(root, false);
            chargeSlider = slider.GetComponent<LongNoteSliderHud>();
            chargeSlider.Initialize(settings);
        }
        RefreshGameHud();
    }

    private void RefreshGameHud()
    {
        float width = ((RectTransform)canvas).rect.width;
        if (width <= 0f) width = 1600f;
        if (chargeSlider != null)
            chargeSlider.Refresh(width, runner.LongNoteChargeProgress, runner.IsLongNoteCharging, runner.IsLongNoteLaunching);
        float available = chargeSlider != null ? (width - chargeSlider.Width) * 0.5f - 48f : width - 48f;
        available = Mathf.Max(80f, available);
        hud.fontSize = Mathf.Clamp(Mathf.FloorToInt(available / 9f), 12, 18);
        hud.rectTransform.sizeDelta = new Vector2(available, 110);
        string time = $"TIME {Time.time - startedAt:000.00}s";
        string misses = $"MISS {runner.CollisionMisses}";
        string longMisses = $"LONG MISS {runner.PendingLongNotePenalties}";
        string speed = $"SPEED {runner.CurrentSpeed:0.0}";
        hud.text = $"{time}   {misses}   {longMisses}   {speed}";
        if (hud.preferredWidth > available) hud.text = $"{time}\n{misses}\n{longMisses}\n{speed}";
    }

    public void Complete(AutoRunner player)
    {
        if (IsStopped || player != runner) return;
        completed = true;
        resultTime = Time.time - startedAt;
        resultMisses = player.PendingLongNotePenalties;
        resultCollisionMisses = player.CollisionMisses;
        player.FinishRun();
        // Stop pending warp sequences when the run ends.
        foreach (var zone in FindObjectsByType<TeleportZone>()) zone.StopAllCoroutines();
        StopStage(keepMusicPlaying: true);
        Time.timeScale = 0f;
        RunLeaderboard.Add(resultTime, resultCollisionMisses, resultMisses);
        if (bgm != null && bgm.clip != null)
            Debug.Log($"[Course complete] Run {resultTime:F3}s / BGM {bgm.clip.length:F3}s / " +
                $"difference {resultTime - bgm.clip.length:+0.000;-0.000;0.000}s");
        if (gameHud != null) gameHud.SetActive(false);
        ShowResult();
    }

    private void StopStage(bool keepMusicPlaying = false)
    {
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>())
        {
            if (behaviour == this || behaviour is RunMenuAudio || behaviour is EventSystem ||
                behaviour is BaseInputModule || behaviour is Graphic || behaviour is Selectable ||
                behaviour is CanvasScaler) continue;
            behaviour.StopAllCoroutines();
            behaviour.enabled = false;
        }
        foreach (var source in FindObjectsByType<AudioSource>())
            if (source.GetComponent<RunMenuAudio>() == null && !(keepMusicPlaying && source == bgm))
                source.Stop();
        foreach (var animator in FindObjectsByType<Animator>()) animator.enabled = false;
    }

    public void Die(AutoRunner player)
    {
        if (IsStopped || player != runner) return;
        dead = true;
        resultTime = Time.time - startedAt;
        player.Die();
        StopStage();
        Time.timeScale = 0f;
        if (gameHud != null) gameHud.SetActive(false);
        NewPage(true);
        Label(page.transform, "Title", "RUN ENDED", 66, new Vector2(0, 230), new Vector2(1100, 110), pink);
        Label(page.transform, "Misses", $"{resultTime:0.00} SEC   /   {player.CollisionMisses} MISS   /   {player.PendingLongNotePenalties} LONG MISS",
            28, new Vector2(0, 110), new Vector2(1300, 65), Color.white);
        ActionButton("재시작", -50, () => Load(SceneManager.GetActiveScene().name), true);
        ActionButton("메인메뉴", -150, () => Load(MenuScene));
        FocusFirstButton();
    }

    private void ShowCollection()
    {
        NewPage(true);
        collectionVisible = true;
        collectionIndex = 0;
        Label(page.transform, "Title", "컬렉션", 54, new Vector2(0, 320), new Vector2(1100, 90), Color.white);
        var portrait = new GameObject("Player Portrait", typeof(RectTransform), typeof(Image));
        portrait.transform.SetParent(page.transform, false);
        ((RectTransform)portrait.transform).sizeDelta = new Vector2(220, 240);
        ((RectTransform)portrait.transform).anchoredPosition = new Vector2(0, 90);
        collectionPortrait = portrait.GetComponent<Image>();
        var settings = Resources.Load<RunGameSettings>("RunGameSettings");
        collectionPortrait.sprite = settings != null ? settings.defaultCharacterSprite : null;
        collectionPortrait.preserveAspect = true;
        collectionPortrait.raycastTarget = false;
        collectionPlaceholder = Label(page.transform, "Coming soon", "COMING SOON", 34,
            new Vector2(0, 90), new Vector2(440, 240), cyan);
        collectionName = Label(page.transform, "Character", "", 32, new Vector2(0, -80), new Vector2(800, 60), Color.white);
        collectionStatus = Label(page.transform, "Equipped", "", 24, new Vector2(0, -145), new Vector2(800, 50), pink);
        ActionButton("메인메뉴", -285, ShowMenu);
        collectionPrevious = ActionButton("◀", 90, () => ChangeCollection(-1), navigationSound: true);
        collectionNext = ActionButton("▶", 90, () => ChangeCollection(1), navigationSound: true);
        var previousRect = (RectTransform)collectionPrevious.transform;
        previousRect.anchoredPosition = new Vector2(-310, 90); previousRect.sizeDelta = new Vector2(90, 90);
        var nextRect = (RectTransform)collectionNext.transform;
        nextRect.anchoredPosition = new Vector2(310, 90); nextRect.sizeDelta = new Vector2(90, 90);
        foreach (var arrow in new[] { collectionPrevious, collectionNext })
            arrow.GetComponentInChildren<Text>().rectTransform.sizeDelta = new Vector2(90, 90);
        Label(page.transform, "Browse controls", "A / D  OR  LEFT / RIGHT    BROWSE", 18,
            new Vector2(0, -385), new Vector2(1100, 40), cyan);
        ChangeCollection(0);
        FocusFirstButton();
    }

    private void ChangeCollection(int direction)
    {
        // Only the default character is available. Browsing never changes equipment.
        collectionIndex = (collectionIndex + direction + 3) % 3;
        bool available = collectionIndex == 0;
        collectionPortrait.gameObject.SetActive(available && collectionPortrait.sprite != null);
        collectionPlaceholder.gameObject.SetActive(!available);
        collectionName.text = available ? "기본 캡슐" : "COMING SOON";
        collectionStatus.text = available ? "현재 사용 중" : "IN DEVELOPMENT";
        collectionPlaceholder.text = "?";
        collectionPlaceholder.fontSize = 110;
    }

    private void NewPage(bool dark)
    {
        waitingForKey = false;
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
        collectionVisible = false;
        if (page != null) { page.SetActive(false); Destroy(page); }
        page = new GameObject("Menu page", typeof(RectTransform));
        page.transform.SetParent(canvas, false);
        var rect = (RectTransform)page.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
        if (dark)
        {
            var image = page.AddComponent<Image>();
            image.color = new Color(0.015f, 0.02f, 0.065f, 0.94f);
        }
    }

    private void ShowMenu()
    {
        NewPage(false);
        Label(page.transform, "Edition", "NIGHT CITY  /  RHYTHM RUNNER", 20, new Vector2(0, 300), new Vector2(900, 50), cyan);
        Label(page.transform, "Title upper", "NEON", 96, new Vector2(0, 215), new Vector2(1050, 120), Color.white);
        Label(page.transform, "Title lower", "REVIVED", 96, new Vector2(0, 100), new Vector2(1050, 120), Color.white);
        Label(page.transform, "Subtitle", "CHASE THE BEAT. FLIP THE CITY.", 23, new Vector2(0, -3), new Vector2(900, 50), cyan);
        ActionButton("START RUN", -80, () => Load(GameScene), true);
        ActionButton("RANKING", -162, ShowRanking);
        ActionButton("컬렉션", -244, ShowCollection);
        ActionButton("설정", -326, ShowKeySettings);
        Label(page.transform, "Controls", $"{RunKeyBindings.Gravity}  GRAVITY FLIP     /     {RunKeyBindings.Cross}  CROSS + HOLD LONG NOTES", 19,
            new Vector2(0, -380), new Vector2(1400, 40), Color.white);
        Label(page.transform, "Menu controls", "W / S  OR  UP / DOWN    SELECT     |     ENTER    CONFIRM", 18,
            new Vector2(0, -415), new Vector2(1200, 40), cyan);
        FocusFirstButton();
    }

    private void ShowKeySettings()
    {
        NewPage(true);
        Label(page.transform, "Title", "키 설정", 54, new Vector2(0, 290), new Vector2(1100, 90), cyan);
        ActionButton($"중력 반전 : {RunKeyBindings.Gravity}", 130, () => BeginBinding(true));
        ActionButton($"통과 / 롱노트 : {RunKeyBindings.Cross}", 35, () => BeginBinding(false));
        keySettingsHint = Label(page.transform, "Key hint", "변경할 동작을 선택한 뒤 원하는 키를 누르세요.\n같은 키를 지정하면 두 동작의 키가 서로 바뀝니다.\nESC는 일시정지 전용입니다. 변경 내용은 자동 저장됩니다.",
            22, new Vector2(0, -90), new Vector2(1300, 120), Color.white);
        ActionButton("기본값 복원 (J / K)", -215, () => { RunKeyBindings.Reset(); ShowKeySettings(); });
        ActionButton("메인메뉴", -315, ShowMenu);
        FocusFirstButton();
    }

    private void BeginBinding(bool gravity)
    {
        waitingForKey = true;
        rebindingGravity = gravity;
        rebindStartedFrame = Time.frameCount;
        keySettingsHint.text = "사용할 키를 누르세요. (ESC: 취소)";
        foreach (var button in menuButtons) button.interactable = false;
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
    }

    private void CaptureBinding()
    {
        if (Time.frameCount <= rebindStartedFrame || Keyboard.current == null) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            lastSubmitFrame = Time.frameCount;
            ShowKeySettings();
            return;
        }
        foreach (var key in Keyboard.current.allKeys)
        {
            if (!key.wasPressedThisFrame || !RunKeyBindings.Assign(rebindingGravity, key.keyCode)) continue;
            // Prevent Enter used as a binding from also submitting the new page.
            lastSubmitFrame = Time.frameCount;
            ShowKeySettings();
            return;
        }
    }

    private void ShowResult()
    {
        NewPage(true);
        Label(page.transform, "Kicker", "SIGNAL DELIVERED  /  FINISH", 20, new Vector2(0, 325), new Vector2(900, 40), cyan);
        Label(page.transform, "Title", "RUN COMPLETE", 66, new Vector2(0, 225), new Vector2(1100, 110), Color.white);
        Label(page.transform, "Record", $"{resultTime:0.00} SEC     /     {resultCollisionMisses} MISS  /  {resultMisses} LONG MISS", 30,
            new Vector2(0, 115), new Vector2(1100, 60), pink);
        string timing = bgm != null && bgm.clip != null
            ? $"BGM {bgm.clip.length:0.00} SEC   /   FINISH GAP {resultTime - bgm.clip.length:+0.00;-0.00;0.00} SEC"
            : "RECORD SAVED ON THIS DEVICE";
        Label(page.transform, "Music timing", timing, 18, new Vector2(0, 50), new Vector2(1100, 40), cyan);
        ActionButton("RESTART", -55, () => Load(GameScene), true);
        ActionButton("RANKING", -147, ShowRanking);
        ActionButton("MAIN MENU", -239, () => Load(MenuScene));
        Label(page.transform, "Saved", "RECORD SAVED ON THIS DEVICE", 17, new Vector2(0, -330), new Vector2(900, 40), cyan);
        FocusFirstButton();
    }

    private void ShowRanking()
    {
        NewPage(true);
        Label(page.transform, "Title", "LOCAL RANKING", 54, new Vector2(0, 345), new Vector2(1100, 90), Color.white);
        Label(page.transform, "Rule", "THIS DEVICE  /  FEWEST TOTAL MISSES, THEN FASTEST TIME", 19,
            new Vector2(0, 273), new Vector2(1250, 40), cyan);
        var entries = RunLeaderboard.Load();
        if (entries.Count == 0)
            Label(page.transform, "Empty", "NO RUNS YET. YOUR FIRST FINISH STARTS HERE.", 24,
                Vector2.zero, new Vector2(1300, 70), Color.white);
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            Label(page.transform, "Rank " + (i + 1), $"{i + 1:00}     {e.seconds,8:0.00} SEC     {e.collisionMisses,3} MISS   {e.misses,3} LONG MISS     {e.date}",
                24, new Vector2(0, 200 - i * 43), new Vector2(1250, 40), i == 0 ? pink : Color.white);
        }
        ActionButton("BACK", -325, () => { if (completed) ShowResult(); else ShowMenu(); });
        FocusFirstButton();
    }

    private void Load(string scene)
    {
        if (loading) return;
        loading = true;
        // Ranking only replaces the page. Leaving the scene ends this track;
        // the destination starts its own music from the beginning.
        if (bgm != null) bgm.Stop();
        gameStartRequested = scene == GameScene;
        SceneManager.LoadSceneAsync(scene);
    }

    private Button ActionButton(string title, float y, UnityEngine.Events.UnityAction action, bool primary = false, bool navigationSound = false)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(page.transform, false);
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(430, 72); rect.anchoredPosition = new Vector2(0, y);
        go.GetComponent<Image>().color = primary ? cyan : new Color(0.065f, 0.08f, 0.18f, 0.98f);
        var outline = go.AddComponent<Outline>(); outline.effectColor = cyan; outline.effectDistance = new Vector2(2, -2);
        var button = go.GetComponent<Button>(); button.onClick.AddListener(() =>
        {
            if (loading || lastSubmitFrame == Time.frameCount) return;
            lastSubmitFrame = Time.frameCount;
            if (navigationSound) RunMenuAudio.Move(); else RunMenuAudio.Select();
            action();
        });
        var colors = button.colors; colors.highlightedColor = new Color(1f, 0.55f, 0.8f);
        colors.selectedColor = colors.highlightedColor; button.colors = colors;
        Label(go.transform, "Label", title, 25, Vector2.zero, rect.sizeDelta, primary ? new Color(0.015f, 0.035f, 0.09f) : Color.white);
        return button;
    }

    private Text Label(Transform parent, string name, string value, int size, Vector2 position, Vector2 dimensions, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.sizeDelta = dimensions; rect.anchoredPosition = position;
        var text = go.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter; text.color = color; text.raycastTarget = false;
        return text;
    }

    private void FocusFirstButton()
    {
        menuButtons = page.GetComponentsInChildren<Button>();
        for (int i = 0; i < menuButtons.Length; i++)
        {
            menuButtons[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = menuButtons[(i + menuButtons.Length - 1) % menuButtons.Length],
                selectOnDown = menuButtons[(i + 1) % menuButtons.Length]
            };
        }
        if (EventSystem.current != null && menuButtons.Length > 0)
        {
            EventSystem.current.firstSelectedGameObject = menuButtons[0].gameObject;
            EventSystem.current.SetSelectedGameObject(menuButtons[0].gameObject);
        }
        highlightedButton = null;
        UpdateMenuSelection();
    }

    private void ConfigureMenuInput()
    {
        var events = FindAnyObjectByType<EventSystem>();
        events.sendNavigationEvents = true;
        var module = events.GetComponent<InputSystemUIInputModule>();
        if (module == null) module = events.gameObject.AddComponent<InputSystemUIInputModule>();
        foreach (var other in events.GetComponents<BaseInputModule>())
            if (other != module) other.enabled = false;
        module.enabled = true;
        module.deselectOnBackgroundClick = false;
        module.moveRepeatDelay = 0.35f;
        module.moveRepeatRate = 0.12f;

        // Keep pointer actions intact. One UI module owns keyboard movement and
        // submission, avoiding duplicate Enter events or double selection jumps.
        menuActions = ScriptableObject.CreateInstance<InputActionAsset>();
        var map = menuActions.AddActionMap("Menu");
        var move = map.AddAction("Move", InputActionType.PassThrough);
        move.expectedControlType = "Vector2";
        move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s");
        move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow");
        move.AddBinding("<Gamepad>/dpad");
        var submit = map.AddAction("Submit", InputActionType.Button);
        submit.AddBinding("<Keyboard>/enter");
        submit.AddBinding("<Keyboard>/numpadEnter");
        submit.AddBinding("<Gamepad>/buttonSouth");
        menuMove = InputActionReference.Create(move);
        menuSubmit = InputActionReference.Create(submit);
        module.move = menuMove;
        module.submit = menuSubmit;
        menuActions.Enable();
    }

    private void UpdateMenuSelection()
    {
        if (loading || page == null || menuButtons == null || menuButtons.Length == 0 || EventSystem.current == null) return;
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.transform.IsChildOf(page.transform))
        {
            selected = menuButtons[0].gameObject;
            EventSystem.current.SetSelectedGameObject(selected);
        }
        if (selected == highlightedButton) return;
        if (highlightedButton != null) RunMenuAudio.Move();
        highlightedButton = selected;
        foreach (var button in menuButtons)
        {
            bool focused = button.gameObject == selected;
            var outline = button.GetComponent<Outline>();
            outline.effectColor = focused ? pink : cyan;
            outline.effectDistance = focused ? new Vector2(4, -4) : new Vector2(2, -2);
            button.GetComponentInChildren<Text>().text = focused && button != collectionPrevious && button != collectionNext ? $">  {button.name}  <" : button.name;
        }
    }

    private void OnDestroy()
    {
        if (menuActions != null) { menuActions.Disable(); Destroy(menuActions); }
        if (menuMove != null) Destroy(menuMove);
        if (menuSubmit != null) Destroy(menuSubmit);
        if (Instance == this) { Instance = null; Time.timeScale = 1f; AudioListener.pause = false; }
    }
}
