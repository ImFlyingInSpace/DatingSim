using UnityEngine;

public class GameVariables : MonoBehaviour
{
    private const string LOVE_KEY = "Love";

    public static int Love
    {
        get => PlayerPrefs.GetInt(LOVE_KEY, 0);
        set => PlayerPrefs.SetInt(LOVE_KEY, value);
    }
    // Volitelně: metoda pro přidání bodů (pohodlnější použití)
    public static void AddLove(int amount = 1)
    {
        Love += amount;
         Debug.Log($"Lucy love increased → {Love}");
    }

    // Pro testování / debug
    public static void ResetLucyLove()
    {
        PlayerPrefs.DeleteKey(LOVE_KEY);
        // nebo LucyLove = 0;
    }

    // Pokud budeš mít víc postav později, můžeš udělat generičtější
    // public static int GetAffection(string characterKey) { ... }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}

