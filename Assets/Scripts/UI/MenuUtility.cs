using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>Petits outils partagés par MainMenu et PauseMenu.</summary>
public static class MenuUtility
{
    /// <summary>
    /// Les boutons ne réagissent pas sans EventSystem. On en crée un seulement s'il n'y en a aucun
    /// dans la scène (le HUD ou une autre UI peut déjà en avoir un : pas de doublon).
    /// Le projet utilise uniquement le nouvel Input System : InputSystemUIInputModule obligatoire.
    /// </summary>
    public static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    /// <summary>Sélectionne un bouton pour la navigation clavier / manette.</summary>
    public static void Select(GameObject target)
    {
        EventSystem current = EventSystem.current;
        if (current == null || target == null)
        {
            return;
        }

        current.SetSelectedGameObject(null);
        current.SetSelectedGameObject(target);
    }

    /// <summary>Échap au clavier ou Start à la manette, pressé pendant cette frame.</summary>
    public static bool PausePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            return true;
        }

        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad.startButton.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    /// <summary>Curseur libre et visible pour cliquer dans un menu.</summary>
    public static void FreeCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
