using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEditor.Rendering;

public class DialogManager : MonoBehaviour
{
    public static DialogManager Instance;
    private void Awake() => Instance = this;

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
    }

    // Update is called once per frame
    void Update()
    {
        if(waitingForDecision) return;
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            ManageSpeechLogic();
        }
    }

    private void ManageSpeechLogic()
    {
        if (!currentDialog) return;
        ManageAlignments();
        if(talkingRoutine!= null&&isTalking)StopCoroutine(talkingRoutine);
        talkingRoutine= StartCoroutine(Say(currentDialog.lines[currentLineIndex], currentDialog.character.characterName));
        
    }

    private void ManageAlignments()
    {
        ChangeImageHolderState(leftImage, currentDialog.additionalCharacterLeft);
        ChangeImageHolderState(rightImage, currentDialog.additionalCharacterRight);
        ChangeImageHolderState(middleImage, currentDialog.additionalCharacterMiddle);
        switch (currentDialog.position)
        {
            case Dialog.Position.Left:
                leftImage.sprite = currentDialog.character.sprite;
                SetImageColorActive(rightImage, false);
                SetImageColorActive(leftImage, true);
                SetImageColorActive(middleImage, false);
                ChangeImageHolderState(leftImage, currentDialog.character);
                break;
            case Dialog.Position.Right:
                rightImage.sprite = currentDialog.character.sprite;
                SetImageColorActive(rightImage, true);
                SetImageColorActive(leftImage, false);
                SetImageColorActive(middleImage, false);
                ChangeImageHolderState(rightImage, currentDialog.character);
                break;
            case Dialog.Position.Middle:
                middleImage.sprite = currentDialog.character.sprite;
                SetImageColorActive(rightImage, false);
                SetImageColorActive(leftImage, false);
                SetImageColorActive(middleImage, true);
                ChangeImageHolderState(middleImage, currentDialog.character);
                break;
        }

    }

    private IEnumerator Say(string text, string charName)
    {
        dialogPanel.SetActive(true);
        if (!isTalking)
        {
            isTalking = true;
            dialogText.text = charName + ":" + "\r\n";
            for (int i = 0; i < text.Length; i++)
            {
                dialogText.text += text[i];
                yield return new WaitForSeconds(0.035f);
            }
        }
        else
        {
            dialogText.text = charName + ":" + "\r\n"+text;
        }

        isTalking = false;
        currentLineIndex++;
        FinishSayingLogic();
    }

    private void FinishSayingLogic()
    {
        if (currentDialog.requirePlayerDecision && currentLineIndex >= currentDialog.lines.Length)
        {
            StartCoroutine(ManagePlayerDecision());
            return;
        }

        if (currentLineIndex >= currentDialog.lines.Length)
        {
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

    private void ChangeImageHolderState(Image image, Character character)
    {
        if (character) image.sprite = character.sprite;
        image.gameObject.SetActive(character);
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
        image.color = state ? new Color(1, 1, 1) : new Color(0.65f, 0.65f, 0.65f);
    }
}
