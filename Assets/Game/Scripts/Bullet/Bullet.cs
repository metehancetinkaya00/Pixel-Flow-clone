using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(AudioSource))]
public class Bullet : MonoBehaviour
{
    public float speed = 18f;
    public float lifeTime = 4f;

    public float hitDestroyDuration = 0.12f;
    public float hitDestroyDelay = 0f;

    public float arriveDistance = 0.08f;

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
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.volume = 1f;

            if (use2DSound)
            {
                audioSource.spatialBlend = 0f; // 2D ses, uzaklýktan kýsýlmaz
            }
        }
    }

    private void Start()
    {

        Invoke(nameof(Expire), lifeTime);
    }

    private void Expire()
    {
        Destroy(gameObject);
    }

    public void Init(Shooter ownerShooter, int key, Block target)
    {
        owner = ownerShooter;
        lineKey = key;
        targetBlock = target;
    }

    private void Update()
    {
        if (resolved)
        {
            return;
        }

        if (targetBlock == null)
        {
            Resolve(false);
            Destroy(gameObject);
            return;
        }

        Vector3 targetPos = targetBlock.transform.position;
        Vector3 toTarget = targetPos - transform.position;

        float dist = toTarget.magnitude;

        if (dist <= arriveDistance)
        {
            bool success = TryHitTarget();
            Resolve(success);
            DestroyAfterHit(success);
            return;
        }

        Vector3 dir = toTarget / dist;

        transform.forward = dir;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (resolved)
        {
            return;
        }

        Block b = other.GetComponentInParent<Block>();
        if (b == null)
        {
            return;
        }

        if (targetBlock != null && b != targetBlock)
        {
            return;
        }

        bool success = TryHitTarget();
        Resolve(success);
        DestroyAfterHit(success);
    }

    private bool TryHitTarget()
    {
        if (targetBlock == null)
        {
            return false;
        }

        if (BlockGridManager.Instance == null)
        {
            return false;
        }

        if (targetBlock.IsDying)
        {
            return false;
        }

        if (audioSource != null && blockHitSound != null)
        {
            audioSource.PlayOneShot(blockHitSound, blockHitVolume);
        }

        BlockGridManager.Instance.DestroyBlockTween(targetBlock, hitDestroyDuration, hitDestroyDelay);
        return true;
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

        float destroyDelayTime = blockHitSound.length + 0.05f;
        Destroy(gameObject, destroyDelayTime);
    }

    private void HideBulletVisuals()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            r.enabled = false;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders)
        {
            c.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (!resolved)
        {
            Resolve(false);
        }
    }

    private void Resolve(bool success)
    {
        if (resolved)
        {
            return;
        }

        resolved = true;

        if (!success)
        {
            if (targetBlock != null && !targetBlock.IsDying)
            {
                targetBlock.IsTargeted = false;
            }
        }

        if (owner != null)
        {
            owner.OnBulletResolved(lineKey, success);
        }
    }
}