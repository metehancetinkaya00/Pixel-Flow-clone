using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Bullet : MonoBehaviour
{
    public float speed = 18f;
    public float lifeTime = 4f;

    public float hitDestroyDuration = 0.12f;
    public float hitDestroyDelay = 0f;

    public float arriveDistance = 0.08f;

    private Shooter owner;
    private int lineKey;
    private Block targetBlock;

    private bool resolved;

    private void Start()
    {
        Destroy(gameObject, lifeTime);
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
            Destroy(gameObject);
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
        Destroy(gameObject);
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

        BlockGridManager.Instance.DestroyBlockTween(targetBlock, hitDestroyDuration, hitDestroyDelay);
        return true;
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