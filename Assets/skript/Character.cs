using System.Collections.Generic;
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

    // Use a List so you can add/remove expressions in the Inspector at runtime/editor
    public List<Expression> expressions = new List<Expression>();

    public Sprite GetSprite(string expressionName = "default")
    {
        Debug.Log($"[GetSprite] Postava: {characterName} | Hledaný výraz: '{expressionName}'");

        if (expressions == null || expressions.Count == 0)
        {
            Debug.LogWarning("Expressions prázdné!");
            return sprite;
        }

        // fallback to first expression or main sprite
        Sprite fallbackSprite = expressions[0]?.expressionSprite ?? sprite;
        if (fallbackSprite == null)
        {
            Debug.LogWarning("První sprite je null – zkontroluj inspektor!");
        }

        foreach (var exp in expressions)
        {
            if (exp == null) continue;
            if (string.Equals(exp.expressionName, expressionName, System.StringComparison.OrdinalIgnoreCase))
            {
                if (exp.expressionSprite != null)
                {
                    Debug.Log($"Nalezeno: {exp.expressionName} → {exp.expressionSprite.name}");
                    return exp.expressionSprite;
                }
                Debug.LogWarning($"Výraz '{exp.expressionName}' nalezen, ale sprite je null!");
                return fallbackSprite;
            }
        }

        Debug.Log($"Výraz '{expressionName}' nenalezen → fallback na první: {fallbackSprite?.name ?? "NULL"}");
        return fallbackSprite;
    }

    // Helper to get all expression sprites (useful from DialogManager)
    public List<Sprite> GetAllExpressionSprites()
    {
        var list = new List<Sprite>();
        if (expressions == null) return list;
        foreach (var exp in expressions)
        {
            if (exp?.expressionSprite != null) list.Add(exp.expressionSprite);
        }
        return list;
    }

    // Optional: get available expression names
    public List<string> GetExpressionNames()
    {
        var list = new List<string>();
        if (expressions == null) return list;
        foreach (var exp in expressions)
        {
            if (exp != null) list.Add(exp.expressionName);
        }
        return list;
    }
}