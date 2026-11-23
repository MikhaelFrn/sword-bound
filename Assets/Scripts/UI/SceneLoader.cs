using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Noms des scènes et changements de scène communs aux menus.
/// Un seul endroit à modifier si une scène est renommée (ex. TerrainTest -> Niveau1).
/// Les scènes doivent être dans File > Build Profiles (Build Settings) pour être chargées.
/// </summary>
public static class SceneLoader
{
    /// <summary>Écran titre (index 0 du build).</summary>
    public const string MainMenuScene = "MainMenu";

    /// <summary>Première scène jouable (niveau 1).</summary>
    public const string FirstLevelScene = "TerrainTest";

    /// <summary>Charge une scène par son nom en remettant le temps à la normale.</summary>
    public static void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneLoader] Nom de scène vide.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("[SceneLoader] La scène '" + sceneName + "' n'est pas dans le Build " +
                           "(File > Build Profiles > Scene List).");
            return;
        }

        ResetTime();
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>Recharge la scène courante (bouton Recommencer).</summary>
    public static void ReloadCurrent()
    {
        int index = SceneManager.GetActiveScene().buildIndex;
        if (index < 0)
        {
            Debug.LogError("[SceneLoader] La scène courante n'est pas dans le Build : impossible de la recharger.");
            return;
        }

        ResetTime();
        SceneManager.LoadScene(index);
    }

    public static void LoadMainMenu()
    {
        Load(MainMenuScene);
    }

    public static void LoadFirstLevel()
    {
        Load(FirstLevelScene);
    }

    /// <summary>Quitte le jeu (arrête le mode Play dans l'éditeur).</summary>
    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Une pause oubliée (timeScale = 0) figerait la scène suivante.</summary>
    private static void ResetTime()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}
