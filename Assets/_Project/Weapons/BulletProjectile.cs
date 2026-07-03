using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 100f;
    [SerializeField] private float lifetime = 2f;

    private Vector3 _direction;

    public void Initialize(Vector3 direction)
    {
        _direction = direction.normalized;
        // Rotate bullet to face travel direction
        transform.rotation = Quaternion.LookRotation(_direction);
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // Move bullet forward every frame
        transform.position += _direction * speed * Time.deltaTime;
    }
}