using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public sealed class CronostasisStopwatch : MonoBehaviour
{
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private Button mainButton;
    [SerializeField] private TMP_Text mainButtonLabel;
    [SerializeField] private Image mainButtonImage;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button lapButton;
    [SerializeField] private ScrollRect lapScroll;
    [SerializeField] private RectTransform lapContent;
    [SerializeField] private GameObject lapRowTemplate;
    [SerializeField] private Sprite pillSprite;
    [SerializeField] private TMP_FontAsset fontAsset;

    private readonly List<LapEntry> laps = new List<LapEntry>();
    private bool isRunning;
    private bool isBound;
    private double elapsedBeforeStart;
    private double startedAt;
    private double lastLapTotal;
    private long lastRenderedCentisecond = -1;

    private static readonly Color MainColor = new Color32(255, 103, 108, 255);
    private static readonly Color SecondaryColor = new Color32(32, 34, 41, 255);
    private static readonly Color FastColor = new Color32(0, 216, 145, 255);

    private struct LapEntry
    {
        public int Number;
        public double Split;
        public double Total;

        public LapEntry(int number, double split, double total)
        {
            Number = number;
            Split = split;
            Total = total;
        }
    }

    public void Configure(
        TMP_Text timer,
        Button main,
        TMP_Text mainLabel,
        Image mainImage,
        Button reset,
        Button lap,
        ScrollRect scroll,
        RectTransform content,
        GameObject rowTemplate,
        Sprite roundedSprite,
        TMP_FontAsset font)
    {
        timerLabel = timer;
        mainButton = main;
        mainButtonLabel = mainLabel;
        mainButtonImage = mainImage;
        resetButton = reset;
        lapButton = lap;
        lapScroll = scroll;
        lapContent = content;
        lapRowTemplate = rowTemplate;
        pillSprite = roundedSprite;
        fontAsset = font;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        BindButtons();
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        isRunning = false;
        elapsedBeforeStart = 0d;
        startedAt = 0d;
        lastLapTotal = 0d;
        laps.Clear();
        lastRenderedCentisecond = -1;
        UpdateTimerLabel(0d, true);
        RefreshRows();
        UpdateControls();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    private void Update()
    {
        if (!isRunning)
            return;

        double elapsed = GetElapsedSeconds();
        long centisecond = ToCentiseconds(elapsed);
        if (centisecond != lastRenderedCentisecond)
            UpdateTimerLabel(elapsed, false);
    }

    private void ResolveReferences()
    {
        if (timerLabel == null)
            timerLabel = FindText("TextoTiempoPrincipal");

        Transform primary = transform.Find("BotonIniciarDetener");
        if (mainButton == null && primary != null)
            mainButton = primary.GetComponent<Button>();
        if (mainButtonImage == null && primary != null)
            mainButtonImage = primary.GetComponent<Image>();
        if (mainButtonLabel == null && primary != null)
            mainButtonLabel = primary.GetComponentInChildren<TMP_Text>(true);

        Transform reset = transform.Find("BotonRestablecer");
        if (resetButton == null && reset != null)
            resetButton = reset.GetComponent<Button>();

        Transform lap = transform.Find("BotonVuelta");
        if (lapButton == null && lap != null)
            lapButton = lap.GetComponent<Button>();

        Transform table = transform.Find("TablaVueltas");
        if (table != null)
        {
            if (lapScroll == null)
                lapScroll = table.GetComponent<ScrollRect>();
            if (lapContent == null)
            {
                Transform content = table.Find("Viewport/Content");
                if (content != null)
                    lapContent = content as RectTransform;
            }
        }

        if (lapRowTemplate == null && lapContent != null)
        {
            Transform template = lapContent.Find("FilaVueltaTemplate");
            if (template != null)
                lapRowTemplate = template.gameObject;
        }

        if (fontAsset == null && timerLabel != null)
            fontAsset = timerLabel.font;
        if (mainButtonLabel == null && mainButton != null)
            mainButtonLabel = mainButton.GetComponentInChildren<TMP_Text>(true);
        if (mainButtonImage == null && mainButton != null)
            mainButtonImage = mainButton.GetComponent<Image>();
    }

    private TMP_Text FindText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private void BindButtons()
    {
        if (isBound)
            return;

        if (mainButton != null)
            mainButton.onClick.AddListener(ToggleRunning);
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetStopwatch);
        if (lapButton != null)
            lapButton.onClick.AddListener(RecordLap);

        isBound = true;
    }

    private void UnbindButtons()
    {
        if (!isBound)
            return;

        if (mainButton != null)
            mainButton.onClick.RemoveListener(ToggleRunning);
        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetStopwatch);
        if (lapButton != null)
            lapButton.onClick.RemoveListener(RecordLap);

        isBound = false;
    }

    private double GetElapsedSeconds()
    {
        if (!isRunning)
            return elapsedBeforeStart;

        return elapsedBeforeStart + Math.Max(0d, Time.realtimeSinceStartupAsDouble - startedAt);
    }

    private void ToggleRunning()
    {
        if (isRunning)
        {
            elapsedBeforeStart = GetElapsedSeconds();
            isRunning = false;
        }
        else
        {
            startedAt = Time.realtimeSinceStartupAsDouble;
            isRunning = true;
        }

        UpdateTimerLabel(GetElapsedSeconds(), true);
        UpdateControls();
    }

    private void ResetStopwatch()
    {
        isRunning = false;
        elapsedBeforeStart = 0d;
        startedAt = 0d;
        lastLapTotal = 0d;
        laps.Clear();
        lastRenderedCentisecond = -1;
        UpdateTimerLabel(0d, true);
        RefreshRows();
        UpdateControls();
    }

    private void RecordLap()
    {
        if (!isRunning)
            return;

        double total = GetElapsedSeconds();
        double split = Math.Max(0d, total - lastLapTotal);
        laps.Insert(0, new LapEntry(laps.Count + 1, split, total));
        lastLapTotal = total;
        RefreshRows();
        UpdateControls();
    }

    private void UpdateTimerLabel(double elapsed, bool force)
    {
        if (timerLabel == null)
            return;

        long centiseconds = ToCentiseconds(elapsed);
        if (!force && centiseconds == lastRenderedCentisecond)
            return;

        lastRenderedCentisecond = centiseconds;
        long wholeSeconds = centiseconds / 100;
        long minutes = wholeSeconds / 60;
        long seconds = wholeSeconds % 60;
        long fraction = centiseconds % 100;
        timerLabel.text = string.Format(
            "<color=#F5F5F6>{0:00}:{1:00}</color><color=#A4A5A9>.{2:00}</color>",
            minutes,
            seconds,
            fraction);
    }

    private void UpdateControls()
    {
        if (mainButtonLabel != null)
            mainButtonLabel.text = isRunning ? "Detener" : (elapsedBeforeStart > 0d ? "Continuar" : "Iniciar");

        if (mainButtonImage != null)
            mainButtonImage.color = MainColor;

        if (resetButton != null)
            resetButton.interactable = GetElapsedSeconds() > 0d || laps.Count > 0;

        if (lapButton != null)
            lapButton.interactable = isRunning;

        if (mainButton != null)
            mainButton.interactable = true;
    }

    private void RefreshRows()
    {
        if (lapContent == null || lapRowTemplate == null)
            return;

        for (int i = lapContent.childCount - 1; i >= 0; i--)
        {
            Transform child = lapContent.GetChild(i);
            if (child.gameObject != lapRowTemplate)
                Destroy(child.gameObject);
        }

        if (laps.Count == 0)
        {
            if (lapScroll != null)
                lapScroll.verticalNormalizedPosition = 1f;
            return;
        }

        double fastest = double.MaxValue;
        for (int i = 0; i < laps.Count; i++)
            fastest = Math.Min(fastest, laps[i].Split);

        for (int i = 0; i < laps.Count; i++)
        {
            LapEntry entry = laps[i];
            GameObject row = Instantiate(lapRowTemplate, lapContent);
            row.name = "FilaVuelta_" + entry.Number.ToString("00");
            row.SetActive(true);
            row.transform.SetSiblingIndex(i);

            bool isFastest = Math.Abs(entry.Split - fastest) < 0.000001d;
            TMP_Text number = FindRowText(row.transform, "LapNumber");
            TMP_Text split = FindRowText(row.transform, "LapSplit");
            TMP_Text total = FindRowText(row.transform, "LapTotal");
            Transform badge = row.transform.Find("FastestBadge");

            if (number != null)
                number.text = "Vuelta " + entry.Number.ToString("00");
            if (split != null)
            {
                split.text = "+" + FormatTime(entry.Split);
                split.color = isFastest ? FastColor : new Color32(205, 208, 214, 255);
                RectTransform splitRect = split.transform as RectTransform;
                if (splitRect != null)
                {
                    Vector2 min = splitRect.offsetMin;
                    min.x = isFastest ? 48f : 0f;
                    splitRect.offsetMin = min;
                }
            }
            if (total != null)
                total.text = FormatTime(entry.Total);
            if (badge != null)
                badge.gameObject.SetActive(isFastest);
        }

        Canvas.ForceUpdateCanvases();
        if (lapScroll != null)
            lapScroll.verticalNormalizedPosition = 1f;
    }

    private static TMP_Text FindRowText(Transform row, string childName)
    {
        Transform child = row.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static long ToCentiseconds(double seconds)
    {
        return (long)Math.Floor(Math.Max(0d, seconds) * 100d + 0.0000001d);
    }

    private static string FormatTime(double seconds)
    {
        long centiseconds = ToCentiseconds(seconds);
        long wholeSeconds = centiseconds / 100;
        long minutes = wholeSeconds / 60;
        long remainder = wholeSeconds % 60;
        long fraction = centiseconds % 100;
        return string.Format("{0:00}:{1:00}.{2:00}", minutes, remainder, fraction);
    }
}

#if UNITY_EDITOR
public static class CronostasisStopwatchSceneBuilder
{
    private static readonly Color BackgroundColor = new Color32(15, 16, 18, 255);
    private static readonly Color DividerColor = new Color32(39, 41, 46, 255);
    private static readonly Color HeaderColor = new Color32(125, 132, 145, 255);
    private static readonly Color RowColor = new Color32(211, 214, 220, 255);
    private static readonly Color TotalColor = new Color32(245, 245, 246, 255);
    private static readonly Color MainColor = new Color32(255, 103, 108, 255);
    private static readonly Color SecondaryColor = new Color32(32, 34, 41, 255);
    private static readonly Color BadgeColor = new Color32(10, 47, 35, 255);
    private static readonly Color FastColor = new Color32(0, 216, 145, 255);

    [MenuItem("Cronostasis/Construir interfaz del cronómetro")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Detén Play Mode antes de construir la interfaz de Cronostasis.");
            return;
        }

        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "SampleScene")
        {
            Debug.LogError("Abre SampleScene antes de construir la interfaz de Cronostasis.");
            return;
        }

        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject == null)
        {
            Debug.LogError("No se encontró el Canvas existente de SampleScene.");
            return;
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (canvas == null || scaler == null)
        {
            Debug.LogError("El Canvas debe tener Canvas y CanvasScaler.");
            return;
        }

        TMP_Text timer = FindText(canvasObject.transform, "TextoTiempoPrincipal");
        TMP_Text lapHeader = FindText(canvasObject.transform, "CabeceraTabla/TextoVueltaHeader");
        Transform headerTransform = canvasObject.transform.Find("CabeceraTabla");
        Transform primaryTransform = canvasObject.transform.Find("BotonIniciarDetener");

        if (timer == null || lapHeader == null || headerTransform == null || primaryTransform == null)
        {
            Debug.LogError("Falta algún objeto de la interfaz existente; no se guardaron cambios.");
            return;
        }

        Undo.RecordObject(scaler, "Configurar escala de Cronostasis");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(453f, 882f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Transform backgroundTransform = canvasObject.transform.Find("Fondo");
        if (backgroundTransform != null)
        {
            UnityEngine.UI.Image background = backgroundTransform.GetComponent<UnityEngine.UI.Image>();
            if (background != null)
            {
                Undo.RecordObject(background, "Estilizar fondo de Cronostasis");
                SetFill(background.rectTransform);
                background.color = BackgroundColor;
                background.raycastTarget = false;
            }
        }

        TMP_FontAsset font = timer.font;
        RectTransform timerRect = timer.transform as RectTransform;
        SetTopCentered(timerRect, 164f, 430f, 94f);
        StyleText(timer, font, 74f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        timer.text = "<color=#F5F5F6>00:00</color><color=#A4A5A9>.00</color>";
        timer.characterSpacing = 1f;

        RectTransform headerRect = headerTransform as RectTransform;
        SetTopStretch(headerRect, 39f, 39f, 317f, 26f);
        UnityEngine.UI.HorizontalLayoutGroup horizontal = headerTransform.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (horizontal != null)
        {
            Undo.RecordObject(horizontal, "Ajustar cabecera de vueltas");
            horizontal.enabled = false;
        }

        StyleHeaderCell(lapHeader, font, 0f, 0.22f, HeaderColor, TextAlignmentOptions.MidlineLeft, 13f);
        SetCell(lapHeader.rectTransform, 0f, 0.22f, 12f, 0f);
        TMP_Text splitHeader = EnsureText(headerTransform, "TextoParcialHeader", lapHeader, font);
        StyleHeaderCell(splitHeader, font, 0.22f, 0.70f, HeaderColor, TextAlignmentOptions.Center, 13f);
        TMP_Text totalHeader = EnsureText(headerTransform, "TextoTotalHeader", lapHeader, font);
        StyleHeaderCell(totalHeader, font, 0.70f, 1f, HeaderColor, TextAlignmentOptions.MidlineRight, 13f);
        SetCell(totalHeader.rectTransform, 0.70f, 1f, 0f, 12f);

        UnityEngine.UI.Image headerDivider = EnsureImage(
            EnsureChild(canvasObject.transform, "LineaCabeceraTabla"),
            "LineaCabeceraTabla");
        SetTopStretch(headerDivider.rectTransform, 39f, 39f, 348f, 1f);
        headerDivider.color = DividerColor;
        headerDivider.raycastTarget = false;

        UnityEngine.UI.Button primaryButton = primaryTransform.GetComponent<UnityEngine.UI.Button>();
        UnityEngine.UI.Image primaryImage = EnsureRoundedImage(primaryTransform.gameObject);
        if (primaryButton == null || primaryImage == null)
        {
            Debug.LogError("El botón existente debe tener Button e Image.");
            return;
        }

        Transform primaryLabelTransform = primaryTransform.Find("Text (TMP)");
        TMP_Text primaryLabel = primaryLabelTransform != null
            ? primaryLabelTransform.GetComponent<TMP_Text>()
            : primaryTransform.GetComponentInChildren<TMP_Text>(true);
        if (primaryLabel == null)
        {
            primaryLabel = EnsureText(primaryTransform, "TextoBotonPrincipal", timer, font);
        }

        StyleButton(primaryButton, primaryImage, MainColor);
        SetBottomAnchored(primaryTransform as RectTransform, 0.08f, 0.92f, 107f, 97f);
        StyleButtonLabel(primaryLabel, font, new Color32(10, 11, 13, 255), 20f, FontStyles.Bold);
        SetFill(primaryLabel.rectTransform);

        UnityEngine.UI.Button resetButton = EnsureButton(canvasObject.transform, "BotonRestablecer", primaryImage, "Restablecer", font, SecondaryColor, TotalColor);
        UnityEngine.UI.Button lapButton = EnsureButton(canvasObject.transform, "BotonVuelta", primaryImage, "Vuelta", font, SecondaryColor, TotalColor);
        SetBottomAnchored(resetButton.transform as RectTransform, 0.08f, 0.485f, 24f, 70f);
        SetBottomAnchored(lapButton.transform as RectTransform, 0.515f, 0.92f, 24f, 70f);

        GameObject tableObject = EnsureChild(canvasObject.transform, "TablaVueltas");
        RectTransform tableRect = tableObject.transform as RectTransform;
        SetTopStretch(tableRect, 39f, 39f, 356f, 258f);
        UnityEngine.UI.ScrollRect scroll = EnsureComponent<UnityEngine.UI.ScrollRect>(tableObject);
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.scrollSensitivity = 28f;
        scroll.horizontalScrollbar = null;
        scroll.verticalScrollbar = null;

        GameObject viewportObject = EnsureChild(tableObject.transform, "Viewport");
        RectTransform viewportRect = viewportObject.transform as RectTransform;
        SetFill(viewportRect);
        UnityEngine.UI.Image viewportImage = EnsureImage(viewportObject, "Viewport");
        viewportImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        viewportImage.type = UnityEngine.UI.Image.Type.Sliced;
        viewportImage.color = Color.white;
        viewportImage.raycastTarget = true;
        UnityEngine.UI.Mask mask = EnsureComponent<UnityEngine.UI.Mask>(viewportObject);
        mask.showMaskGraphic = false;

        GameObject contentObject = EnsureChild(viewportObject.transform, "Content");
        RectTransform contentRect = contentObject.transform as RectTransform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        UnityEngine.UI.VerticalLayoutGroup vertical = EnsureComponent<UnityEngine.UI.VerticalLayoutGroup>(contentObject);
        vertical.padding = new RectOffset(0, 0, 0, 0);
        vertical.spacing = 0f;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        UnityEngine.UI.ContentSizeFitter fitter = EnsureComponent<UnityEngine.UI.ContentSizeFitter>(contentObject);
        fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        GameObject rowTemplate = EnsureLapRow(contentObject.transform, font, primaryImage.sprite);

        CronostasisStopwatch controller = canvasObject.GetComponent<CronostasisStopwatch>();
        if (controller == null)
            controller = Undo.AddComponent<CronostasisStopwatch>(canvasObject);
        Undo.RecordObject(controller, "Conectar cronómetro de Cronostasis");
        controller.Configure(
            timer,
            primaryButton,
            primaryLabel,
            primaryImage,
            resetButton,
            lapButton,
            scroll,
            contentRect,
            rowTemplate,
            primaryImage.sprite,
            font);
        EditorUtility.SetDirty(controller);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Interfaz de Cronostasis construida y SampleScene guardada.");
    }

    private static TMP_Text FindText(Transform parent, string path)
    {
        Transform child = parent.Find(path);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static GameObject EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject child = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(child, "Crear interfaz de Cronostasis");
        child.transform.SetParent(parent, false);
        child.layer = parent.gameObject.layer;
        return child;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(target);
    }

    private static UnityEngine.UI.Image EnsureRoundedImage(GameObject target)
    {
        UnityEngine.UI.Image existing = target.GetComponent<UnityEngine.UI.Image>();
        if (existing is CronostasisRoundedImage)
            return existing;

        Color oldColor = existing != null ? existing.color : Color.white;
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        CronostasisRoundedImage rounded = Undo.AddComponent<CronostasisRoundedImage>(target);
        rounded.sprite = null;
        rounded.color = oldColor;
        return rounded;
    }

    private static UnityEngine.UI.Image EnsureImage(GameObject target, string undoName)
    {
        UnityEngine.UI.Image image = target.GetComponent<UnityEngine.UI.Image>();
        if (image == null)
            image = Undo.AddComponent<UnityEngine.UI.Image>(target);
        return image;
    }

    private static TMP_Text EnsureText(Transform parent, string name, TMP_Text source, TMP_FontAsset font)
    {
        GameObject textObject = EnsureChild(parent, name);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (text == null)
            text = Undo.AddComponent<TextMeshProUGUI>(textObject);

        text.font = font != null ? font : (source != null ? source.font : TMP_Settings.defaultFontAsset);
        text.raycastTarget = false;
        return text;
    }

    private static UnityEngine.UI.Button EnsureButton(
        Transform parent,
        string name,
        UnityEngine.UI.Image sourceImage,
        string label,
        TMP_FontAsset font,
        Color background,
        Color textColor)
    {
        GameObject buttonObject = EnsureChild(parent, name);
        UnityEngine.UI.Image image = EnsureRoundedImage(buttonObject);
        UnityEngine.UI.Button button = EnsureComponent<UnityEngine.UI.Button>(buttonObject);
        image.sprite = sourceImage.sprite;
        image.type = sourceImage.type;
        StyleButton(button, image, background);
        TMP_Text labelText = EnsureText(buttonObject.transform, "Texto" + name, null, font);
        labelText.text = label;
        StyleButtonLabel(labelText, font, textColor, 16f, FontStyles.Bold);
        SetFill(labelText.rectTransform);
        return button;
    }

    private static GameObject EnsureLapRow(Transform content, TMP_FontAsset font, Sprite pillSprite)
    {
        GameObject row = EnsureChild(content, "FilaVueltaTemplate");
        UnityEngine.UI.LayoutElement layout = EnsureComponent<UnityEngine.UI.LayoutElement>(row);
        layout.preferredHeight = 52f;
        layout.minHeight = 52f;
        layout.flexibleHeight = 0f;

        TMP_Text number = EnsureText(row.transform, "LapNumber", null, font);
        number.text = "Vuelta 01";
        number.color = RowColor;
        number.fontSize = 14f;
        number.fontStyle = FontStyles.Normal;
        number.alignment = TextAlignmentOptions.MidlineLeft;
        number.textWrappingMode = TextWrappingModes.NoWrap;
        SetCell(number.rectTransform, 0f, 0.22f, 13f, 0f);

        TMP_Text split = EnsureText(row.transform, "LapSplit", null, font);
        split.text = "+00:00.00";
        split.color = RowColor;
        split.fontSize = 14f;
        split.fontStyle = FontStyles.Normal;
        split.alignment = TextAlignmentOptions.Center;
        split.textWrappingMode = TextWrappingModes.NoWrap;
        SetCell(split.rectTransform, 0.22f, 0.70f, 0f, 0f);

        TMP_Text total = EnsureText(row.transform, "LapTotal", null, font);
        total.text = "00:00.00";
        total.color = TotalColor;
        total.fontSize = 14f;
        total.fontStyle = FontStyles.Bold;
        total.alignment = TextAlignmentOptions.MidlineRight;
        total.textWrappingMode = TextWrappingModes.NoWrap;
        SetCell(total.rectTransform, 0.70f, 1f, 0f, 13f);

        GameObject badge = EnsureChild(row.transform, "FastestBadge");
        RectTransform badgeRect = badge.transform as RectTransform;
        badgeRect.anchorMin = new Vector2(0.22f, 0.5f);
        badgeRect.anchorMax = new Vector2(0.22f, 0.5f);
        badgeRect.pivot = new Vector2(0f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(3f, 0f);
        badgeRect.sizeDelta = new Vector2(45f, 19f);
        UnityEngine.UI.Image badgeImage = EnsureRoundedImage(badge);
        badgeImage.sprite = null;
        badgeImage.type = UnityEngine.UI.Image.Type.Sliced;
        badgeImage.color = BadgeColor;
        badgeImage.raycastTarget = false;

        TMP_Text badgeLabel = EnsureText(badge.transform, "Texto", null, font);
        badgeLabel.text = "Rápida";
        badgeLabel.color = FastColor;
        badgeLabel.fontSize = 10f;
        badgeLabel.alignment = TextAlignmentOptions.Center;
        badgeLabel.textWrappingMode = TextWrappingModes.NoWrap;
        SetFill(badgeLabel.rectTransform);
        badge.SetActive(false);

        GameObject separatorObject = EnsureChild(row.transform, "Separador");
        RectTransform separatorRect = separatorObject.transform as RectTransform;
        separatorRect.anchorMin = new Vector2(0f, 0f);
        separatorRect.anchorMax = new Vector2(1f, 0f);
        separatorRect.pivot = new Vector2(0.5f, 0f);
        separatorRect.anchoredPosition = Vector2.zero;
        separatorRect.sizeDelta = new Vector2(0f, 1f);
        UnityEngine.UI.Image separator = EnsureComponent<UnityEngine.UI.Image>(separatorObject);
        separator.color = DividerColor;
        separator.raycastTarget = false;

        row.SetActive(false);
        return row;
    }

    private static void StyleHeaderCell(
        TMP_Text text,
        TMP_FontAsset font,
        float minX,
        float maxX,
        Color color,
        TextAlignmentOptions alignment,
        float fontSize)
    {
        text.text = minX < 0.1f ? "VUELTA" : (maxX > 0.95f ? "TOTAL" : "PARCIAL");
        StyleText(text, font, fontSize, color, alignment, FontStyles.Normal);
        text.characterSpacing = 0.5f;
        if (minX >= 0.1f && maxX <= 0.70f)
            SetCell(text.rectTransform, minX, maxX, 0f, 0f);
    }

    private static void StyleText(
        TMP_Text text,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        FontStyles style)
    {
        if (font != null)
            text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.richText = true;
        text.margin = Vector4.zero;
    }

    private static void StyleButtonLabel(TMP_Text text, TMP_FontAsset font, Color color, float fontSize, FontStyles style)
    {
        StyleText(text, font, fontSize, color, TextAlignmentOptions.Center, style);
    }

    private static void StyleButton(
        UnityEngine.UI.Button button,
        UnityEngine.UI.Image image,
        Color color)
    {
        if (!(image is CronostasisRoundedImage) && image.sprite == null)
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = true;
        button.targetGraphic = image;
        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
        UnityEngine.UI.ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(245, 245, 245, 255);
        colors.pressedColor = new Color32(218, 218, 220, 255);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color32(130, 132, 138, 150);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        UnityEngine.UI.Navigation navigation = button.navigation;
        navigation.mode = UnityEngine.UI.Navigation.Mode.None;
        button.navigation = navigation;
    }

    private static void SetFill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetTopCentered(RectTransform rect, float top, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetTopStretch(RectTransform rect, float left, float right, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetBottomAnchored(RectTransform rect, float minX, float maxX, float bottom, float height)
    {
        rect.anchorMin = new Vector2(minX, 0f);
        rect.anchorMax = new Vector2(maxX, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, bottom);
        rect.sizeDelta = new Vector2(0f, height);
    }

    private static void SetCell(RectTransform rect, float minX, float maxX, float left, float right)
    {
        rect.anchorMin = new Vector2(minX, 0f);
        rect.anchorMax = new Vector2(maxX, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, 0f);
        rect.offsetMax = new Vector2(-right, 0f);
    }
}
#endif
