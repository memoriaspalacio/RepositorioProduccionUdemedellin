using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target; // The character to follow
    public float distance = 3.0f; // Distance behind the target
    public float height = 1.0f; // Height offset from the target
    public float smoothSpeed = 10.0f; // Speed of camera following the target

    public float rotationSpeed = 5.0f; // Speed of camera rotation with right click
    public float minDistance = 1.0f; // Minimum distance to avoid collisions
    public LayerMask collisionLayers; // Layers to consider for camera collision

    private float currentX = 0.0f; // Horizontal rotation
    private float currentY = 10.0f; // Vertical rotation

    private void Start()
    {
        // Initialize the camera to look at the target with some initial offset
        currentX = transform.eulerAngles.y;
        currentY = transform.eulerAngles.x;

        // Optionally lock and hide the cursor if needed
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Handle camera rotation with the right mouse button
        if (Input.GetMouseButton(1)) // Right mouse button held down
        {
            currentX += Input.GetAxis("Mouse X") * rotationSpeed;
            currentY -= Input.GetAxis("Mouse Y") * rotationSpeed;
            currentY = Mathf.Clamp(currentY, -35, 60); // Clamp vertical rotation
        }

        // Calculate the desired rotation of the camera
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        
        // Calculate the desired camera position
        Vector3 desiredPosition = target.position - rotation * Vector3.forward * distance + Vector3.up * height;

        // Check for collisions and adjust camera position if needed
        RaycastHit hit;
        if (Physics.Linecast(target.position + Vector3.up * height, desiredPosition, out hit, collisionLayers))
        {
            float hitDistance = Vector3.Distance(target.position + Vector3.up * height, hit.point);
            desiredPosition = target.position - rotation * Vector3.forward * (hitDistance - 0.1f) + Vector3.up * height;
        }

        // Smoothly move the camera to the desired position
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smoothSpeed);

        // Make the camera look at the target
        transform.LookAt(target.position + Vector3.up * height);
    }
}