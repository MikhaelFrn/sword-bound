using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menu pause : Échap (ou Start à la manette) ouvre et ferme le menu.
/// Boutons : Continuer, Recommencer, Menu principal, Quitter.
///
/// - Pause = Time.timeScale à 0 et sons en pause. CameraLook libère alors le curseur tout seul,
///   et le reverrouille à la reprise : ce script ne touche pas au verrouillage du curseur.
/// - Le prefab PauseMenu (Assets/Prefabs/UI/Resources) est ajouté automatiquement à chaque scène
///   jouable du Build (sauf MainMenu) si la scène n'en contient pas déjà un. Rien à glisser à la main.
/// - Le GameManager peut bloquer la pause (écrans de victoire / défaite) avec PauseMenu.PauseAllowed.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    /// <summary>Nom du prefab dans un dossier Resources (chargé par l'ajout automatique).</summary>
    public const string ResourcePath = "PauseMenu";

    [Tooltip("Panneau affiché pendant la pause (enfant du Canvas). Caché au démarrage.")]
    [SerializeField] private GameObject menuRoot;

    [Tooltip("Bouton sélectionné à l'ouverture (navigation clavier / manette).")]
    [SerializeField] private GameObject firstSelected;

    [Tooltip("Bouton Quitter : caché sur WebGL.")]
    [SerializeField] private GameObject quitButton;

    [Tooltip("Met le jeu en pause quand la fenêtre perd le focus (Alt+Tab). Ignoré dans l'éditeur.")]
    [SerializeField] private bool pauseOnFocusLost = true;

    /// <summary>Vrai pendant la pause.</summary>
    public static bool IsPaused { get; private set; }

    /// <summary>
    /// Mettre à false pour empêcher d'ouvrir le menu (écran de victoire, de défaite, cinématique).
    /// Remis à true à chaque chargement de scène.
    /// </summary>
    public static bool PauseAllowed { get; set; } = true;

    /// <summary>Si false, le prefab n'est plus ajouté automatiquement aux scènes.</summary>
    public static bool AutoSpawn { get; set; } = true;

    /// <summary>Appelé à l'ouverture du menu pause (ex. : couper la musique, cacher le HUD).</summary>
    public static event Action Paused;

    /// <summary>Appelé à la fermeture du menu pause.</summary>
    public static event Action Resumed;

    /// <summary>Appelé juste avant de recommencer le niveau (ex. : remettre les PV, l'étage).</summary>
    public static event Action Restarting;

    private void Awake()
    {
        MenuUtility.EnsureEventSystem();
        SetMenuVisible(false);

        if (quitButton != null && Application.platform == RuntimePlatform.WebGLPlayer)
        {
            quitButton.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        // Changement de scène pendant la pause : on ne laisse pas le jeu figé.
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }

    private void Update()
    {
        if (!MenuUtility.PausePressedThisFrame())
        {
            return;
        }

        if (IsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && pauseOnFocusLost && !Application.isEditor && !IsPaused)
        {
            Pause();
        }
    }

    // ---------------------------------------------------------------- Boutons

    /// <summary>Ouvre le menu et fige le jeu.</summary>
    public void Pause()
    {
        if (IsPaused || !PauseAllowed)
        {
            return;
        }

        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        SetMenuVisible(true);
        MenuUtility.FreeCursor();
        MenuUtility.Select(firstSelected);
        Paused?.Invoke();
    }

    /// <summary>Bouton Continuer (ou Échap) : ferme le menu et relance le jeu.</summary>
    public void Resume()
    {
        if (!IsPaused)
        {
            return;
        }

        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SetMenuVisible(false);
        MenuUtility.Select(null);
        Resumed?.Invoke();
    }

    /// <summary>Bouton Recommencer : recharge le niveau courant depuis le début.</summary>
    public void Restart()
    {
        Restarting?.Invoke();
        ClearPauseState();
        SceneLoader.ReloadCurrent();
    }

    /// <summary>Bouton Menu principal.</summary>
    public void GoToMainMenu()
    {
        ClearPauseState();
        SceneLoader.LoadMainMenu();
    }

    /// <summary>Bouton Quitter.</summary>
    public void QuitGame()
    {
        SceneLoader.Quit();
    }

    // ---------------------------------------------------------------- Interne

    private void SetMenuVisible(bool visible)
    {
        if (menuRoot != null)
        {
            menuRoot.SetActive(visible);
        }
    }

    private static void ClearPauseState()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    // ------------------------------------------------------ Ajout automatique

    // Remet les valeurs statiques à zéro à chaque Play (utile si "Enter Play Mode Options" est activé).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsPaused = false;
        PauseAllowed = true;
        AutoSpawn = true;
        Paused = null;
        Resumed = null;
        Restarting = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallAutoSpawn()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
        {
            return;
        }

        PauseAllowed = true;

        // Seulement les scènes du Build (pas les scènes de démo des packs d'assets), et pas l'écran titre.
        if (!AutoSpawn || scene.buildIndex < 0 || scene.name == SceneLoader.MainMenuScene)
        {
            return;
        }

        if (FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include) != null ||
            FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("[PauseMenu] Prefab introuvable dans un dossier Resources : " + ResourcePath);
            return;
        }

        GameObject instance = Instantiate(prefab);
        instance.name = prefab.name;
        SceneManager.MoveGameObjectToScene(instance, scene);
    }
}
