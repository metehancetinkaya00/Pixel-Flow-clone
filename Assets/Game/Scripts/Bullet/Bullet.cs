using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Bullet : MonoBehaviour
{
    public float speed = 18f;
    public float lifeTime = 4f;

    private Block targetBlock;
    private bool hasTarget;
    private bool hasHit;

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    public void SetTarget(Block blockk)
    {
        if (blockk == null)
        {
            hasTarget = false;
            targetBlock = null;
            return;
        }

        targetBlock = blockk;
        hasTarget = true;
    }

    private void Update()
    {
        if (hasHit)
        {
            return;
        }

        if (!hasTarget)
        {
            transform.position += transform.forward * speed * Time.deltaTime;
            return;
        }

        if (targetBlock == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPos = targetBlock.transform.position;
        Vector3 dir = (targetPos - transform.position).normalized;

        transform.forward = dir;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit)
        {
            return;
        }

        Block blockk = other.GetComponentInParent<Block>();
        if (blockk == null)
        {
            return;
        }

        if (targetBlock != null && blockk != targetBlock)
        {
            return;
        }

        hasHit = true;

        if (BlockGridManager.Instance != null)
        {
            BlockGridManager.Instance.DestroyBlockTween(blockk, 0.12f, 0f);
        }

        Destroy(gameObject);
    }
}