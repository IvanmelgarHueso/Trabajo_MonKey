using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public sealed class SuperMemoryGame : MonoBehaviour
{
    private enum GamePhase
    {
        Ready,
        Showing,
        Playing,
        Transition,
        GameOver
    }

    [Header("HUD")]
    [SerializeField] private TMP_Text scoreValue;
    [SerializeField] private TMP_Text bestValue;
    [SerializeField] private TMP_Text levelValue;
    [SerializeField] private TMP_Text livesValue;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text helperText;

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_Text startButtonLabel;
    [SerializeField] private Button eyeButton;
    [SerializeField] private TMP_Text eyeCount;
    [SerializeField] private Button hintButton;
    [SerializeField] private TMP_Text hintCount;
    [SerializeField] private Button shieldButton;
    [SerializeField] private TMP_Text shieldCount;

    [Header("Board")]
    [SerializeField] private Button[] tileButtons = new Button[9];
    [SerializeField] private Image[] tileImages = new Image[9];
    [SerializeField] private TMP_Text[] tileLabels = new TMP_Text[9];

    private readonly int[] numberAtCell = new int[9];
    private readonly int[] cellForStep = new int[9];
    private readonly bool[] solvedCells = new bool[9];

    private static readonly Color EmptyCellColor = new Color32(61, 61, 68, 255);
    private static readonly Color EmptyTargetColor = new Color32(73, 72, 79, 255);
    private static readonly Color RevealedColor = new Color32(243, 116, 83, 255);
    private static readonly Color SolvedColor = new Color32(83, 207, 168, 255);
    private static readonly Color WrongColor = new Color32(248, 83, 92, 255);
    private static readonly Color WildColor = new Color32(250, 183, 91, 255);

    private GamePhase phase = GamePhase.Ready;
    private Coroutine activeRoutine;
    private int score;
    private int bestScore;
    private int roundNumber = 1;
    private int sequenceLength = 3;
    private int currentStep;
    private int lives = 3;
    private int eyesRemaining = 14;
    private int hintsRemaining = 1;
    private int shieldsRemaining = 1;
    private bool shieldActive;
    private bool temporarilyRevealing;
    private float revealSeconds = 1.0f;

    public void Configure(
        TMP_Text score,
        TMP_Text best,
        TMP_Text level,
        TMP_Text lifeDisplay,
        TMP_Text status,
        TMP_Text helper,
        Button start,
        TMP_Text startLabel,
        Button eye,
        TMP_Text eyeUses,
        Button hint,
        TMP_Text hintUses,
        Button shield,
        TMP_Text shieldUses,
        Button[] cells,
        Image[] cellImages,
        TMP_Text[] cellLabels)
    {
        scoreValue = score;
        bestValue = best;
        levelValue = level;
        livesValue = lifeDisplay;
        statusText = status;
        helperText = helper;
        startButton = start;
        startButtonLabel = startLabel;
        eyeButton = eye;
        eyeCount = eyeUses;
        hintButton = hint;
        hintCount = hintUses;
        shieldButton = shield;
        shieldCount = shieldUses;
        tileButtons = cells;
        tileImages = cellImages;
        tileLabels = cellLabels;
    }

    private void Awake()
    {
        if (scoreValue == null)
            CreateRuntimeInterface();
        bestScore = PlayerPrefs.GetInt("SuperMemoryNumbers_BestScore", 0);
        BindButtons();
        ShowReadyState();
    }

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
        StopAllCoroutines();
        activeRoutine = null;
    }

    private void BindButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartOrRestart);
            startButton.onClick.AddListener(StartOrRestart);
        }

        if (eyeButton != null)
        {
            eyeButton.onClick.RemoveListener(UseEye);
            eyeButton.onClick.AddListener(UseEye);
        }

        if (hintButton != null)
        {
            hintButton.onClick.RemoveListener(UseHint);
            hintButton.onClick.AddListener(UseHint);
        }

        if (shieldButton != null)
        {
            shieldButton.onClick.RemoveListener(UseShield);
            shieldButton.onClick.AddListener(UseShield);
        }

        if (tileButtons == null)
            return;

        for (int i = 0; i < tileButtons.Length; i++)
        {
            if (tileButtons[i] == null)
                continue;

            int cellIndex = i;
            tileButtons[i].onClick.RemoveAllListeners();
            tileButtons[i].onClick.AddListener(() => OnCellPressed(cellIndex));
        }
    }

    private void UnbindButtons()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(StartOrRestart);
        if (eyeButton != null)
            eyeButton.onClick.RemoveListener(UseEye);
        if (hintButton != null)
            hintButton.onClick.RemoveListener(UseHint);
        if (shieldButton != null)
            shieldButton.onClick.RemoveListener(UseShield);

        if (tileButtons == null)
            return;

        for (int i = 0; i < tileButtons.Length; i++)
        {
            if (tileButtons[i] != null)
                tileButtons[i].onClick.RemoveAllListeners();
        }
    }

    private void ShowReadyState()
    {
        phase = GamePhase.Ready;
        score = 0;
        roundNumber = 1;
        sequenceLength = 3;
        lives = 3;
        eyesRemaining = 14;
        hintsRemaining = 1;
        shieldsRemaining = 1;
        shieldActive = false;
        temporarilyRevealing = false;
        currentStep = 0;

        if (statusText != null)
            statusText.text = "PREPARADO";

        if (helperText != null)
            helperText.text = "Memoriza los números y tócalos del menor al mayor.";

        if (startButtonLabel != null)
            startButtonLabel.text = "COMENZAR";

        if (startButton != null)
            startButton.gameObject.SetActive(true);

        for (int i = 0; i < numberAtCell.Length; i++)
        {
            numberAtCell[i] = 0;
            solvedCells[i] = false;
            if (tileLabels != null && i < tileLabels.Length && tileLabels[i] != null)
                tileLabels[i].text = string.Empty;
            if (tileImages != null && i < tileImages.Length && tileImages[i] != null)
                tileImages[i].color = EmptyCellColor;
        }

        UpdateHud();
        SetTileInteraction(false);
    }

    private void StartOrRestart()
    {
        if (phase != GamePhase.Ready && phase != GamePhase.GameOver)
            return;

        score = 0;
        roundNumber = 1;
        sequenceLength = 3;
        lives = 3;
        eyesRemaining = 14;
        hintsRemaining = 1;
        shieldsRemaining = 1;
        shieldActive = false;
        currentStep = 0;
        BeginRound(true);
    }

    private void BeginRound(bool createNewLayout)
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        if (createNewLayout)
            CreateSequenceLayout();

        currentStep = 0;
        shieldActive = false;
        temporarilyRevealing = false;

        for (int i = 0; i < solvedCells.Length; i++)
            solvedCells[i] = false;

        phase = GamePhase.Showing;
        revealSeconds = Mathf.Max(0.7f, 1.0f - Mathf.Max(0, roundNumber - 1) * 0.035f);

        if (statusText != null)
            statusText.text = roundNumber == 1 ? "MEMORIZA" : "NUEVA RONDA";

        if (helperText != null)
        {
            helperText.text = roundNumber >= 2
                ? "El último número aparece como * y cierra la secuencia."
                : "Observa la cuadrícula: tienes un segundo para memorizarla.";
        }

        if (startButton != null)
            startButton.gameObject.SetActive(false);

        UpdateHud();
        RenderBoard(true);
        SetTileInteraction(false);
        activeRoutine = StartCoroutine(HidePreviewAfterDelay());
    }

    private void CreateSequenceLayout()
    {
        for (int i = 0; i < numberAtCell.Length; i++)
            numberAtCell[i] = 0;

        int[] available = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        for (int i = available.Length - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            int temporary = available[i];
            available[i] = available[randomIndex];
            available[randomIndex] = temporary;
        }

        for (int step = 0; step < sequenceLength; step++)
        {
            int cell = available[step];
            cellForStep[step] = cell;
            numberAtCell[cell] = step + 1;
        }
    }

    private IEnumerator HidePreviewAfterDelay()
    {
        yield return new WaitForSeconds(revealSeconds);
        phase = GamePhase.Playing;

        if (statusText != null)
            statusText.text = "TU TURNO";

        if (helperText != null)
            helperText.text = "Pulsa las casillas en orden. ¡Concéntrate!";

        RenderBoard(false);
        SetTileInteraction(true);
        activeRoutine = null;
    }

    private void OnCellPressed(int cellIndex)
    {
        if (phase != GamePhase.Playing || temporarilyRevealing)
            return;

        if (cellIndex < 0 || cellIndex >= numberAtCell.Length || solvedCells[cellIndex])
            return;

        int expectedCell = currentStep < sequenceLength ? cellForStep[currentStep] : -1;
        if (cellIndex == expectedCell)
        {
            solvedCells[cellIndex] = true;
            currentStep++;
            RenderBoard(false);
            SetTileInteraction(true);

            if (currentStep >= sequenceLength)
                activeRoutine = StartCoroutine(CompleteRound());
            else if (statusText != null)
                statusText.text = "¡BIEN!";
        }
        else
        {
            activeRoutine = StartCoroutine(HandleMistake(cellIndex));
        }
    }

    private IEnumerator HandleMistake(int cellIndex)
    {
        phase = GamePhase.Transition;
        SetTileInteraction(false);

        if (cellIndex >= 0 && cellIndex < tileImages.Length && tileImages[cellIndex] != null)
            tileImages[cellIndex].color = WrongColor;
        if (cellIndex >= 0 && cellIndex < tileLabels.Length && tileLabels[cellIndex] != null)
            tileLabels[cellIndex].text = "×";

        if (shieldActive)
        {
            shieldActive = false;
            if (statusText != null)
                statusText.text = "ESCUDO ACTIVADO";
            if (helperText != null)
                helperText.text = "El corazón te ha protegido. Continúa la secuencia.";
            UpdateHud();

            yield return new WaitForSeconds(0.55f);
            phase = GamePhase.Playing;
            RenderBoard(false);
            SetTileInteraction(true);
            activeRoutine = null;
            yield break;
        }

        lives = Mathf.Max(0, lives - 1);
        UpdateHud();

        if (lives <= 0)
        {
            phase = GamePhase.GameOver;
            if (score > bestScore)
            {
                bestScore = score;
                PlayerPrefs.SetInt("SuperMemoryNumbers_BestScore", bestScore);
                PlayerPrefs.Save();
            }

            RenderBoard(true);
            if (statusText != null)
                statusText.text = "FIN DE PARTIDA";
            if (helperText != null)
                helperText.text = "Tu puntuación: " + score.ToString("0000") + "  ·  Récord: " + bestScore.ToString("0000");
            if (startButtonLabel != null)
                startButtonLabel.text = "JUGAR OTRA VEZ";
            if (startButton != null)
                startButton.gameObject.SetActive(true);
            SetTileInteraction(false);
            UpdateHud();
            activeRoutine = null;
            yield break;
        }

        if (statusText != null)
            statusText.text = "CASI";

        if (helperText != null)
            helperText.text = "Has perdido una vida. Repasa la secuencia.";

        yield return new WaitForSeconds(0.75f);
        BeginRound(false);
    }

    private IEnumerator CompleteRound()
    {
        phase = GamePhase.Transition;
        SetTileInteraction(false);

        int points = 100 + roundNumber * 25;
        score += points;

        if (score > bestScore)
        {
            bestScore = score;
            PlayerPrefs.SetInt("SuperMemoryNumbers_BestScore", bestScore);
            PlayerPrefs.Save();
        }

        if (statusText != null)
            statusText.text = "¡SECUENCIA COMPLETA!";

        if (helperText != null)
            helperText.text = "+" + points + " PUNTOS  ·  PREPÁRATE PARA LA SIGUIENTE.";

        UpdateHud();
        yield return new WaitForSeconds(0.9f);

        roundNumber++;
        sequenceLength = Mathf.Min(9, sequenceLength + 1);
        BeginRound(true);
    }

    private void UseEye()
    {
        if (phase != GamePhase.Playing || temporarilyRevealing || eyesRemaining <= 0)
            return;

        eyesRemaining--;
        UpdateHud();
        activeRoutine = StartCoroutine(RevealAllTemporarily());
    }

    private IEnumerator RevealAllTemporarily()
    {
        temporarilyRevealing = true;
        SetTileInteraction(false);
        RenderBoard(true);

        if (statusText != null)
            statusText.text = "OJOS ABIERTOS";

        if (helperText != null)
            helperText.text = "Todos los números están visibles durante un instante.";

        yield return new WaitForSeconds(0.85f);
        temporarilyRevealing = false;
        phase = GamePhase.Playing;
        RenderBoard(false);
        SetTileInteraction(true);

        if (statusText != null)
            statusText.text = "TU TURNO";

        if (helperText != null)
            helperText.text = "Pulsa las casillas en orden. ¡Concéntrate!";

        activeRoutine = null;
    }

    private void UseHint()
    {
        if (phase != GamePhase.Playing || temporarilyRevealing || hintsRemaining <= 0 || currentStep >= sequenceLength)
            return;

        hintsRemaining--;
        UpdateHud();
        activeRoutine = StartCoroutine(ShowHintTemporarily());
    }

    private IEnumerator ShowHintTemporarily()
    {
        temporarilyRevealing = true;
        SetTileInteraction(false);

        int cell = cellForStep[currentStep];
        bool isWild = sequenceLength >= 4 && currentStep == sequenceLength - 1;

        if (cell >= 0 && cell < tileLabels.Length && tileLabels[cell] != null)
            tileLabels[cell].text = isWild ? "*" : (currentStep + 1).ToString();
        if (cell >= 0 && cell < tileImages.Length && tileImages[cell] != null)
            tileImages[cell].color = isWild ? WildColor : RevealedColor;

        if (statusText != null)
            statusText.text = "PISTA";

        if (helperText != null)
            helperText.text = isWild ? "El siguiente es el comodín *." : "El siguiente número es " + (currentStep + 1) + ".";

        yield return new WaitForSeconds(1.15f);
        temporarilyRevealing = false;
        phase = GamePhase.Playing;
        RenderBoard(false);
        SetTileInteraction(true);
        activeRoutine = null;
    }

    private void UseShield()
    {
        if (phase != GamePhase.Playing || temporarilyRevealing || shieldsRemaining <= 0 || shieldActive)
            return;

        shieldsRemaining--;
        shieldActive = true;
        UpdateHud();

        if (statusText != null)
            statusText.text = "CORAZÓN ACTIVO";

        if (helperText != null)
            helperText.text = "Tu próximo error no te quitará una vida.";
    }

    private void RenderBoard(bool revealTargets)
    {
        if (tileLabels == null || tileImages == null)
            return;

        for (int i = 0; i < tileLabels.Length; i++)
        {
            if (tileLabels[i] == null || i >= tileImages.Length || tileImages[i] == null)
                continue;

            int number = i < numberAtCell.Length ? numberAtCell[i] : 0;
            bool isSolved = number > 0 && solvedCells[i];
            bool isWildcard = number > 0 && sequenceLength >= 4 && number == sequenceLength;
            bool isVisible = number > 0 && (revealTargets || isSolved || isWildcard);

            if (number == 0)
            {
                tileLabels[i].text = string.Empty;
                tileImages[i].color = EmptyCellColor;
            }
            else if (isSolved)
            {
                tileLabels[i].text = isWildcard ? "*" : number.ToString();
                tileImages[i].color = SolvedColor;
            }
            else if (isVisible)
            {
                tileLabels[i].text = isWildcard ? "*" : number.ToString();
                tileImages[i].color = isWildcard ? WildColor : RevealedColor;
            }
            else
            {
                tileLabels[i].text = string.Empty;
                tileImages[i].color = EmptyTargetColor;
            }
        }

        UpdateHud();
    }

    private void SetTileInteraction(bool enabled)
    {
        if (tileButtons == null)
            return;

        for (int i = 0; i < tileButtons.Length; i++)
        {
            if (tileButtons[i] != null)
                tileButtons[i].interactable = enabled && (i >= solvedCells.Length || !solvedCells[i]);
        }
    }

    private void UpdateHud()
    {
        if (scoreValue != null)
            scoreValue.text = score.ToString("0000");

        if (bestValue != null)
            bestValue.text = bestScore.ToString("0000");

        if (levelValue != null)
            levelValue.text = "NIVEL " + roundNumber.ToString("00");

        if (livesValue != null)
        {
            int safeLives = Mathf.Clamp(lives, 0, 3);
            livesValue.text = "<color=#F27758>" + new string('♥', safeLives)
                + "</color><color=#64646B>" + new string('♥', 3 - safeLives) + "</color>";
        }

        if (eyeCount != null)
            eyeCount.text = eyesRemaining.ToString();

        if (hintCount != null)
            hintCount.text = hintsRemaining.ToString();

        if (shieldCount != null)
            shieldCount.text = shieldsRemaining.ToString();

        if (eyeButton != null)
            eyeButton.interactable = phase == GamePhase.Playing && !temporarilyRevealing && eyesRemaining > 0;

        if (hintButton != null)
            hintButton.interactable = phase == GamePhase.Playing && !temporarilyRevealing && hintsRemaining > 0;

        if (shieldButton != null)
            shieldButton.interactable = phase == GamePhase.Playing && !temporarilyRevealing && shieldsRemaining > 0 && !shieldActive;
    }
    private static readonly Color RuntimeBackgroundColor = new Color32(16, 17, 25, 255);
    private static readonly Color RuntimePanelColor = new Color32(34, 35, 44, 255);
    private static readonly Color RuntimeCreamColor = new Color32(250, 245, 239, 255);
    private static readonly Color RuntimeMutedColor = new Color32(174, 174, 182, 255);
    private static Sprite runtimeWhiteSprite;

    private void CreateRuntimeInterface()
    {
        GameObject legacyCanvas = GameObject.Find("Canvas");
        if (legacyCanvas != null)
            legacyCanvas.SetActive(false);

        GameObject legacyEventSystem = GameObject.Find("EventSystem");
        if (legacyEventSystem != null)
            legacyEventSystem.SetActive(false);

        if (runtimeWhiteSprite == null)
        {
            runtimeWhiteSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                100f);
            runtimeWhiteSprite.name = "SuperMemoryWhiteSprite";
            runtimeWhiteSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        GameObject canvasObject = new GameObject(
            "Super Memory Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        GameObject background = BuildSolidPanel("Background", canvasRect, RuntimeBackgroundColor, runtimeWhiteSprite);
        StretchRect(background.GetComponent<RectTransform>(), 0f);

        BuildRuntimeLabel("Title", canvasRect, "SUPER MEMORY", font, 49f, RevealedColor,
            TextAlignmentOptions.Center, FontStyles.Bold, TopRect(70f, 920f, 70f), false);
        BuildRuntimeLabel("Subtitle", canvasRect, "NUMBERS", font, 26f, RuntimeCreamColor,
            TextAlignmentOptions.Center, FontStyles.Bold, TopRect(133f, 520f, 42f), false);

        GameObject statsPanel = BuildRoundedPanel("ScoreAndLives", canvasRect, RuntimePanelColor);
        PositionRect(statsPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -218f), new Vector2(940f, 132f));

        TMP_Text score = BuildRuntimeLabel("ScoreValue", statsPanel.transform, "0000", font, 43f,
            RuntimeCreamColor, TextAlignmentOptions.Center, FontStyles.Bold, CenterRect(-300f, 12f, 250f, 54f), false);
        BuildRuntimeLabel("ScoreCaption", statsPanel.transform, "PUNTOS", font, 22f, RuntimeMutedColor,
            TextAlignmentOptions.Center, FontStyles.Bold, CenterRect(-300f, -36f, 250f, 25f), false);
        TMP_Text best = BuildRuntimeLabel("BestValue", statsPanel.transform, "0000", font, 23f,
            RuntimeMutedColor, TextAlignmentOptions.Center, FontStyles.Normal, CenterRect(-300f, -57f, 250f, 24f), false);

        TMP_Text level = BuildRuntimeLabel("LevelValue", statsPanel.transform, "NIVEL 01", font, 29f,
            RuntimeCreamColor, TextAlignmentOptions.Center, FontStyles.Bold, CenterRect(0f, 9f, 280f, 48f), false);
        BuildRuntimeLabel("LevelCaption", statsPanel.transform, "SECUENCIA EN ORDEN", font, 20f, RuntimeMutedColor,
            TextAlignmentOptions.Center, FontStyles.Normal, CenterRect(0f, -34f, 280f, 25f), false);

        TMP_Text lifeDisplay = BuildRuntimeLabel("LivesValue", statsPanel.transform, "<color=#F27758>♥♥♥</color>", font, 34f,
            RuntimeCreamColor, TextAlignmentOptions.Center, FontStyles.Bold, CenterRect(300f, 11f, 230f, 48f), false);
        BuildRuntimeLabel("LivesCaption", statsPanel.transform, "VIDAS", font, 22f, RuntimeMutedColor,
            TextAlignmentOptions.Center, FontStyles.Bold, CenterRect(300f, -34f, 230f, 25f), false);

        TMP_Text status = BuildRuntimeLabel("Status", canvasRect, "PREPARADO", font, 30f,
            RuntimeCreamColor, TextAlignmentOptions.Center, FontStyles.Bold, TopRect(340f, 920f, 48f), false);
        TMP_Text helper = BuildRuntimeLabel("Instructions", canvasRect,
            "Memoriza los números y tócalos del menor al mayor.",
            font, 24f, RuntimeMutedColor, TextAlignmentOptions.Center, FontStyles.Normal,
            TopRect(390f, 950f, 42f), true);

        GameObject boardPanel = BuildSolidPanel("MemoryBoard", canvasRect, new Color32(27, 28, 36, 255), runtimeWhiteSprite);
        PositionRect(boardPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(860f, 860f));

        GameObject gridObject = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        RectTransform gridRect = gridObject.GetComponent<RectTransform>();
        gridRect.SetParent(boardPanel.transform, false);
        StretchRect(gridRect, 18f);

        GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.cellSize = new Vector2(248f, 248f);
        grid.spacing = new Vector2(18f, 18f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        Button[] cells = new Button[9];
        Image[] cellImages = new Image[9];
        TMP_Text[] cellLabels = new TMP_Text[9];

        for (int i = 0; i < 9; i++)
        {
            GameObject cell = new GameObject(
                "Casilla " + (i + 1).ToString("00"),
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));

            cell.GetComponent<RectTransform>().SetParent(gridRect, false);
            Image cellImage = cell.GetComponent<Image>();
            cellImage.color = EmptyCellColor;

            Button cellButton = cell.GetComponent<Button>();
            StyleRuntimeButton(cellButton, cellImage);

            TMP_Text cellLabel = BuildRuntimeLabel(
                "Number",
                cell.transform,
                string.Empty,
                font,
                86f,
                RuntimeCreamColor,
                TextAlignmentOptions.Center,
                FontStyles.Bold,
                FillRect(),
                false);

            cells[i] = cellButton;
            cellImages[i] = cellImage;
            cellLabels[i] = cellLabel;
        }

        Button start = BuildRuntimeButton("StartButton", canvasRect, RevealedColor);
        PositionRect(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(440f, 88f));
        TMP_Text startLabel = BuildRuntimeLabel("Label", start.transform, "COMENZAR", font, 25f,
            new Color32(26, 27, 34, 255), TextAlignmentOptions.Center, FontStyles.Bold, FillRect(), false);

        GameObject powerupsRoot = new GameObject("PowerUps", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        RectTransform powerupsRect = powerupsRoot.GetComponent<RectTransform>();
        powerupsRect.SetParent(canvasRect, false);
        PositionRect(powerupsRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 166f), new Vector2(870f, 112f));

        HorizontalLayoutGroup powerLayout = powerupsRoot.GetComponent<HorizontalLayoutGroup>();
        powerLayout.spacing = 16f;
        powerLayout.childAlignment = TextAnchor.MiddleCenter;
        powerLayout.childControlWidth = true;
        powerLayout.childControlHeight = true;
        powerLayout.childForceExpandWidth = false;
        powerLayout.childForceExpandHeight = false;

        TMP_Text eyeUses;
        Button eye = BuildRuntimePowerUp(powerupsRoot.transform, font, "OJO", "REVELAR", RevealedColor, out eyeUses);
        TMP_Text hintUses;
        Button hint = BuildRuntimePowerUp(powerupsRoot.transform, font, "LUZ", "SIGUIENTE",
            new Color32(250, 190, 96, 255), out hintUses);
        TMP_Text shieldUses;
        Button shield = BuildRuntimePowerUp(powerupsRoot.transform, font, "♥", "PROTEGER",
            new Color32(242, 119, 153, 255), out shieldUses);

        BuildRuntimeLabel(
            "FooterHelp",
            canvasRect,
            "OJO: revela todos  ·  LUZ: da una pista  ·  CORAZÓN: evita un error",
            font,
            22f,
            RuntimeMutedColor,
            TextAlignmentOptions.Center,
            FontStyles.Normal,
            BottomRect(48f, 1040f, 50f),
            true);

        GameObject eventSystemObject = new GameObject("Super Memory Event System", typeof(EventSystem));
        InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();

        Configure(
            score, best, level, lifeDisplay, status, helper,
            start, startLabel, eye, eyeUses, hint, hintUses, shield, shieldUses,
            cells, cellImages, cellLabels);
    }

    private Button BuildRuntimePowerUp(Transform parent, TMP_FontAsset font, string title, string caption, Color titleColor, out TMP_Text count)
    {
        GameObject buttonObject = new GameObject(
            title + "PowerUp",
            typeof(RectTransform),
            typeof(CronostasisRoundedImage),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.GetComponent<RectTransform>().SetParent(parent, false);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 279f;
        layout.preferredHeight = 112f;
        layout.minWidth = 200f;
        layout.minHeight = 90f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = RuntimePanelColor;

        Button button = buttonObject.GetComponent<Button>();
        StyleRuntimeButton(button, image);

        BuildRuntimeLabel("Title", buttonObject.transform, title, font, 26f, titleColor,
            TextAlignmentOptions.Center, FontStyles.Bold, CenterRect(0f, 15f, 240f, 44f), false);
        BuildRuntimeLabel("Caption", buttonObject.transform, caption, font, 19f, RuntimeMutedColor,
            TextAlignmentOptions.Center, FontStyles.Bold, CenterRect(0f, -29f, 240f, 24f), false);
        count = BuildRuntimeLabel("Count", buttonObject.transform, "0", font, 22f, RuntimeCreamColor,
            TextAlignmentOptions.Center, FontStyles.Bold, TopRightRect(16f, 56f, 26f), false);

        return button;
    }

    private static GameObject BuildRoundedPanel(string name, Transform parent, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CronostasisRoundedImage));
        panel.GetComponent<RectTransform>().SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private static GameObject BuildSolidPanel(string name, Transform parent, Color color, Sprite sprite)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.GetComponent<RectTransform>().SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private static Button BuildRuntimeButton(string name, Transform parent, Color color)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CronostasisRoundedImage), typeof(Button));
        buttonObject.GetComponent<RectTransform>().SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        StyleRuntimeButton(button, image);
        return button;
    }

    private static void StyleRuntimeButton(Button button, Image image)
    {
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color32(220, 220, 220, 255);
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static TMP_Text BuildRuntimeLabel(
        string name,
        Transform parent,
        string content,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        FontStyles style,
        RectSpec spec,
        bool wrap)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        ApplyRect(rect, spec);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.margin = Vector4.zero;
        text.richText = true;
        return text;
    }

    private struct RectSpec
    {
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 Pivot;
        public Vector2 Position;
        public Vector2 Size;
    }

    private static RectSpec TopRect(float top, float width, float height)
    {
        return new RectSpec
        {
            AnchorMin = new Vector2(0.5f, 1f),
            AnchorMax = new Vector2(0.5f, 1f),
            Pivot = new Vector2(0.5f, 1f),
            Position = new Vector2(0f, -top),
            Size = new Vector2(width, height)
        };
    }

    private static RectSpec BottomRect(float bottom, float width, float height)
    {
        return new RectSpec
        {
            AnchorMin = new Vector2(0.5f, 0f),
            AnchorMax = new Vector2(0.5f, 0f),
            Pivot = new Vector2(0.5f, 0.5f),
            Position = new Vector2(0f, bottom),
            Size = new Vector2(width, height)
        };
    }

    private static RectSpec CenterRect(float x, float y, float width, float height)
    {
        return new RectSpec
        {
            AnchorMin = new Vector2(0.5f, 0.5f),
            AnchorMax = new Vector2(0.5f, 0.5f),
            Pivot = new Vector2(0.5f, 0.5f),
            Position = new Vector2(x, y),
            Size = new Vector2(width, height)
        };
    }

    private static RectSpec TopRightRect(float inset, float width, float height)
    {
        return new RectSpec
        {
            AnchorMin = new Vector2(1f, 1f),
            AnchorMax = new Vector2(1f, 1f),
            Pivot = new Vector2(1f, 1f),
            Position = new Vector2(-inset, -10f),
            Size = new Vector2(width, height)
        };
    }

    private static RectSpec FillRect()
    {
        return new RectSpec
        {
            AnchorMin = Vector2.zero,
            AnchorMax = Vector2.one,
            Pivot = new Vector2(0.5f, 0.5f),
            Position = Vector2.zero,
            Size = Vector2.zero
        };
    }

    private static void PositionRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void ApplyRect(RectTransform rect, RectSpec spec)
    {
        PositionRect(rect, spec.AnchorMin, spec.AnchorMax, spec.Pivot, spec.Position, spec.Size);
    }

    private static void StretchRect(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

}


