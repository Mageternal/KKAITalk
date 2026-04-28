using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;
using Image = UnityEngine.UI.Image;
using Text = UnityEngine.UI.Text;

namespace KKAITalk.UI
{
    public class AIDialogueUI : MonoBehaviour
    {
        public static AIDialogueUI Instance { get; private set; }

        private Canvas _canvas;
        private GameObject _panel;
        private Image _panelBg;
        private Text _nameText;
        private Text _dialogueText;
        private Coroutine _typingCoroutine;
        private GameObject _inputPanel;
        private UnityEngine.UI.InputField _inputField;

        // 打字速度，每个字符间隔（秒）
        private const float TypeSpeed = 0.04f;

        public void ShowInputMode(string charaName = null)
        {
            _panel.SetActive(true);
            _inputPanel.SetActive(true);
            if (charaName != null)
            {
                RefreshFont();
                _nameText.text = charaName;
                if (string.IsNullOrEmpty(_dialogueText.text))
                    _dialogueText.text = "……";
            }
        }

        public void HideInputMode()
        {
            _inputPanel.SetActive(false);
            // 对话框保留显示最后回复，不隐藏
        }
        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
            Hide();
        }

        private void BuildUI()
        {
            // ── Canvas ──────────────────────────────────────────
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // ── 底部主面板（对话框）───────────────────────────────────
            // anchorMin/Max = (0,0)到(1,0) 表示：水平拉伸铺满，垂直锚定在底部
            // offsetMin = 左边距, 底边距（从锚点往内缩）
            // offsetMax = 右边距, 顶边距（从锚点往外扩，正值=向上）
            // 最终效果：左边留80px(给返回按钮)，右边留10px，底部在55+45=100px处，高度150px
            _panel = new GameObject("DialoguePanel");
            _panel.transform.SetParent(_canvas.transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);  // 锚点左下
            panelRect.anchorMax = new Vector2(1f, 0f);  // 锚点右下（水平拉伸）
            panelRect.pivot = new Vector2(0.5f, 0f);    // 轴心在底部中间
            panelRect.offsetMin = new Vector2(80f, 0f);  // 左边80px，底部0px
            panelRect.offsetMax = new Vector2(-10f, 200f); 
                                                           // 注意：不要设置sizeDelta，offsetMin+offsetMax已经完整定义了大小

            _panelBg = _panel.AddComponent<Image>();
            _panelBg.color = new Color(0.05f, 0.05f, 0.1f, 0.88f);

            // ── 顶部装饰线 ───────────────────────────────────────
            var lineObj = new GameObject("TopLine");
            lineObj.transform.SetParent(_panel.transform, false);
            var lineRect = lineObj.AddComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0f, 1f);
            lineRect.anchorMax = new Vector2(1f, 1f);
            lineRect.pivot = new Vector2(0.5f, 1f);
            lineRect.sizeDelta = new Vector2(0f, 2f);
            lineRect.anchoredPosition = Vector2.zero;
            var lineImg = lineObj.AddComponent<Image>();
            lineImg.color = new Color(0.8f, 0.7f, 1f, 0.6f); // 淡紫色线条

            // ── 角色名字背景小标签 ───────────────────────────────
            var nameBg = new GameObject("NameBg");
            nameBg.transform.SetParent(_panel.transform, false);
            var nameBgRect = nameBg.AddComponent<RectTransform>();
            nameBgRect.anchorMin = new Vector2(0f, 1f);
            nameBgRect.anchorMax = new Vector2(0f, 1f);
            nameBgRect.pivot = new Vector2(0f, 0f);
            nameBgRect.anchoredPosition = new Vector2(30f, 0f);
            nameBgRect.sizeDelta = new Vector2(140f, 34f);
            var nameBgImg = nameBg.AddComponent<Image>();
            nameBgImg.color = new Color(0.3f, 0.1f, 0.5f, 0.9f); // 深紫

            // ── 角色名字 Text ────────────────────────────────────
            var nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(nameBg.transform, false);
            var nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(8f, 2f);
            nameRect.offsetMax = new Vector2(-8f, -2f);

            _nameText = nameObj.AddComponent<Text>();
            _nameText.fontSize = 20;
            _nameText.fontStyle = FontStyle.Bold;
            _nameText.color = new Color(1f, 0.85f, 1f);
            _nameText.alignment = TextAnchor.MiddleCenter;
            _nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // ── 对话内容 Text ────────────────────────────────────
            var dialogueObj = new GameObject("DialogueText");
            dialogueObj.transform.SetParent(_panel.transform, false);
            var dialogueRect = dialogueObj.AddComponent<RectTransform>();
            dialogueRect.anchorMin = Vector2.zero;
            dialogueRect.anchorMax = Vector2.one;
            dialogueRect.offsetMin = new Vector2(30f, 12f);
            dialogueRect.offsetMax = new Vector2(-30f, -44f);

            _dialogueText = dialogueObj.AddComponent<Text>();
            _dialogueText.fontSize = 22;
            _dialogueText.color = Color.white;
            _dialogueText.alignment = TextAnchor.UpperLeft;
            _dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _dialogueText.verticalOverflow = VerticalWrapMode.Overflow;
            _dialogueText.lineSpacing = 1.3f;
            _dialogueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // ── 输入框面板 ─────────────────────────────────────
            _inputPanel = new GameObject("InputPanel");
            _inputPanel.transform.SetParent(_canvas.transform, false);

            var inputPanelRect = _inputPanel.AddComponent<RectTransform>();
            inputPanelRect.anchorMin = new Vector2(0f, 0f);
            inputPanelRect.anchorMax = new Vector2(1f, 0f);
            inputPanelRect.pivot = new Vector2(0.5f, 0f);
            // 输入框面板
            inputPanelRect.offsetMin = new Vector2(80f, 0f);
            inputPanelRect.offsetMax = new Vector2(-10f, 50f);

            var inputPanelBg = _inputPanel.AddComponent<Image>();
            inputPanelBg.color = new Color(0.05f, 0.05f, 0.1f, 0.88f);

            // 输入框
            var inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(_inputPanel.transform, false);
            var inputRect = inputObj.AddComponent<RectTransform>();
            inputRect.anchorMin = Vector2.zero;
            inputRect.anchorMax = Vector2.one;
            inputRect.offsetMin = new Vector2(5f, 5f);
            inputRect.offsetMax = new Vector2(-90f, -5f); // 右边留给发送按钮

            var inputBg = inputObj.AddComponent<Image>();
            inputBg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

            _inputField = inputObj.AddComponent<UnityEngine.UI.InputField>();
            _inputField.lineType = UnityEngine.UI.InputField.LineType.SingleLine;
            _inputField.onEndEdit.AddListener((text) =>
            {
                if (!string.IsNullOrEmpty(text.Trim()))
                    OnSendClicked();
            });
            _inputField.transition = Selectable.Transition.None;

            // InputField需要Text子对象
            var inputTextObj = new GameObject("Text");
            inputTextObj.transform.SetParent(inputObj.transform, false);
            var inputTextRect = inputTextObj.AddComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = new Vector2(5f, 2f);
            inputTextRect.offsetMax = new Vector2(-5f, -2f);

            var inputText = inputTextObj.AddComponent<Text>();
            inputText.fontSize = 18;
            inputText.color = Color.white;
            inputText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _inputField.textComponent = inputText;

            // 发送按钮
            var sendBtn = new GameObject("SendButton");
            sendBtn.transform.SetParent(_inputPanel.transform, false);
            var sendRect = sendBtn.AddComponent<RectTransform>();
            sendRect.anchorMin = new Vector2(0.83f, 0f);
            sendRect.anchorMax = new Vector2(1f, 1f);
            sendRect.offsetMin = new Vector2(5f, 5f);
            sendRect.offsetMax = new Vector2(-10f, -5f);

            var sendBtnBg = sendBtn.AddComponent<Image>();
            sendBtnBg.color = new Color(0.3f, 0.1f, 0.5f, 0.9f);

            var sendBtnComp = sendBtn.AddComponent<UnityEngine.UI.Button>();
            sendBtnComp.targetGraphic = sendBtnBg;

            var sendTextObj = new GameObject("Text");
            sendTextObj.transform.SetParent(sendBtn.transform, false);
            var sendTextRect = sendTextObj.AddComponent<RectTransform>();
            sendTextRect.anchorMin = Vector2.zero;
            sendTextRect.anchorMax = Vector2.one;
            var sendText = sendTextObj.AddComponent<Text>();
            sendText.text = "发送";
            sendText.fontSize = 16;
            sendText.color = Color.white;
            sendText.alignment = TextAnchor.MiddleCenter;
            sendText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 发送按钮事件
            sendBtnComp.onClick.AddListener(() => OnSendClicked());

            _inputPanel.SetActive(false); // 默认隐藏
        }
        public System.Action<string> OnUserInput; // 外部订阅

        private void OnSendClicked()
        {
            string text = _inputField.text.Trim();
            AITalkPlugin.Log.LogInfo($"OnSendClicked触发, text='{text}'");
            AITalkPlugin.Log.LogInfo($"OnUserInput是否为null: {OnUserInput == null}");
            if (string.IsNullOrEmpty(text)) return;

            _inputField.text = "";
            OnUserInput?.Invoke(text);
            AITalkPlugin.Log.LogInfo("OnUserInput已调用");
        }

        // Talk场景加载完成后，用游戏已有字体替换Arial
        public void RefreshFont()
        {
            var gameFont = FindGameFont();
            if (gameFont == null) return;
            _nameText.font = gameFont;
            _dialogueText.font = gameFont;
        }

        private Font FindGameFont()
        {
            var allTexts = Resources.FindObjectsOfTypeAll<Text>();
            foreach (var t in allTexts)
            {
                if (t.font != null && t.font.name != "Arial")
                    return t.font;
            }
            return null;
        }

        // ── 公开接口 ─────────────────────────────────────────────

        public void ShowWaiting(string charaName)
        {
            RefreshFont(); // 此时场景已加载，字体可以拿到
            _panel.SetActive(true);
            _nameText.text = charaName;
            _dialogueText.text = "";
            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
            _typingCoroutine = StartCoroutine(WaitingDots());
        }

        public void ShowReply(string reply)
        {
            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
            _typingCoroutine = StartCoroutine(TypeText(reply));
        }

        public void Hide()
        {
            if (_typingCoroutine != null)
            {
                StopCoroutine(_typingCoroutine);
                _typingCoroutine = null;
            }
            if (_panel != null) _panel.SetActive(false);
            if (_inputPanel != null) _inputPanel.SetActive(false); // 加这行
        }
        public void SetPanelHeight(float height)
        {
            var panelRect = _panel.GetComponent<RectTransform>();
            panelRect.offsetMax = new Vector2(-10f, height);
        }
        // ── 协程 ─────────────────────────────────────────────────

        // 等待时显示滚动省略号动画
        private IEnumerator WaitingDots()
        {
            string[] frames = { ".", "..", "..." };
            int i = 0;
            while (true)
            {
                _dialogueText.text = frames[i % 3];
                i++;
                yield return new WaitForSeconds(0.4f);
            }
        }


        // 打字机逐字显示
        private IEnumerator TypeText(string text)
        {
            _dialogueText.text = "";
            foreach (char c in text)
            {
                _dialogueText.text += c;
                // 标点后稍作停顿，更自然
                float delay = (c == '。' || c == '，' || c == '！' || c == '？')
                    ? TypeSpeed * 4f
                    : TypeSpeed;
                yield return new WaitForSeconds(delay);
            }
        }


    }
}