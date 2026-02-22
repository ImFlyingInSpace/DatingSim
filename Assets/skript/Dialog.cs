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
    // expression for the left additional character (used when additionalCharacterLeft is assigned)
    public string additionalCharacterLeftExpression = "default";

    public Character additionalCharacterMiddle;
    // expression for the middle additional character (used when additionalCharacterMiddle is assigned)
    public string additionalCharacterMiddleExpression = "default";

    public Character additionalCharacterRight;
    // expression for the right additional character (used when additionalCharacterRight is assigned)
    public string additionalCharacterRightExpression = "default";

    [Header("Audio")]
    [Tooltip("Looped background music to play when this dialog becomes active. If null, current music keeps playing.")]
    public AudioClip music;
    [Tooltip("If true, stop currently playing music when this dialog becomes active.")]
    public bool stopMusic = false;

    [Tooltip("One-shot sound effect that will be played once when this dialog becomes active (separate from background music).")]
    public AudioClip sfx;
    [Tooltip("Per-dialog multiplier for SFX volume (0..1). Final volume = DialogManager.sfxVolume * this value.")]
    [Range(0f, 1f)]
    public float sfxVolume = 1f;

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
