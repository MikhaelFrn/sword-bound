using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ControleurRobot3D : MonoBehaviour
{
    [Header("Déplacement")]
    [SerializeField] private float vitesse = 5f;
    [SerializeField] private float multiplicateurSprint = 1.6f;

    [Header("Saut")]
    [SerializeField] private float hauteurSaut = 1.2f;
    [Tooltip("Temps pendant lequel on peut encore sauter après avoir quitté le sol.")]
    [SerializeField] private float tempsCoyote = 0.12f;
    [Tooltip("Temps pendant lequel un appui sur Espace est mémorisé.")]
    [SerializeField] private float tamponSaut = 0.15f;

    private Rigidbody corps;
    private Vector3 direction;
    private bool sprint;
    private bool auSol;
    private float tempsDepuisSol = 999f;
    private float tempsDepuisAppui = 999f;

    private void Awake()
    {
        corps = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        direction = Vector3.ClampMagnitude(
            transform.right * horizontal + transform.forward * vertical,
            1f
        );

        sprint = Input.GetKey(KeyCode.LeftShift);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            tempsDepuisAppui = 0f;
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (auSol) tempsDepuisSol = 0f;
        else tempsDepuisSol += dt;
        tempsDepuisAppui += dt;

        float vitesseActuelle = vitesse * (sprint ? multiplicateurSprint : 1f);

        Vector3 v = corps.linearVelocity;
        v.x = direction.x * vitesseActuelle;
        v.z = direction.z * vitesseActuelle;

        if (tempsDepuisAppui <= tamponSaut && tempsDepuisSol <= tempsCoyote)
        {
            v.y = Mathf.Sqrt(2f * -Physics.gravity.y * hauteurSaut);
            // Consomme l'appui et le sol : pas de double saut
            tempsDepuisAppui = 999f;
            tempsDepuisSol = 999f;
        }

        corps.linearVelocity = v;
        auSol = false; // remis à vrai par OnCollisionStay s'il y a toujours un sol
    }

    private void OnCollisionStay(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                auSol = true;
                return;
            }
        }
    }
}