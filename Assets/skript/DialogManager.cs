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

    [Header("Background brightness")]
    [Tooltip("Brightness multiplier for normal dialogs (0 = black, 1 = original).")]
    [SerializeField] private float backgroundDimAmount = 0.65f;
    [Tooltip("Brightness multiplier when dialog.backgroundOnly == true.")]
    [SerializeField] private float backgroundLightAmount = 1f;

    [Header("Audio")]
    [Tooltip("AudioSource used to play dialog music. If not assigned, one will be created on the DialogManager GameObject.")]
    [SerializeField] private AudioSource musicSource;
    [Tooltip("Master music volume for dialog music (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 1f;

    [Tooltip("AudioSource used to play one-shot SFX for dialogs. If not assigned, one will be created.")]
    [SerializeField] private AudioSource sfxSource;
    [Tooltip("Master SFX volume for dialog one-shots (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

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

    // NEW: when a decision triggers a transition, store the chosen dialog here
    private Dialog pendingDialogForTransition;

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

        // Ensure we have an AudioSource to play music
        if (musicSource == null)
        {
            musicSource = gameObject.GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
        }
        musicSource.volume = musicVolume;

        // Ensure we have an AudioSource to play one-shot SFX
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }
        sfxSource.volume = sfxVolume;

        // Apply music and sfx for starting dialog (if any)
        ApplyDialogMusic(currentDialog);
        ApplyDialogSfx(currentDialog);
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

        // Gather references
        Character addL = currentDialog?.additionalCharacterLeft;
        Character addR = currentDialog?.additionalCharacterRight;
        Character addM = currentDialog?.additionalCharacterMiddle;
        Sprite mainSprite = currentDialog?.character != null ? currentDialog.character.GetSprite(currentDialog.expression) : null;

        // Decide which image should be visible based on additional characters or main character position
        bool showLeft = addL != null || (currentDialog.position == Dialog.Position.Left && currentDialog.character != null);
        bool showRight = addR != null || (currentDialog.position == Dialog.Position.Right && currentDialog.character != null);
        bool showMiddle = addM != null || (currentDialog.position == Dialog.Position.Middle && currentDialog.character != null);
        bool showFull = currentDialog.position == Dialog.Position.FullView && currentDialog.character != null;

        // Assign sprites: prefer additional character sprite when present, otherwise use main character sprite if appropriate
        if (addL != null)
            leftImage.sprite = addL.GetSprite();
        else if (currentDialog.position == Dialog.Position.Left && mainSprite != null)
            leftImage.sprite = mainSprite;

        if (addR != null)
            rightImage.sprite = addR.GetSprite();
        else if (currentDialog.position == Dialog.Position.Right && mainSprite != null)
            rightImage.sprite = mainSprite;

        if (addM != null)
            middleImage.sprite = addM.GetSprite();
        else if (currentDialog.position == Dialog.Position.Middle && mainSprite != null)
            middleImage.sprite = mainSprite;

        if (showFull && fullViewImage != null && mainSprite != null)
            fullViewImage.sprite = mainSprite;
        // keep additional middle on middleImage by default (no action needed here)

        // Set active states (don't forcibly hide additional characters)
        if (leftImage != null) leftImage.gameObject.SetActive(showLeft);
        if (rightImage != null) rightImage.gameObject.SetActive(showRight);
        if (middleImage != null) middleImage.gameObject.SetActive(showMiddle);
        if (fullViewImage != null) fullViewImage.gameObject.SetActive(showFull);

        // Apply dimming: highlight the side where main character (currentDialog.character) sits
        SetImageColorActive(leftImage, currentDialog.position == Dialog.Position.Left);
        SetImageColorActive(rightImage, currentDialog.position == Dialog.Position.Right);
        SetImageColorActive(middleImage, currentDialog.position == Dialog.Position.Middle);
        if (fullViewImage != null) SetImageColorActive(fullViewImage, currentDialog.position == Dialog.Position.FullView);
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

            // Apply music and sfx change (if any) for the new dialog
            ApplyDialogMusic(currentDialog);
            ApplyDialogSfx(currentDialog);

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

    // REPLACE existing ManagePlayerDecision() with this version
    private IEnumerator ManagePlayerDecision()
    {
        waitingForDecision = true;

        // remember the dialog where the decision was requested
        Dialog sourceDialog = currentDialog;

        // Decide which option pair to show based on Love gate
        bool useGate = sourceDialog != null && sourceDialog.useLoveThreshold;
        bool gatePassed = useGate && GameVariables.Love >= sourceDialog.loveThreshold;

        if (gatePassed && (!string.IsNullOrEmpty(sourceDialog.option3Text) || !string.IsNullOrEmpty(sourceDialog.option4Text)))
        {
            // show alternate buttons 3 & 4
            optionButton1?.SetActive(false);
            optionButton2?.SetActive(false);

            if (buttonC != null)
            {
                buttonC.SetActive(true);
                buttonCText.text = string.IsNullOrEmpty(sourceDialog.option3Text) ? sourceDialog.option1Text : sourceDialog.option3Text;
                SetButtonInteractable(buttonC, true);
            }
            if (buttonD != null)
            {
                buttonD.SetActive(true);
                buttonDText.text = string.IsNullOrEmpty(sourceDialog.option4Text) ? sourceDialog.option2Text : sourceDialog.option4Text;
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
                optionButton1Text.text = sourceDialog.option1Text;
                SetButtonInteractable(optionButton1, true);
            }
            if (optionButton2 != null)
            {
                optionButton2.SetActive(true);
                optionButton2Text.text = sourceDialog.option2Text;
                SetButtonInteractable(optionButton2, true);
            }
        }

        // wait for any of the possible choices (1..4)
        yield return new WaitUntil(() => option1 || option2 || option3 || option4);

        // Determine chosen next dialog (do NOT overwrite currentDialog yet)
        Dialog chosenDialog = null;

        if (option1)
        {
            if (gatePassed && sourceDialog.option3NextDialog != null)
            {
                ApplyLucyChange(sourceDialog.option3AddsLove, sourceDialog.option3LoveAmount);
                chosenDialog = sourceDialog.option3NextDialog;
            }
            else
            {
                ApplyLucyChange(sourceDialog.option1AddsLove, sourceDialog.option1LoveAmount);
                chosenDialog = sourceDialog.option1NextDialog;
            }
            option1 = false;
        }

        if (option2)
        {
            if (gatePassed && sourceDialog.option4NextDialog != null)
            {
                ApplyLucyChange(sourceDialog.option4AddsLove, sourceDialog.option4LoveAmount);
                chosenDialog = sourceDialog.option4NextDialog;
            }
            else
            {
                ApplyLucyChange(sourceDialog.option2AddsLove, sourceDialog.option2LoveAmount);
                chosenDialog = sourceDialog.option2NextDialog;
            }
            option2 = false;
        }

        if (option3)
        {
            ApplyLucyChange(sourceDialog.option3AddsLove, sourceDialog.option3LoveAmount);
            chosenDialog = sourceDialog.option3NextDialog ?? sourceDialog.option1NextDialog;
            option3 = false;
        }

        if (option4)
        {
            ApplyLucyChange(sourceDialog.option4AddsLove, sourceDialog.option4LoveAmount);
            chosenDialog = sourceDialog.option4NextDialog ?? sourceDialog.option2NextDialog;
            option4 = false;
        }

        currentLineIndex = 0;
        waitingForDecision = false;

        // hide all option buttons and stop interactability
        ChangeOptionButtonsState(false);

        // If the SOURCE dialog (where decision occurred) requests a scene transition, start it now.
        if (sourceDialog != null && sourceDialog.triggerSceneTransition)
        {
            // store chosen dialog so SceneTransitionRoutine can set it after fade
            pendingDialogForTransition = chosenDialog;

            // ensure SceneTransitionRoutine uses sourceDialog as the "from" dialog
            currentDialog = sourceDialog;
            transitionState = TransitionState.Fading;
            StartCoroutine(SceneTransitionRoutine());
            yield break;
        }

        // Otherwise continue with chosen dialog immediately
        currentDialog = chosenDialog;

        // Apply music and sfx change for chosen dialog (if any)
        ApplyDialogMusic(currentDialog);
        ApplyDialogSfx(currentDialog);

        ManageSpeechLogic();
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

        // fade out UI elements (same as before)
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

        // Switch dialog data: if pendingDialogForTransition is set, use it; otherwise use currentDialog.nextDialog
        Dialog next = pendingDialogForTransition ?? currentDialog?.nextDialog;
        pendingDialogForTransition = null;
        currentDialog = next;
        currentLineIndex = 0;

        // Apply music and sfx change (if any) for dialog after transition
        ApplyDialogMusic(currentDialog);
        ApplyDialogSfx(currentDialog);

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

        // Dodáno pro úpravu jasu pozadí na základě dialogu
        if (currentDialog.backgroundOnly)
        {
            // Pokud je pouze pozadí, nastavte jas na plný (žádná úprava)
            backgroundImage.color = new Color(1, 1, 1, 1);
        }
        else
        {
            // Pokud je dialog normální, ztmavte pozadí podle backgroundDimAmount
            backgroundImage.color = new Color(backgroundDimAmount, backgroundDimAmount, backgroundDimAmount, 1);
        }
    }

    private void ApplyLucyChange(bool flag, int amount)
    {
        if (flag || amount != 0)
        {
            int a = amount != 0 ? amount : 1;
            GameVariables.AddLove(a);
        }
    }

    // NEW: Apply dialog audio (called whenever currentDialog is changed)
    private void ApplyDialogMusic(Dialog dialog)
    {
        if (musicSource == null) return;

        if (dialog == null) return;

        // If dialog explicitly requests stopping music, stop it.
        if (dialog.stopMusic)
        {
            musicSource.Stop();
            musicSource.clip = null;
            return;
        }

        // If dialog has a music clip, switch to it (looped). If it is null, keep playing current music.
        if (dialog.music != null)
        {
            // If same clip already playing, just ensure volume and looping
            if (musicSource.clip == dialog.music)
            {
                musicSource.loop = true;
                musicSource.volume = musicVolume;
                if (!musicSource.isPlaying) musicSource.Play();
                return;
            }

            musicSource.clip = dialog.music;
            musicSource.loop = true;
            musicSource.volume = musicVolume;
            musicSource.Play();
        }
        // If dialog.music is null -> do nothing (keep current music playing)
    }

    // NEW: Play one-shot dialog SFX (separate from background music)
    private void ApplyDialogSfx(Dialog dialog)
    {
        if (sfxSource == null) return;
        if (dialog == null) return;

        if (dialog.sfx != null)
        {
            float volume = Mathf.Clamp01(sfxVolume * dialog.sfxVolume);
            sfxSource.PlayOneShot(dialog.sfx, volume);
        }
    }

    // Expose runtime setters for music / sfx volume (optional)
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null) musicSource.volume = musicVolume;
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null) sfxSource.volume = sfxVolume;
    }

    private void HandleTransitionClick()
    {
        Debug.Log("HandleTransitionClick invoked, state: " + transitionState);
        switch (transitionState)
        {
            case TransitionState.WaitingForClickFadingIn:
                // Start the fade immediately when waiting to fade in
                transitionState = TransitionState.Fading;
                StartCoroutine(SceneTransitionRoutine());
                break;

            case TransitionState.WaitingForClickPostFade:
                // After fade completed and waiting for click to proceed to next dialog lines
                transitionState = TransitionState.None;
                ManageSpeechLogic();
                break;

            default:
                // No-op for other states
                break;
        }
    }
}
