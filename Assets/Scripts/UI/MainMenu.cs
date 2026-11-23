using UnityEngine;

/// <summary>
/// Écran titre (scène MainMenu). Les boutons du Canvas appellent StartGame() et QuitGame()
/// via leur événement OnClick (réglé dans l'Inspector).
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("Scène chargée par le bouton Jouer. Doit être dans la liste des scènes du Build.")]
    [SerializeField] private string firstLevelScene = SceneLoader.FirstLevelScene;

    [Tooltip("Bouton sélectionné à l'ouverture (navigation clavier / manette).")]
    [SerializeField] private GameObject firstSelected;

    [Tooltip("Bouton Quitter : caché sur les plateformes où quitter n'a pas de sens (WebGL).")]
    [SerializeField] private GameObject quitButton;

    public string FirstLevelScene => firstLevelScene;

    private void Start()
    {
        // On peut arriver ici depuis le menu pause : temps figé et curseur verrouillé à corriger.
        Time.timeScale = 1f;
        AudioListener.pause = false;
        MenuUtility.FreeCursor();
        MenuUtility.EnsureEventSystem();
        MenuUtility.Select(firstSelected);

        if (quitButton != null && Application.platform == RuntimePlatform.WebGLPlayer)
        {
            quitButton.SetActive(false);
        }
    }

    /// <summary>Bouton Jouer : lance le niveau 1.</summary>
    public void StartGame()
    {
        SceneLoader.Load(firstLevelScene);
    }

    /// <summary>Bouton Quitter.</summary>
    public void QuitGame()
    {
        SceneLoader.Quit();
    }
}
