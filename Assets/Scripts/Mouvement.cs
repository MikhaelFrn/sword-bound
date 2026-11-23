using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class ControleurRobot3D : MonoBehaviour
{
    [Header("Déplacement")]
    [SerializeField] private float vitesse = 5f;
    [SerializeField] private float multiplicateurSprint = 1.6f;
    [SerializeField] private float multiplicateurAccroupi = 0.5f;

    [Header("Saut")]
    [SerializeField] private float hauteurSaut = 1.2f;
    [SerializeField] private float tempsCoyote = 0.12f;
    [SerializeField] private float tamponSaut = 0.15f;

    [Header("Accroupi")]
    [SerializeField] private KeyCode toucheAccroupi = KeyCode.LeftControl;
    [SerializeField] private float hauteurAccroupie = 1.0f;
    [SerializeField] private float vitesseTransition = 10f;
    [Tooltip("Le PlayerCameraRig (enfant du Player).")]
    [SerializeField] private Transform rigCamera;

    private Rigidbody corps;
    private CapsuleCollider capsule;
    private Vector3 direction;
    private bool sprint;
    private bool auSol;
    private float tempsDepuisSol = 999f;
    private float tempsDepuisAppui = 999f;

    // Accroupi
    private bool accroupi;
    private float hauteurDebout;
    private float basCapsule;          // hauteur des pieds dans l'espace local (reste fixe)
    private float camYDebout;
    private float camYAccroupi;
    private readonly Collider[] tampon = new Collider[8];

    private void Awake()
    {
        corps = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        hauteurDebout = capsule.height;
        basCapsule = capsule.center.y - capsule.height / 2f;

        if (rigCamera != null)
        {
            camYDebout = rigCamera.localPosition.y;
            camYAccroupi = camYDebout - (hauteurDebout - hauteurAccroupie);
        }
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        direction = Vector3.ClampMagnitude(
            transform.right * horizontal + transform.forward * vertical,
            1f
        );

        bool veutAccroupi = Input.GetKey(toucheAccroupi);
        if (veutAccroupi)
        {
            accroupi = true;
        }
        else if (accroupi && PeutSeLever())
        {
            accroupi = false;
        }

        // Pas de sprint accroupi
        sprint = Input.GetKey(KeyCode.LeftShift) && !accroupi;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            tempsDepuisAppui = 0f;
        }

        MettreAJourHitboxEtCamera();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (auSol) tempsDepuisSol = 0f;
        else tempsDepuisSol += dt;
        tempsDepuisAppui += dt;

        float vitesseActuelle = vitesse;
        if (accroupi) vitesseActuelle *= multiplicateurAccroupi;
        else if (sprint) vitesseActuelle *= multiplicateurSprint;

        Vector3 v = corps.linearVelocity;
        v.x = direction.x * vitesseActuelle;
        v.z = direction.z * vitesseActuelle;

        if (!accroupi && tempsDepuisAppui <= tamponSaut && tempsDepuisSol <= tempsCoyote)
        {
            v.y = Mathf.Sqrt(2f * -Physics.gravity.y * hauteurSaut);
            tempsDepuisAppui = 999f;
            tempsDepuisSol = 999f;
        }

        corps.linearVelocity = v;
        auSol = false;
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

    // ------------------------------------------------------------ Accroupi

    private void MettreAJourHitboxEtCamera()
    {
        float hauteurCible = accroupi ? hauteurAccroupie : hauteurDebout;
        float t = 1f - Mathf.Exp(-vitesseTransition * Time.deltaTime);

        // La capsule rétrécit vers le haut : les pieds restent au même endroit
        float h = Mathf.Lerp(capsule.height, hauteurCible, t);
        if (Mathf.Abs(h - hauteurCible) < 0.001f) h = hauteurCible;
        capsule.height = h;
        capsule.center = new Vector3(capsule.center.x, basCapsule + h / 2f, capsule.center.z);

        if (rigCamera != null)
        {
            Vector3 p = rigCamera.localPosition;
            p.y = Mathf.Lerp(p.y, accroupi ? camYAccroupi : camYDebout, t);
            rigCamera.localPosition = p;
        }
    }

    // Vérifie qu'il y a la place pour se lever (rien au-dessus de la tête)
    private bool PeutSeLever()
    {
        float rayon = capsule.radius * 0.95f; // un peu plus petit pour ne pas toucher les murs
        Vector3 pieds = transform.position + Vector3.up * basCapsule;
        Vector3 p1 = pieds + Vector3.up * (capsule.radius);
        Vector3 p2 = pieds + Vector3.up * (hauteurDebout - capsule.radius);

        int n = Physics.OverlapCapsuleNonAlloc(p1, p2, rayon, tampon, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (!tampon[i].transform.IsChildOf(transform))
            {
                return false;
            }
        }
        return true;
    }
}