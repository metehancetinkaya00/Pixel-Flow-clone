using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(AudioSource))]
public class Bullet : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 18f;
    public float lifeTime = 4f;
    public float arriveDistance = 0.08f;

    [Header("Hit")]
    public float hitDestroyDuration = 0.12f;
    public float hitDestroyDelay = 0f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip blockHitSound;
    [Range(0f, 1f)] public float blockHitVolume = 1f;
    public bool use2DSound = true;

    private Shooter owner;
    private int lineKey;
    private Block targetBlock;

    private bool resolved;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.volume = 1f;
            audioSource.spatialBlend = use2DSound ? 0f : 1f;
        }
    }

    private void Start()
    {
        Invoke(nameof(Expire), lifeTime);
    }

    public void Init(Shooter ownerShooter, int key, Block target)
    {
        owner = ownerShooter;
        lineKey = key;
        targetBlock = target;
    }

    private void Update()
    {
        if (resolved) return;

        if (targetBlock == null)
        {
            Resolve(success: false);
            Destroy(gameObject);
            return;
        }

        Vector3 toTarget = targetBlock.transform.position - transform.position;
        float distance = toTarget.magnitude;

        if (distance <= arriveDistance)
        {
            bool hit = TryHitTarget();
            Resolve(hit);
            DestroyAfterHit(hit);
            return;
        }

        transform.forward = toTarget / distance;
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetBlock.transform.position,
            speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (resolved) return;

        Block block = other.GetComponentInParent<Block>();
        if (block == null) return;
        if (targetBlock != null && block != targetBlock) return;

        bool hit = TryHitTarget();
        Resolve(hit);
        DestroyAfterHit(hit);
    }

    private void OnDestroy()
    {
        if (!resolved)
            Resolve(success: false);
    }

    private bool TryHitTarget()
    {
        if (targetBlock == null) return false;
        if (BlockGridManager.Instance == null) return false;
        if (targetBlock.IsDying) return false;

        if (audioSource != null && blockHitSound != null)
            audioSource.PlayOneShot(blockHitSound, blockHitVolume);

        BlockGridManager.Instance.DestroyBlockTween(targetBlock, hitDestroyDuration, hitDestroyDelay);
        return true;
    }

    private void Resolve(bool success)
    {
        if (resolved) return;
        resolved = true;

        if (!success && targetBlock != null && !targetBlock.IsDying)
            targetBlock.IsTargeted = false;

        owner?.OnBulletResolved(lineKey, success);
    }

    private void DestroyAfterHit(bool success)
    {
        CancelInvoke(nameof(Expire));

        if (!success || blockHitSound == null)
        {
            Destroy(gameObject);
            return;
        }

        HideBulletVisuals();
        Destroy(gameObject, blockHitSound.length + 0.05f);
    }

    private void HideBulletVisuals()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = false;

        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;
    }

    private void Expire() => Destroy(gameObject);
}
