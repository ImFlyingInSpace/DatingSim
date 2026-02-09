using UnityEngine;

[System.Serializable]
public class Expression
{
    public string expressionName;
    public Sprite expressionSprite;
}

[CreateAssetMenu(fileName = "Character", menuName = "Scriptable Objects/Character")]
public class Character : ScriptableObject
{
    public string characterName;
    public Sprite sprite;
    public Expression[] expressions;

    public Sprite GetSprite(string expressionName = "default")
    {
        Debug.Log($"[GetSprite] Postava: {characterName} | Hledaný výraz: '{expressionName}'");

        // If no expressions configured, fall back to the main sprite if present
        if (expressions == null || expressions.Length == 0)
        {
            Debug.LogWarning("Expressions prázdné!");
            if (sprite != null)
                return sprite;
            return null;
        }

        // Use the first expression as fallback (prefer it over null)
        Sprite fallbackSprite = expressions[0].expressionSprite;
        if (fallbackSprite == null)
        {
            Debug.LogWarning("První sprite je null – zkontroluj inspektor!");
            // If a main sprite exists, prefer it as ultimate fallback
            if (sprite != null)
                fallbackSprite = sprite;
        }

        foreach (var exp in expressions)
        {
            if (string.Equals(exp.expressionName, expressionName, System.StringComparison.OrdinalIgnoreCase))
            {
                if (exp.expressionSprite != null)
                {
                    Debug.Log($"Nalezeno: {exp.expressionName} → {exp.expressionSprite.name}");
                    return exp.expressionSprite;
                }
                else
                {
                    Debug.LogWarning($"Výraz '{exp.expressionName}' nalezen, ale sprite je null!");
                    return fallbackSprite;
                }
            }
        }

        Debug.Log($"Výraz '{expressionName}' nenalezen → fallback na první: {fallbackSprite?.name ?? "NULL"}");
        return fallbackSprite;
    }
}