using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "Dialog", menuName = "Scriptable Objects/Dialog")]
public class Dialog : ScriptableObject
{
    [Header("Main + addChar")]

    public Sprite backgroundSprite;
    public Character character;
    public string expression = "default";
    public Position position;
    public string[] lines;

    public Character additionalCharacterLeft;
    public Character additionalCharacterMiddle;
    public Character additionalCharacterRight;

    [Header("Next dialog / options")]

    public Dialog nextDialog;
    public bool triggerSceneTransition;
    public bool requirePlayerDecision;

    [Header("Opt 1")]
    public string option1Text;
    public Dialog option1NextDialog;
    public bool option1AddsLove;
    public int option1LoveAmount = 1;

    [Header("Opt 2")]
    public string option2Text;
    public Dialog option2NextDialog;
    public bool option2AddsLove;
    public int option2LoveAmount = 1;

    [Header("Threshold gate")]
    public bool useLoveThreshold;
    public int loveThreshold = 0;

    [Header("Opt 3")]
    public string option3Text;
    public Dialog option3NextDialog;
    public bool option3AddsLove;
    public int option3LoveAmount = 1;

    [Header("Opt 4")]
    public string option4Text;
    public Dialog option4NextDialog;
    public bool option4AddsLove;
    public int option4LoveAmount = 1;

    [Space(6)]
    [Header("Display")]
    [Tooltip("If true: show only the background (no characters, no textbox, no buttons, no dim). Click still advances the dialog.")]
    public bool backgroundOnly = false;

    public enum Position
    {
        Left, Right, Middle, FullView
    }
}
