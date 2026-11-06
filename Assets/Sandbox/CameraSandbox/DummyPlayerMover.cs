using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Faux contrôleur de joueur, UNIQUEMENT pour tester la caméra dans CameraSandbox.
/// Ne fait jamais tourner le corps : la rotation (yaw) est la responsabilité de CameraLook.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DummyPlayerMover : MonoBehaviour
{
    [SerializeField, Min(0f)] private float walkSpeed = 4f;
    [SerializeField, Min(0f)] private float runSpeed = 7f;

    private Rigidbody body;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        float x = 0f;
        float z = 0f;
        if (keyboard.dKey.isPressed) x += 1f;
        if (keyboard.aKey.isPressed) x -= 1f;
        if (keyboard.wKey.isPressed) z += 1f;
        if (keyboard.sKey.isPressed) z -= 1f;

        Vector3 direction = (transform.right * x + transform.forward * z).normalized;
        float speed = keyboard.leftShiftKey.isPressed ? runSpeed : walkSpeed;

        Vector3 velocity = body.linearVelocity;
        body.linearVelocity = new Vector3(direction.x * speed, velocity.y, direction.z * speed);
    }
}
