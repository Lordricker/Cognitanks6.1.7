using UnityEngine;

public class ExpandingRing : MonoBehaviour
{
    public float expansionSpeed = 5f;  // How fast the ring expands (units per second).
    public float maxScale = 10f;       // Maximum size before stopping.

    private ParticleSystem ps;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        transform.localScale = Vector3.one * 0.1f;  // Start very small.
        ps.Play();
    }

    void Update()
    {
        // Expand the ring by scaling the GameObject.
        transform.localScale += Vector3.one * expansionSpeed * Time.deltaTime;

        // Stop and clean up when max size is reached.
        if (transform.localScale.x >= maxScale)
        {
            ps.Stop();
            Destroy(gameObject, ps.main.startLifetime.constant);  // Wait for particles to die.
        }
    }
}