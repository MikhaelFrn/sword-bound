using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Référence au HUD (glisser l'objet HUD ici dans l'Inspector)
    [SerializeField] private HUDController hud;

    // Accès global au GameManager
    public static GameManager Instance { get; private set; }

    // Points de vie maximum du robot
    [SerializeField] private int pointsDeVieMax = 100;

    // Tag des ennemis de la scène
    [SerializeField] private string tagEnnemi = "Ennemi";

    // Points de vie actuels
    public int PointsDeVie { get; private set; }

    // Nombre d'ennemis encore en vie
    public int EnnemisRestants { get; private set; }

    // Nombre d'ennemis éliminés
    public int EnnemisElimines { get; private set; }

    // Nombre d'objets ramassés
    public int ObjetsRamasses { get; private set; }

    // Indique si le personnage est accroupi
    public bool EstAccroupi { get; private set; }

    // Indique si le robot est détruit
    public bool EstMort { get; private set; }

    private void Awake()
    {
        // Garde une seule instance
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Commence avec la vie pleine
        PointsDeVie = pointsDeVieMax;
    }

    private void Start()
    {
        // Compte les ennemis au début de la partie
        EnnemisRestants = GameObject.FindGameObjectsWithTag(tagEnnemi).Length;
        Debug.Log("Ennemis : " + EnnemisRestants);

        // Affiche les valeurs de départ dans le HUD
        RafraichirHUD();
    }

    // Enlève des points de vie au robot
    public void PrendreDegats(int degats)
    {
        if (EstMort)
        {
            return;
        }

        PointsDeVie = Mathf.Max(PointsDeVie - degats, 0);
        Debug.Log("Points de vie : " + PointsDeVie + " / " + pointsDeVieMax);

        if (hud != null)
        {
            hud.SetHealth(PointsDeVie, pointsDeVieMax);
        }

        // Meurt si la vie tombe à 0
        if (PointsDeVie == 0)
        {
            EstMort = true;
            Debug.Log("Le robot est détruit !");
        }
    }

    // Redonne des points de vie (sans dépasser le maximum)
    public void Soigner(int soin)
    {
        if (EstMort)
        {
            return;
        }

        PointsDeVie = Mathf.Min(PointsDeVie + soin, pointsDeVieMax);
        Debug.Log("Points de vie : " + PointsDeVie + " / " + pointsDeVieMax);

        if (hud != null)
        {
            hud.SetHealth(PointsDeVie, pointsDeVieMax);
        }
    }

    // Appelée quand un objet est ramassé
    public void AjouterObjet()
    {
        ObjetsRamasses++;
        Debug.Log("Objets ramassés : " + ObjetsRamasses);

        if (hud != null)
        {
            hud.SetItemCount(ObjetsRamasses);
        }
    }

    // Appelée quand un ennemi est éliminé
    public void EnnemiElimine()
    {
        EnnemisRestants = Mathf.Max(EnnemisRestants - 1, 0);
        EnnemisElimines++;
        Debug.Log("Ennemis restants : " + EnnemisRestants);

        if (hud != null)
        {
            hud.SetKillCount(EnnemisElimines);
        }
    }

    // Appelée par le script du joueur quand il s'accroupit ou se relève
    public void SetAccroupi(bool accroupi)
    {
        EstAccroupi = accroupi;

        if (hud != null)
        {
            hud.SetCrouching(accroupi);
        }
    }

    // Met tout le HUD à jour d'un coup
    private void RafraichirHUD()
    {
        if (hud == null)
        {
            return;
        }

        hud.SetHealth(PointsDeVie, pointsDeVieMax);
        hud.SetKillCount(EnnemisElimines);
        hud.SetItemCount(ObjetsRamasses);
        hud.SetCrouching(EstAccroupi);
    }
}
