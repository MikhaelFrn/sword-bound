using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ControleurRobot3D : MonoBehaviour
{
    // Vitesse de d�placement
    [SerializeField] private float vitesse = 5f;

    // Vitesse de rotation
    [SerializeField] private float vitesseRotation = 10f;

    // Force du saut
    [SerializeField] private float forceSaut = 6f;

    // Distance du rayon pour d�tecter le sol
    [SerializeField] private float distanceSol = 1.1f;

    // Distance maximale pour ramasser un objet
    [SerializeField] private float porteeRamassage = 1.5f;

    // Tag des objets ramassables
    [SerializeField] private string tagRamassable = "Ramassable";

    // Points de vie maximum
    [SerializeField] private int pointsDeVieMax = 100;

    private Rigidbody corps;
    private Vector3 direction;
    private bool veutSauter;

    // Points de vie actuels
    private int pointsDeVie;

    // Indique si le robot est d�truit
    private bool estMort;

    // Objets ramass�s
    private List<GameObject> inventaire = new List<GameObject>();

    private void Awake()
    {
        // R�cup�re le Rigidbody
        corps = GetComponent<Rigidbody>();

        // Commence avec la vie pleine
        pointsDeVie = pointsDeVieMax;
    }

    private void Update()
    {
        // Ignore les touches si le robot est mort
        if (estMort)
        {
            return;
        }

        // Lit les touches
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // Calcule la direction (limit�e � 1 pour la diagonale)
        direction = Vector3.ClampMagnitude(
            new Vector3(horizontal, 0f, vertical),
            1f
        );

        // Demande un saut avec la barre d'espace
        if (Input.GetKeyDown(KeyCode.Space))
        {
            veutSauter = true;
        }

        // Ramasse un objet avec la touche E
        if (Input.GetKeyDown(KeyCode.E))
        {
            Ramasser();
        }
    }

    private void FixedUpdate()
    {
        // Arr�te le robot s'il est mort
        if (estMort)
        {
            corps.linearVelocity = new Vector3(0f, corps.linearVelocity.y, 0f);
            return;
        }

        // D�place le robot (sans toucher � la vitesse verticale)
        Vector3 vitesseActuelle = corps.linearVelocity;
        corps.linearVelocity = new Vector3(
            direction.x * vitesse,
            vitesseActuelle.y,
            direction.z * vitesse
        );

        // Saute si le robot est au sol
        if (veutSauter && EstAuSol())
        {
            corps.AddForce(Vector3.up * forceSaut, ForceMode.VelocityChange);
        }
        veutSauter = false;

        // Ne tourne pas si le robot est immobile
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        // Calcule la rotation voulue
        Quaternion cible = Quaternion.LookRotation(
            direction,
            Vector3.up
        );

        // Tourne progressivement vers la direction
        corps.MoveRotation(
            Quaternion.Slerp(
                corps.rotation,
                cible,
                vitesseRotation * Time.fixedDeltaTime
            )
        );
    }

    private void Mourir()
    {
        // Bloque les contr�les
        estMort = true;
        direction = Vector3.zero;
        Debug.Log("Le robot est d�truit !");
    }

    private bool EstAuSol()
    {
        // Lance un rayon vers le bas pour d�tecter le sol
        return Physics.Raycast(
            transform.position,
            Vector3.down,
            distanceSol
        );
    }

    private void Ramasser()
    {
        // Trouve tous les colliders proches
        Collider[] proches = Physics.OverlapSphere(
            transform.position,
            porteeRamassage
        );

        GameObject plusProche = null;
        float distanceMin = float.MaxValue;

        // Cherche l'objet ramassable le plus proche
        foreach (Collider c in proches)
        {
            if (!c.CompareTag(tagRamassable))
            {
                continue;
            }

            float distance = Vector3.Distance(
                transform.position,
                c.transform.position
            );

            if (distance < distanceMin)
            {
                distanceMin = distance;
                plusProche = c.gameObject;
            }
        }

        // Ramasse l'objet trouv�
        if (plusProche != null)
        {
            inventaire.Add(plusProche);
            plusProche.SetActive(false);
            Debug.Log("Objet ramass� : " + plusProche.name);
        }
    }
}