using UnityEngine;

public class CircularMotion : MonoBehaviour
{
    [Header("Motion Settings")]
    [Tooltip("Diameter of the circular path (full width of the circle).")]
    public float diameter = 1f;

    [Tooltip("Speed of the circular motion.")]
    [Range(0f, 10f)]
    public float speed = 1f;

    [Tooltip("Check for clockwise motion, uncheck for counterclockwise.")]
    public bool clockwise = false;

    private Vector3 initialPosition;

    void Start()
    {
        initialPosition = transform.position;
    }

    void Update()
    {
        float angle = Time.time * speed * (clockwise ? -1f : 1f);
        float radius = diameter / 2f;
        Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        transform.position = initialPosition + offset;
    }
}