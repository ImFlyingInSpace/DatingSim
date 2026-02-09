using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

public class DialogManager : MonoBehaviour
{
    public static DialogManager Instance;
    private void Awake() => Instance = this;

    [SerializeField] public Image backgroundImage;
    [SerializeField] public Image leftImage;
    [SerializeField] public Image rightImage;
    [SerializeField] public Image middleImage;

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
    private bool option1;
    private bool option2;
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
        ChangeImageHolderState(leftImage, false);
        ChangeImageHolderState(rightImage, false);
        ChangeImageHolderState(middleImage, false);
        dialogPanel.SetActive(false);
        ChangeOptionButtonsState(false);
        if (fadeImage)
            fadeImage.color = new Color(0, 0, 0, 0);

        skipTypingRequest = false;
        lineFullyVisible = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (waitingForDecision) return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            // 1. Nejvyšší priorita = transition (fade, čekání na klik po fade atd.)
            if (transitionState != TransitionState.None)
            {
                HandleTransitionClick();
                return;   // ← důležité – ukončíme tento klik
            }

            // 2. Pokud ještě píše text → skip na celý text
            if (isTalking)
            {
                skipTypingRequest = true;
                return;
            }

            // 3. Pokud je řádek dokončený → posun na další (nebo finish)
            if (lineFullyVisible)
            {
                lineFullyVisible = false;
                currentLineIndex++;

                if (!currentDialog) return;

                if (currentLineIndex >= currentDialog.lines.Length)
                {
                    FinishSayingLogic();

                    // ← KLÍČOVÉ: Pokud se právě nastavil transition stav, 
                    //   tak tenhle klik už může rovnou spustit přechod
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

            // 4. První klik na nový řádek → začni psát
            ManageSpeechLogic();
        }
    }

    private void ManageSpeechLogic()
    {
        if (!currentDialog) return;

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

        talkingRoutine = StartCoroutine(Say(currentDialog.lines[currentLineIndex], currentDialog.character.characterName));
    }

    private void ManageAlignments()
    {
        ChangeImageHolderState(leftImage, currentDialog.additionalCharacterLeft);
        ChangeImageHolderState(rightImage, currentDialog.additionalCharacterRight);
        ChangeImageHolderState(middleImage, currentDialog.additionalCharacterMiddle);

        Sprite currentSprite = currentDialog.character.GetSprite(currentDialog.expression);

        switch (currentDialog.position)
        {
            case Dialog.Position.Left:
                leftImage.sprite = currentSprite;
                SetImageColorActive(rightImage, false);
                SetImageColorActive(leftImage, true);
                SetImageColorActive(middleImage, false);
                //ChangeImageHolderState(leftImage, currentDialog.character);
                break;
            case Dialog.Position.Right:
                rightImage.sprite = currentSprite;
                SetImageColorActive(rightImage, true);
                SetImageColorActive(leftImage, false);
                SetImageColorActive(middleImage, false);
                //ChangeImageHolderState(rightImage, currentDialog.character);
                break;
            case Dialog.Position.Middle:
                middleImage.sprite = currentSprite;
                SetImageColorActive(rightImage, false);
                SetImageColorActive(leftImage, false);
                SetImageColorActive(middleImage, true);
                //ChangeImageHolderState(middleImage, currentDialog.character);
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
            currentLineIndex = 0;
            currentDialog = currentDialog.nextDialog;
        }
    }

    private IEnumerator ManagePlayerDecision()
    {
        waitingForDecision = true;
        ChangeOptionButtonsState(true);
        optionButton1Text.text = currentDialog.option1Text;
        optionButton2Text.text = currentDialog.option2Text;
        yield return new WaitUntil(() => option1 || option2);
        if (option1)
        {
            currentDialog = currentDialog.option1NextDialog;
            option1 = false;
        }
        if (option2)
        {
            currentDialog = currentDialog.option2NextDialog;
            option2 = false;
        }

        currentLineIndex = 0;
        waitingForDecision = false;
        ChangeOptionButtonsState(false);
        ManageSpeechLogic();
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

                // case TransitionState.Fading:  // tenhle obvykle nepotřebuješ řešit, protože coroutine běží sama
                //     break;
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

        // ── 1. Fade-out starých prvků ───────────────────────────────
        float elementsFadeDuration = 0.4f;     // rychlost fade postav + textboxu

        CanvasGroup dialogGroup = dialogPanel.GetComponent<CanvasGroup>();
        if (dialogGroup == null) dialogGroup = dialogPanel.AddComponent<CanvasGroup>();

        float fadeOutElapsed = 0f;
        while (fadeOutElapsed < elementsFadeDuration)
        {
            fadeOutElapsed += Time.deltaTime;
            float t = fadeOutElapsed / elementsFadeDuration;

            // Postavy fade out
            if (leftImage.gameObject.activeSelf) leftImage.color = new Color(1, 1, 1, Mathf.Lerp(1f, 0f, t));
            if (rightImage.gameObject.activeSelf) rightImage.color = new Color(1, 1, 1, Mathf.Lerp(1f, 0f, t));
            if (middleImage.gameObject.activeSelf) middleImage.color = new Color(1, 1, 1, Mathf.Lerp(1f, 0f, t));

            // Dialog panel fade out
            dialogGroup.alpha = Mathf.Lerp(1f, 0f, t);

            yield return null;
        }

        // Zajistíme přesnou nulu
        if (leftImage.gameObject.activeSelf) leftImage.color = new Color(1, 1, 1, 0f);
        if (rightImage.gameObject.activeSelf) rightImage.color = new Color(1, 1, 1, 0f);
        if (middleImage.gameObject.activeSelf) middleImage.color = new Color(1, 1, 1, 0f);
        dialogGroup.alpha = 0f;

        // Schováme objekty
        dialogPanel.SetActive(false);
        ChangeImageHolderState(leftImage, false);
        ChangeImageHolderState(rightImage, false);
        ChangeImageHolderState(middleImage, false);

        // Krátká pauza (můžeš snížit na 0.1–0.3s nebo úplně odstranit)
        yield return new WaitForSeconds(0.25f);

        // ── 2. Fade to black ─────────────────────────────────────────
        yield return StartCoroutine(FadeToAlpha(1f));
        dialogText.text = " ";

        // Změna obsahu scény
        currentDialog = currentDialog.nextDialog;
        currentLineIndex = 0;
        UpdateBackground();   // aktualizujeme pozadí pro nový dialog

        // Krátká pauza po načtení nového obsahu
        yield return new WaitForSeconds(0.25f);
        

        // ── 3. Fade from black ───────────────────────────────────────
        yield return StartCoroutine(FadeToAlpha(0f));

        // ── 4. Fade-in nových prvků ──────────────────────────────────
        ManageAlignments();
        dialogPanel.SetActive(true);

        // Připravíme zatmavenou barvu s alpha 0
        Color dimColorStart = new Color(0.65f, 0.65f, 0.65f, 0f);
        Color dimColorEnd = new Color(0.65f, 0.65f, 0.65f, 1f);

        // Nastavíme počáteční stav – vše zatmavené a neviditelné
        if (leftImage.gameObject.activeSelf) leftImage.color = dimColorStart;
        if (rightImage.gameObject.activeSelf) rightImage.color = dimColorStart;
        if (middleImage.gameObject.activeSelf) middleImage.color = dimColorStart;
        dialogGroup.alpha = 0f;

        // Samotný fade-in – alpha roste od 0 → 1, barva zůstává zatmavená
        float elapsed = 0f;
        while (elapsed < elementsFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / elementsFadeDuration;

            // Lerpujeme jen alpha, barva zůstává konstantně zatmavená
            float currentAlpha = Mathf.Lerp(0f, 1f, t);

            if (leftImage.gameObject.activeSelf)
                leftImage.color = new Color(0.65f, 0.65f, 0.65f, currentAlpha);

            if (rightImage.gameObject.activeSelf)
                rightImage.color = new Color(0.65f, 0.65f, 0.65f, currentAlpha);

            if (middleImage.gameObject.activeSelf)
                middleImage.color = new Color(0.65f, 0.65f, 0.65f, currentAlpha);

            dialogGroup.alpha = currentAlpha;

            yield return null;
        }

        // Zajistíme přesnou konečnou hodnotu
        if (leftImage.gameObject.activeSelf) leftImage.color = dimColorEnd;
        if (rightImage.gameObject.activeSelf) rightImage.color = dimColorEnd;
        if (middleImage.gameObject.activeSelf) middleImage.color = dimColorEnd;
        dialogGroup.alpha = 1f;

        // TADY UŽ ŽÁDNÉ SetImageColorActive(false) !!!
    }
    private IEnumerator FadeToAlpha(float targetAlpha)
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("FadeImage není přiřazen!");
            yield break;
        }

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
    }


    private void ChangeImageHolderState(Image image, Character character)
    {
        if (character != null)
        {
            // Použijeme defaultní výraz (nebo první sprite v poli)
            image.sprite = character.GetSprite("nonexistent");   // ← nebo "neutral", podle toho, jak to máš pojmenované
                                                             // Alternativa, pokud nechceš volat metodu:
                                                             // image.sprite = character.expressions.Length > 0 ? character.expressions[0].sprite : null;
        }
        image.gameObject.SetActive(character != null);
    }
    private void ChangeImageHolderState(Image image, bool state) => image.gameObject.SetActive(state);

    public void ChooseOption(int choice)
    {
        option1 = choice == 1;
        option2 = choice == 2;
    }
    private void ChangeOptionButtonsState(bool state)
    {
        optionButton1.SetActive(state);
        optionButton2.SetActive(state);
    }
    private void SetImageColorActive(Image image, bool state)
    {
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
            // optional: schovat nebo nechat default
            backgroundImage.enabled = false;
            // nebo backgroundImage.sprite = defaultBackgroundSprite;
        }
    }
}
