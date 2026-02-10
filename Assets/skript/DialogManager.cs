using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DialogManager : MonoBehaviour
{
    public static DialogManager Instance;
    private void Awake() => Instance = this;

    [SerializeField] public Image backgroundImage;
    [SerializeField] public Image leftImage;
    [SerializeField] public Image rightImage;
    [SerializeField] public Image middleImage;
    [SerializeField] public Image fullViewImage;

    [SerializeField] private Dialog currentDialog;
    private int currentLineIndex;
    private bool isTalking;
    private Coroutine talkingRoutine;

    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private TextMeshProUGUI dialogText;

    [SerializeField] private GameObject optionButton1;
    [SerializeField] private GameObject optionButton2;
    [SerializeField] private TextMeshProUGUI optionButton1Text;
    [SerializeField] private TextMeshProUGUI optionButton2Text;

    [SerializeField] private GameObject buttonC;
    [SerializeField] private GameObject buttonD;
    [SerializeField] private TextMeshProUGUI buttonCText;
    [SerializeField] private TextMeshProUGUI buttonDText;

    private bool option1;
    private bool option2;
    private bool option3;
    private bool option4;
    private bool waitingForDecision;

    [Header("Scene Transition")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 1f;

    [Header("Typing")]
    [SerializeField] private float typingSpeed = 0.035f;

    private bool skipTypingRequest;
    private bool lineFullyVisible;

    private enum TransitionState { None, WaitingForClickFadingIn, Fading, WaitingForClickPostFade }
    private TransitionState transitionState = TransitionState.None;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SetImageColorActive(leftImage, false);
        SetImageColorActive(rightImage, false);
        SetImageColorActive(middleImage, false);
        SetImageColorActive(fullViewImage, false);
        ChangeImageHolderState(leftImage, false);
        ChangeImageHolderState(rightImage, false);
        ChangeImageHolderState(middleImage, false);
        ChangeImageHolderState(fullViewImage, false);
        dialogPanel.SetActive(false);

        // hide all option buttons initially
        ChangeOptionButtonsState(false);

        if (fadeImage)
        {
            fadeImage.color = new Color(0, 0, 0, 0);
            fadeImage.raycastTarget = false;
        }

        // ensure Button components are present and not interactable at start
        SetButtonInteractable(optionButton1, false);
        SetButtonInteractable(optionButton2, false);
        SetButtonInteractable(buttonC, false);
        SetButtonInteractable(buttonD, false);

        // Wire up OnClick listeners so clicks always call ChooseOption(...)
        SetupOptionButtons();

        skipTypingRequest = false;
        lineFullyVisible = false;
    }

    private void SetupOptionButtons()
    {
        SetupButton(optionButton1, 1);
        SetupButton(optionButton2, 2);
        SetupButton(buttonC, 3);
        SetupButton(buttonD, 4);
    }

    private void SetupButton(GameObject go, int id)
    {
        if (go == null) return;
        var btn = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>();
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => ChooseOption(id));
    }

    private void SetButtonInteractable(GameObject go, bool state)
    {
        if (go == null) return;
        var b = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>();
        if (b != null) b.interactable = state;
    }

    // Update is called once per frame
    void Update()
    {
        if (waitingForDecision) return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            // 1. Highest priority = transition
            if (transitionState != TransitionState.None)
            {
                HandleTransitionClick();
                return;
            }

            // 2. If current dialog is background-only -> consume click as "advance"
            if (currentDialog != null && currentDialog.backgroundOnly)
            {
                currentLineIndex++;
                if (currentDialog.lines == null || currentLineIndex >= currentDialog.lines.Length)
                {
                    FinishSayingLogic();
                }
                // always consume click when in backgroundOnly mode
                return;
            }

            // 3. If still typing -> skip to end of line
            if (isTalking)
            {
                skipTypingRequest = true;
                return;
            }

            // 4. If the current line is fully visible -> advance
            if (lineFullyVisible)
            {
                lineFullyVisible = false;
                currentLineIndex++;

                if (!currentDialog)
                    return;

                if (currentLineIndex >= currentDialog.lines.Length)
                {
                    FinishSayingLogic();

                    if (transitionState == TransitionState.WaitingForClickFadingIn)
                    {
                        HandleTransitionClick();
                    }
                }
                else
                {
                    ManageSpeechLogic();
                }
                return;
            }

            // 5. Otherwise start typing (first click)
            ManageSpeechLogic();
        }
    }

    private void ManageSpeechLogic()
    {
        if (!currentDialog) return;

        // If dialog wants background-only presentation, hide UI and treat line as instantly visible
        if (currentDialog.backgroundOnly)
        {
            // hide all UI elements except background
            dialogPanel.SetActive(false);
            ChangeOptionButtonsState(false);
            ChangeImageHolderState(leftImage, false);
            ChangeImageHolderState(rightImage, false);
            ChangeImageHolderState(middleImage, false);
            ChangeImageHolderState(fullViewImage, false);
            UpdateBackground();

            // make the line considered visible so clicks advance
            skipTypingRequest = false;
            lineFullyVisible = true;
            isTalking = false;
            return;
        }

        // If index out of bounds, finish logic (handles decisions / transitions)
        if (currentLineIndex < 0) currentLineIndex = 0;
        if (currentLineIndex >= currentDialog.lines.Length)
        {
            FinishSayingLogic();
            return;
        }

        ManageAlignments();
        UpdateBackground();

        // Stop previous typing coroutine if any
        if (talkingRoutine != null && isTalking)
            StopCoroutine(talkingRoutine);

        // Reset skip request before starting new typing
        skipTypingRequest = false;
        lineFullyVisible = false;

        talkingRoutine = StartCoroutine(Say(currentDialog.lines[currentLineIndex], currentDialog.character != null ? currentDialog.character.characterName : ""));
    }

    private void ManageAlignments()
    {
        // If backgroundOnly, hide characters and textbox (no dimming)
        if (currentDialog != null && currentDialog.backgroundOnly)
        {
            ChangeImageHolderState(leftImage, false);
            ChangeImageHolderState(rightImage, false);
            ChangeImageHolderState(middleImage, false);
            ChangeImageHolderState(fullViewImage, false);
            dialogPanel.SetActive(false);
            ChangeOptionButtonsState(false);
            return;
        }

        // Additional characters (left/right/middle/fullView) use their default expression (fullView uses middle/additionalMiddle by default)
        ChangeImageHolderState(leftImage, currentDialog.additionalCharacterLeft);
        ChangeImageHolderState(rightImage, currentDialog.additionalCharacterRight);
        ChangeImageHolderState(middleImage, currentDialog.additionalCharacterMiddle);
        // fullViewImage currently has no separate additionalCharacter in Dialog; keep it inactive by default
        ChangeImageHolderState(fullViewImage, false);

        // Main character expression (from currentDialog.expression)
        Sprite currentSprite = null;
        if (currentDialog?.character != null)
            currentSprite = currentDialog.character.GetSprite(currentDialog.expression);

        switch (currentDialog.position)
        {
            case Dialog.Position.Left:
                if (currentSprite != null) leftImage.sprite = currentSprite;
                SetImageColorActive(rightImage, false);
                SetImageColorActive(leftImage, true);
                SetImageColorActive(middleImage, false);
                SetImageColorActive(fullViewImage, false);
                leftImage.gameObject.SetActive(true);
                rightImage.gameObject.SetActive(false);
                middleImage.gameObject.SetActive(false);
                fullViewImage?.gameObject.SetActive(false);
                break;
            case Dialog.Position.Right:
                if (currentSprite != null) rightImage.sprite = currentSprite;
                SetImageColorActive(rightImage, true);
                SetImageColorActive(leftImage, false);
                SetImageColorActive(middleImage, false);
                SetImageColorActive(fullViewImage, false);
                rightImage.gameObject.SetActive(true);
                leftImage.gameObject.SetActive(false);
                middleImage.gameObject.SetActive(false);
                fullViewImage?.gameObject.SetActive(false);
                break;
            case Dialog.Position.Middle:
                if (currentSprite != null) middleImage.sprite = currentSprite;
                SetImageColorActive(rightImage, false);
                SetImageColorActive(leftImage, false);
                SetImageColorActive(middleImage, true);
                SetImageColorActive(fullViewImage, false);
                middleImage.gameObject.SetActive(true);
                leftImage.gameObject.SetActive(false);
                rightImage.gameObject.SetActive(false);
                fullViewImage?.gameObject.SetActive(false);
                break;
            case Dialog.Position.FullView:
                // FullView behaves like Middle by default (character centered, sides hidden)
                if (currentSprite != null && fullViewImage != null) fullViewImage.sprite = currentSprite;
                SetImageColorActive(rightImage, false);
                SetImageColorActive(leftImage, false);
                SetImageColorActive(middleImage, false);
                SetImageColorActive(fullViewImage, true);
                leftImage.gameObject.SetActive(false);
                rightImage.gameObject.SetActive(false);
                middleImage.gameObject.SetActive(false);
                if (fullViewImage != null) fullViewImage.gameObject.SetActive(true);
                break;
        }
    }

    private IEnumerator Say(string text, string charName)
    {
        dialogPanel.SetActive(true);
        isTalking = true;
        lineFullyVisible = false;

        dialogText.text = charName + ":\n";

        for (int i = 0; i < text.Length; i++)
        {
            if (skipTypingRequest)
            {
                // show whole text immediately
                dialogText.text = charName + ":\n" + text;
                break;
            }

            dialogText.text += text[i];
            yield return new WaitForSeconds(typingSpeed);
        }

        // Ensure full text is visible
        if (!dialogText.text.EndsWith(text))
            dialogText.text = charName + ":\n" + text;

        isTalking = false;
        lineFullyVisible = true;
        skipTypingRequest = false;
    }

    private void FinishSayingLogic()
    {
        if (currentDialog == null) return;

        if (currentDialog.requirePlayerDecision && currentLineIndex >= currentDialog.lines.Length)
        {
            StartCoroutine(ManagePlayerDecision());
            return;
        }

        if (currentLineIndex >= currentDialog.lines.Length)
        {
            if (currentDialog.triggerSceneTransition)
            {
                transitionState = TransitionState.WaitingForClickFadingIn;
                return;
            }

            // Advance to next dialog and immediately update UI to reflect it.
            currentLineIndex = 0;
            currentDialog = currentDialog.nextDialog;

            // If no next dialog, hide UI and return.
            if (currentDialog == null)
            {
                dialogPanel.SetActive(false);
                ChangeOptionButtonsState(false);
                ChangeImageHolderState(leftImage, false);
                ChangeImageHolderState(rightImage, false);
                ChangeImageHolderState(middleImage, false);
                if (fullViewImage != null) ChangeImageHolderState(fullViewImage, false);
                return;
            }

            // Update visuals for the new dialog right away.
            UpdateBackground();
            ManageAlignments();

            // Reset typing state so ManageSpeechLogic behaves consistently.
            skipTypingRequest = false;
            lineFullyVisible = false;
            isTalking = false;

            // If the new dialog is background-only, ensure UI is hidden.
            if (currentDialog.backgroundOnly)
            {
                dialogPanel.SetActive(false);
                ChangeOptionButtonsState(false);
                return;
            }

            // Otherwise start the new dialog's speech logic immediately.
            ManageSpeechLogic();
        }
    }

    private IEnumerator ManagePlayerDecision()
    {
        waitingForDecision = true;

        // Decide which option pair to show based on Love gate
        bool useGate = currentDialog != null && currentDialog.useLoveThreshold;
        bool gatePassed = useGate && GameVariables.Love >= currentDialog.loveThreshold;

        if (gatePassed && (!string.IsNullOrEmpty(currentDialog.option3Text) || !string.IsNullOrEmpty(currentDialog.option4Text)))
        {
            // show alternate buttons 3 & 4
            optionButton1?.SetActive(false);
            optionButton2?.SetActive(false);

            if (buttonC != null)
            {
                buttonC.SetActive(true);
                buttonCText.text = string.IsNullOrEmpty(currentDialog.option3Text) ? currentDialog.option1Text : currentDialog.option3Text;
                SetButtonInteractable(buttonC, true);
            }
            if (buttonD != null)
            {
                buttonD.SetActive(true);
                buttonDText.text = string.IsNullOrEmpty(currentDialog.option4Text) ? currentDialog.option2Text : currentDialog.option4Text;
                SetButtonInteractable(buttonD, true);
            }
        }
        else
        {
            // show primary buttons 1 & 2
            buttonC?.SetActive(false);
            buttonD?.SetActive(false);

            if (optionButton1 != null)
            {
                optionButton1.SetActive(true);
                optionButton1Text.text = currentDialog.option1Text;
                SetButtonInteractable(optionButton1, true);
            }
            if (optionButton2 != null)
            {
                optionButton2.SetActive(true);
                optionButton2Text.text = currentDialog.option2Text;
                SetButtonInteractable(optionButton2, true);
            }
        }

        // wait for any of the possible choices (1..4)
        yield return new WaitUntil(() => option1 || option2 || option3 || option4);

        // handle chosen option; prefer alternate dialogs/amounts if gate passed
        if (option1)
        {
            if (gatePassed && currentDialog.option3NextDialog != null)
            {
                ApplyLucyChange(currentDialog.option3AddsLove, currentDialog.option3LoveAmount);
                currentDialog = currentDialog.option3NextDialog;
            }
            else
            {
                ApplyLucyChange(currentDialog.option1AddsLove, currentDialog.option1LoveAmount);
                currentDialog = currentDialog.option1NextDialog;
            }
            option1 = false;
        }

        if (option2)
        {
            if (gatePassed && currentDialog.option4NextDialog != null)
            {
                ApplyLucyChange(currentDialog.option4AddsLove, currentDialog.option4LoveAmount);
                currentDialog = currentDialog.option4NextDialog;
            }
            else
            {
                ApplyLucyChange(currentDialog.option2AddsLove, currentDialog.option2LoveAmount);
                currentDialog = currentDialog.option2NextDialog;
            }
            option2 = false;
        }

        if (option3)
        {
            // choosing option3 (alternate left) maps to option3NextDialog
            ApplyLucyChange(currentDialog.option3AddsLove, currentDialog.option3LoveAmount);
            currentDialog = currentDialog.option3NextDialog ?? currentDialog.option1NextDialog;
            option3 = false;
        }

        if (option4)
        {
            // choosing option4 (alternate right) maps to option4NextDialog
            ApplyLucyChange(currentDialog.option4AddsLove, currentDialog.option4LoveAmount);
            currentDialog = currentDialog.option4NextDialog ?? currentDialog.option2NextDialog;
            option4 = false;
        }

        currentLineIndex = 0;
        waitingForDecision = false;

        // hide all option buttons and stop interactability
        ChangeOptionButtonsState(false);
        ManageSpeechLogic();
    }

    private void ApplyLucyChange(bool flag, int amount)
    {
        if (flag || amount != 0)
        {
            int a = amount != 0 ? amount : 1;
            GameVariables.AddLove(a);
        }
    }

    private void HandleTransitionClick()
    {
        Debug.Log("Klik v transition stavu: " + transitionState);
        switch (transitionState)
        {
            case TransitionState.WaitingForClickFadingIn:
                // ← TADY CHYBĚL TEN CASE (nebo byl špatně)
                transitionState = TransitionState.Fading;
                StartCoroutine(SceneTransitionRoutine());   // ← tady se spustí celý fade
                break;

            case TransitionState.WaitingForClickPostFade:
                transitionState = TransitionState.None;
                ManageSpeechLogic();   // spustí první řádek nového dialogu
                break;
        }
    }

    private IEnumerator SceneTransitionRoutine()
    {
        Debug.Log("SceneTransitionRoutine START");
        if (fadeImage == null)
        {
            Debug.LogWarning("FadeImage chybí!");
            yield break;
        }

        float elementsFadeDuration = 0.4f;

        CanvasGroup dialogGroup = dialogPanel.GetComponent<CanvasGroup>();
        if (dialogGroup == null) dialogGroup = dialogPanel.AddComponent<CanvasGroup>();
        dialogGroup.blocksRaycasts = true;

        float fadeOutElapsed = 0f;
        while (fadeOutElapsed < elementsFadeDuration)
        {
            fadeOutElapsed += Time.deltaTime;
            float t = fadeOutElapsed / elementsFadeDuration;

            if (leftImage != null && leftImage.gameObject.activeSelf) leftImage.color = new Color(1, 1, 1, Mathf.Lerp(1f, 0f, t));
            if (rightImage != null && rightImage.gameObject.activeSelf) rightImage.color = new Color(1, 1, 1, Mathf.Lerp(1f, 0f, t));
            if (middleImage != null && middleImage.gameObject.activeSelf) middleImage.color = new Color(1, 1, 1, Mathf.Lerp(1f, 0f, t));
            if (fullViewImage != null && fullViewImage.gameObject.activeSelf) fullViewImage.color = new Color(1, 1, 1, Mathf.Lerp(1f, 0f, t));

            dialogGroup.alpha = Mathf.Lerp(1f, 0f, t);

            yield return null;
        }

        if (leftImage != null && leftImage.gameObject.activeSelf) leftImage.color = new Color(1, 1, 1, 0f);
        if (rightImage != null && rightImage.gameObject.activeSelf) rightImage.color = new Color(1, 1, 1, 0f);
        if (middleImage != null && middleImage.gameObject.activeSelf) middleImage.color = new Color(1, 1, 1, 0f);
        if (fullViewImage != null && fullViewImage.gameObject.activeSelf) fullViewImage.color = new Color(1, 1, 1, 0f);
        dialogGroup.alpha = 0f;
        dialogGroup.blocksRaycasts = false;

        dialogPanel.SetActive(false);
        ChangeImageHolderState(leftImage, false);
        ChangeImageHolderState(rightImage, false);
        ChangeImageHolderState(middleImage, false);
        if (fullViewImage != null) ChangeImageHolderState(fullViewImage, false);

        yield return new WaitForSeconds(0.25f);

        // Fade to black
        yield return StartCoroutine(FadeToAlpha(1f));
        dialogText.text = " ";

        // Switch dialog data
        currentDialog = currentDialog?.nextDialog;
        currentLineIndex = 0;
        UpdateBackground();

        yield return new WaitForSeconds(0.25f);

        // Fade from black
        yield return StartCoroutine(FadeToAlpha(0f));

        // Apply alignments for new dialog
        ManageAlignments();

        // Only enable dialog UI if new dialog exists and is not background-only
        bool showDialogUI = currentDialog != null && !currentDialog.backgroundOnly;
        dialogPanel.SetActive(showDialogUI);
        dialogGroup.blocksRaycasts = showDialogUI;
        dialogGroup.alpha = 0f;

        Color dimColorStart = new Color(0.65f, 0.65f, 0.65f, 0f);
        Color dimColorEnd = new Color(0.65f, 0.65f, 0.65f, 1f);

        if (leftImage != null && leftImage.gameObject.activeSelf) leftImage.color = dimColorStart;
        if (rightImage != null && rightImage.gameObject.activeSelf) rightImage.color = dimColorStart;
        if (middleImage != null && middleImage.gameObject.activeSelf) middleImage.color = dimColorStart;
        if (fullViewImage != null && fullViewImage.gameObject.activeSelf) fullViewImage.color = dimColorStart;

        float elapsed = 0f;
        while (elapsed < elementsFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / elementsFadeDuration;
            float currentAlpha = Mathf.Lerp(0f, 1f, t);

            if (leftImage != null && leftImage.gameObject.activeSelf) leftImage.color = new Color(0.65f, 0.65f, 0.65f, currentAlpha);
            if (rightImage != null && rightImage.gameObject.activeSelf) rightImage.color = new Color(0.65f, 0.65f, 0.65f, currentAlpha);
            if (middleImage != null && middleImage.gameObject.activeSelf) middleImage.color = new Color(0.65f, 0.65f, 0.65f, currentAlpha);
            if (fullViewImage != null && fullViewImage.gameObject.activeSelf) fullViewImage.color = new Color(0.65f, 0.65f, 0.65f, currentAlpha);

            dialogGroup.alpha = currentAlpha;

            yield return null;
        }

        if (leftImage != null && leftImage.gameObject.activeSelf) leftImage.color = dimColorEnd;
        if (rightImage != null && rightImage.gameObject.activeSelf) rightImage.color = dimColorEnd;
        if (middleImage != null && middleImage.gameObject.activeSelf) middleImage.color = dimColorEnd;
        if (fullViewImage != null && fullViewImage.gameObject.activeSelf) fullViewImage.color = dimColorEnd;
        dialogGroup.alpha = 1f;

        // Set transition state correctly so Update() won't block clicks forever.
        if (currentDialog == null)
        {
            transitionState = TransitionState.None;
            yield break;
        }

        if (currentDialog.backgroundOnly)
        {
            // For background-only: ensure UI hidden and allow Update to advance lines
            dialogPanel.SetActive(false);
            ChangeOptionButtonsState(false);
            ChangeImageHolderState(leftImage, false);
            ChangeImageHolderState(rightImage, false);
            ChangeImageHolderState(middleImage, false);
            if (fullViewImage != null) ChangeImageHolderState(fullViewImage, false);

            isTalking = false;
            lineFullyVisible = true;
            skipTypingRequest = false;

            transitionState = TransitionState.None; // allow clicks to be processed by Update()
        }
        else
        {
            // For normal dialogs, wait for a click to start the new dialog (preserves original UX)
            transitionState = TransitionState.WaitingForClickPostFade;
        }
    }
    private IEnumerator FadeToAlpha(float targetAlpha)
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("FadeImage není přiřazen!");
            yield break;
        }

        // Ensure the fade image blocks raycasts while visible/fading
        fadeImage.raycastTarget = true;

        float startAlpha = fadeImage.color.a;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;

            // lineární interpolace (můžeš změnit na Mathf.SmoothStep pro jemnější křivku)
            float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            fadeImage.color = new Color(
                fadeImage.color.r,
                fadeImage.color.g,
                fadeImage.color.b,
                currentAlpha
            );

            yield return null;
        }

        // zajistíme přesnou cílovou hodnotu (kvůli float přesnosti)
        fadeImage.color = new Color(
            fadeImage.color.r,
            fadeImage.color.g,
            fadeImage.color.b,
            targetAlpha
        );

        // Only block raycasts when alpha > 0
        fadeImage.raycastTarget = targetAlpha > 0f;
    }


    private void ChangeImageHolderState(Image image, Character character)
    {
        if (image == null) return;
        if (character != null)
        {
            // Use the character's default expression (GetSprite default)
            image.sprite = character.GetSprite();
        }
        image.gameObject.SetActive(character != null);
    }
    private void ChangeImageHolderState(Image image, bool state) { if (image != null) image.gameObject.SetActive(state); }

    // Public API: set main character expression (for current dialog)
    public void SetExpression(string expressionName)
    {
        if (currentDialog == null || currentDialog.character == null) return;
        currentDialog.expression = expressionName;
        ManageAlignments();
    }

    // Public API: set expression for a specific position (left/right/middle/fullview) — uses additional characters if present
    public void SetExpressionForPosition(Dialog.Position position, string expressionName)
    {
        if (currentDialog == null) return;

        switch (position)
        {
            case Dialog.Position.Left:
                if (currentDialog.position == Dialog.Position.Left && currentDialog.character != null)
                {
                    currentDialog.expression = expressionName;
                    leftImage.sprite = currentDialog.character.GetSprite(expressionName);
                    leftImage.gameObject.SetActive(true);
                }
                else if (currentDialog.additionalCharacterLeft != null)
                {
                    leftImage.sprite = currentDialog.additionalCharacterLeft.GetSprite(expressionName);
                    leftImage.gameObject.SetActive(true);
                }
                break;
            case Dialog.Position.Right:
                if (currentDialog.position == Dialog.Position.Right && currentDialog.character != null)
                {
                    currentDialog.expression = expressionName;
                    rightImage.sprite = currentDialog.character.GetSprite(expressionName);
                    rightImage.gameObject.SetActive(true);
                }
                else if (currentDialog.additionalCharacterRight != null)
                {
                    rightImage.sprite = currentDialog.additionalCharacterRight.GetSprite(expressionName);
                    rightImage.gameObject.SetActive(true);
                }
                break;
            case Dialog.Position.Middle:
                if (currentDialog.position == Dialog.Position.Middle && currentDialog.character != null)
                {
                    currentDialog.expression = expressionName;
                    middleImage.sprite = currentDialog.character.GetSprite(expressionName);
                    middleImage.gameObject.SetActive(true);
                }
                else if (currentDialog.additionalCharacterMiddle != null)
                {
                    middleImage.sprite = currentDialog.additionalCharacterMiddle.GetSprite(expressionName);
                    middleImage.gameObject.SetActive(true);
                }
                break;
            case Dialog.Position.FullView:
                if (currentDialog.position == Dialog.Position.FullView && currentDialog.character != null)
                {
                    currentDialog.expression = expressionName;
                    if (fullViewImage != null) fullViewImage.sprite = currentDialog.character.GetSprite(expressionName);
                    if (fullViewImage != null) fullViewImage.gameObject.SetActive(true);
                    leftImage.gameObject.SetActive(false);
                    rightImage.gameObject.SetActive(false);
                    middleImage.gameObject.SetActive(false);
                }
                else
                {
                    // fallback to middle additional character if present
                    if (currentDialog.additionalCharacterMiddle != null && fullViewImage != null)
                    {
                        fullViewImage.sprite = currentDialog.additionalCharacterMiddle.GetSprite(expressionName);
                        fullViewImage.gameObject.SetActive(true);
                        leftImage.gameObject.SetActive(false);
                        rightImage.gameObject.SetActive(false);
                        middleImage.gameObject.SetActive(false);
                    }
                }
                break;
        }
    }

    public void ChooseOption(int choice)
    {
        option1 = choice == 1;
        option2 = choice == 2;
        option3 = choice == 3;
        option4 = choice == 4;
    }

    private void ChangeOptionButtonsState(bool state)
    {
        // hide/show and disable interactability for all 4 buttons
        if (optionButton1 != null) { optionButton1.SetActive(state); SetButtonInteractable(optionButton1, state); }
        if (optionButton2 != null) { optionButton2.SetActive(state); SetButtonInteractable(optionButton2, state); }
        if (buttonC != null) { buttonC.SetActive(state); SetButtonInteractable(buttonC, state); }
        if (buttonD != null) { buttonD.SetActive(state); SetButtonInteractable(buttonD, state); }
    }
    private void SetImageColorActive(Image image, bool state)
    {
        if (image == null) return;
        image.color = state ? new Color(1, 1, 1, 1) : new Color(0.65f, 0.65f, 0.65f, 1f);
    }
    private void UpdateBackground()
    {
        if (currentDialog == null) return;
        if (backgroundImage == null) return;

        if (currentDialog.backgroundSprite != null)
        {
            backgroundImage.sprite = currentDialog.backgroundSprite;
            backgroundImage.enabled = true;
        }
        else
        {
            backgroundImage.enabled = false;
        }
    }
}
