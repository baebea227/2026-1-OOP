using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine;
using UnityEngine.UI;

public class NetworkChatPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text messageLogText;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;

    [Header("Options")]
    [SerializeField] private bool buildDefaultUiWhenMissing = true;
    [SerializeField] private int maxVisibleMessages = 30;
    [SerializeField] private string senderNameOverride;

    private const string KoreanFontProbe = "\uC548\uB155\uD558\uC138\uC694\uAC00\uB098\uB2E4\uB77C\uB9C8\uBC14\uC0AC\uC544\uC790\uCC28\uCE74\uD0C0\uD30C\uD558";
    private static TMP_FontAsset runtimeKoreanFontAsset;
    private static bool triedResolveRuntimeKoreanFont;

    private readonly List<string> messageLines = new List<string>();
    private NetworkChatService chatService;

    private void Awake()
    {
        if (buildDefaultUiWhenMissing)
            BuildDefaultUiIfNeeded();

        ConfigureInputField();
        ApplyRuntimeFontToExistingUi();
    }

    private void OnEnable()
    {
        chatService = NetworkChatService.EnsureInstance();

        chatService.OnMessageReceived += AppendMessage;

        if (sendButton != null)
            sendButton.onClick.AddListener(SendCurrentInput);

        if (inputField != null)
            inputField.onSubmit.AddListener(SendFromSubmit);
    }

    private void OnDisable()
    {
        if (chatService != null)
        {
            chatService.OnMessageReceived -= AppendMessage;
        }

        if (sendButton != null)
            sendButton.onClick.RemoveListener(SendCurrentInput);

        if (inputField != null)
            inputField.onSubmit.RemoveListener(SendFromSubmit);
    }

    public void SendCurrentInput()
    {
        if (inputField == null)
            return;

        string message = inputField.text;

        if (chatService == null)
            chatService = NetworkChatService.EnsureInstance();

        if (!chatService.SendChat(message, senderNameOverride))
            return;

        inputField.text = "";
        inputField.ActivateInputField();
    }

    public void ClearMessages()
    {
        messageLines.Clear();
        RefreshMessageLog();
    }

    private void SendFromSubmit(string _)
    {
        SendCurrentInput();
    }

    private void AppendMessage(NetworkChatMessage message)
    {
        if (message.IsSystem)
        {
            AppendSystemLine(message.Text);
            return;
        }

        string senderName = string.IsNullOrWhiteSpace(message.SenderName) ? "Player" : message.SenderName;
        AppendLine(senderName + ": " + message.Text);
    }

    private void AppendSystemLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        AppendLine("[System] " + text);
    }

    private void AppendLine(string line)
    {
        messageLines.Add(line);

        while (messageLines.Count > maxVisibleMessages)
            messageLines.RemoveAt(0);

        RefreshMessageLog();
    }

    private void RefreshMessageLog()
    {
        if (messageLogText == null)
            return;

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < messageLines.Count; i++)
        {
            if (i > 0)
                builder.AppendLine();

            builder.Append(messageLines[i]);
        }

        messageLogText.text = builder.ToString();
    }

    private void ConfigureInputField()
    {
        if (inputField == null)
            return;

        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterLimit = 160;
    }

    private void BuildDefaultUiIfNeeded()
    {
        if (messageLogText != null && inputField != null && sendButton != null)
            return;

        Transform uiParent = ResolveUiParent();

        GameObject panelObject = new GameObject("ChatPanel", typeof(RectTransform), typeof(Image));
        panelObject.layer = uiParent.gameObject.layer;
        panelObject.transform.SetParent(uiParent, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(20f, 20f);
        panelRect.sizeDelta = new Vector2(420f, 260f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.45f);

        if (messageLogText == null)
            messageLogText = CreateMessageLog(panelRect);

        if (inputField == null)
            inputField = CreateInputField(panelRect);

        if (sendButton == null)
            sendButton = CreateSendButton(panelRect);
    }

    private Transform ResolveUiParent()
    {
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas != null)
        {
            EnsureEventSystem();
            return transform;
        }

        GameObject canvasObject = new GameObject("ChatCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = gameObject.layer;
        canvasObject.transform.SetParent(transform, false);

        Canvas createdCanvas = canvasObject.GetComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        EnsureEventSystem();
        return canvasObject.transform;
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private TMP_Text CreateMessageLog(RectTransform parent)
    {
        GameObject logObject = new GameObject("MessageLog", typeof(RectTransform), typeof(TextMeshProUGUI));
        logObject.layer = parent.gameObject.layer;
        logObject.transform.SetParent(parent, false);

        RectTransform logRect = logObject.GetComponent<RectTransform>();
        logRect.anchorMin = new Vector2(0f, 0f);
        logRect.anchorMax = new Vector2(1f, 1f);
        logRect.offsetMin = new Vector2(12f, 54f);
        logRect.offsetMax = new Vector2(-12f, -12f);

        TextMeshProUGUI text = logObject.GetComponent<TextMeshProUGUI>();
        text.text = "";
        text.fontSize = 18f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.BottomLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        ApplyRuntimeFont(text);

        return text;
    }

    private TMP_InputField CreateInputField(RectTransform parent)
    {
        GameObject inputObject = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObject.layer = parent.gameObject.layer;
        inputObject.transform.SetParent(parent, false);

        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 0f);
        inputRect.pivot = new Vector2(0.5f, 0f);
        inputRect.offsetMin = new Vector2(12f, 12f);
        inputRect.offsetMax = new Vector2(-92f, 44f);

        Image inputImage = inputObject.GetComponent<Image>();
        inputImage.color = new Color(1f, 1f, 1f, 0.9f);

        TextMeshProUGUI text = CreateInputText(inputObject.transform, "Text", Color.black, "");
        TextMeshProUGUI placeholder = CreateInputText(inputObject.transform, "Placeholder", new Color(0.35f, 0.35f, 0.35f, 1f), "Type message...");

        TMP_InputField field = inputObject.GetComponent<TMP_InputField>();
        field.textComponent = text;
        field.placeholder = placeholder;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = 160;

        return field;
    }

    private TextMeshProUGUI CreateInputText(Transform parent, string objectName, Color color, string textValue)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 5f);
        rect.offsetMax = new Vector2(-8f, -5f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = textValue;
        text.fontSize = 18f;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        ApplyRuntimeFont(text);

        return text;
    }

    private Button CreateSendButton(RectTransform parent)
    {
        GameObject buttonObject = new GameObject("SendButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.layer = parent.gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.anchoredPosition = new Vector2(-12f, 12f);
        buttonRect.sizeDelta = new Vector2(72f, 32f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.42f, 0.86f, 1f);

        TextMeshProUGUI label = CreateInputText(buttonObject.transform, "Label", Color.white, "Send");
        label.alignment = TextAlignmentOptions.Center;

        return buttonObject.GetComponent<Button>();
    }

    private void ApplyRuntimeFontToExistingUi()
    {
        ApplyRuntimeFont(messageLogText);

        if (inputField != null)
        {
            ApplyRuntimeFont(inputField.textComponent);
            ApplyRuntimeFont(inputField.placeholder as TMP_Text);
        }

        if (sendButton != null)
        {
            TMP_Text buttonText = sendButton.GetComponentInChildren<TMP_Text>(true);
            ApplyRuntimeFont(buttonText);
        }
    }

    private static void ApplyRuntimeFont(TMP_Text text)
    {
        if (text == null)
            return;

        TMP_FontAsset fontAsset = ResolveRuntimeKoreanFontAsset();

        if (fontAsset != null)
            text.font = fontAsset;
    }

    private static TMP_FontAsset ResolveRuntimeKoreanFontAsset()
    {
        if (runtimeKoreanFontAsset != null)
            return runtimeKoreanFontAsset;

        if (triedResolveRuntimeKoreanFont)
            return TMP_Settings.defaultFontAsset;

        triedResolveRuntimeKoreanFont = true;

        string[] fontFamilies =
        {
            "Malgun Gothic",
            "Noto Sans CJK KR",
            "Noto Sans KR",
            "Arial Unicode MS",
            "Apple SD Gothic Neo"
        };

        foreach (string family in fontFamilies)
        {
            try
            {
                TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(family, "Regular", 90);

                if (fontAsset == null)
                    continue;

                fontAsset.name = family + " Chat SDF";

                if (fontAsset.TryAddCharacters(KoreanFontProbe, out string missingCharacters) &&
                    string.IsNullOrEmpty(missingCharacters))
                {
                    runtimeKoreanFontAsset = fontAsset;
                    return runtimeKoreanFontAsset;
                }
            }
            catch
            {
            }
        }

        return TMP_Settings.defaultFontAsset;
    }
}
