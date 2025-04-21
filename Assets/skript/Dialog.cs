using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "Dialog", menuName = "Scriptable Objects/Dialog")]
public class Dialog : ScriptableObject
{
    public Character character;
    public string[] lines;
    public Position position;

    public enum Position
    {
        Left,Right,Middle
    }
    public Character additionalCharacterLeft;
    public Character additionalCharacterRight;
    public Character additionalCharacterMiddle;
    public Dialog nextDialog;
    public bool requirePlayerDecision;
    public string option1Text;
    public string option2Text;
    public Dialog option1NextDialog;
    public Dialog option2NextDialog;
}
