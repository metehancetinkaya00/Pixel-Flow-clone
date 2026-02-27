using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class Shooter : MonoBehaviour
{
    [Header("Shooter")]
    public BlockColor shooterColor;

    [Header("Movement")]
    public bool IsBusy;

    [Header("Shooting")]
    public float bulletFireCooldown = 0.15f;
    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;

    private Coroutine moveRoutine;
    private Coroutine shootRoutine;

    private bool canShoot;
    private bool isMoving;

    private HashSet<int> lockedDepthLines = new HashSet<int>();

    public void StartMove(PathDefinition path, Quaternion lockedRotation, System.Action onFinished)
    {
        if (IsBusy)
        {
            return;
        }

        if (path == null || path.waypoints == null || path.waypoints.Length == 0)
        {
            return;
        }

        IsBusy = true;

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }

        moveRoutine = StartCoroutine(MoveAlongPath(path, lockedRotation, onFinished));
    }

    private IEnumerator MoveAlongPath(PathDefinition path, Quaternion lockedRotation, System.Action onFinished)
    {
        transform.rotation = lockedRotation;

        canShoot = false;
        isMoving = false;

        lockedDepthLines.Clear();

        if (shootRoutine != null)
        {
            StopCoroutine(shootRoutine);
        }

        shootRoutine = StartCoroutine(ShootLoop_LineLockedDepth_Bullet());

        Transform firstWaypointt = path.waypoints[0];

        isMoving = true;

        while (Vector3.Distance(transform.position, firstWaypointt.position) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                firstWaypointt.position,
                path.moveSpeed * Time.deltaTime
            );

            transform.rotation = lockedRotation;

            yield return null;
        }

        isMoving = false;
        canShoot = true;

        for (int i = 1; i < path.waypoints.Length; i++)
        {
            Transform pathwaypoint = path.waypoints[i];

            isMoving = true;

            while (Vector3.Distance(transform.position, pathwaypoint.position) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    pathwaypoint.position,
                    path.moveSpeed * Time.deltaTime
                );

                transform.rotation = lockedRotation;

                yield return null;
            }

            isMoving = false;
        }

        StopShooting();

        IsBusy = false;
        onFinished?.Invoke();
    }

    private void StopShooting()
    {
        if (shootRoutine == null)
        {
            return;
        }

        StopCoroutine(shootRoutine);
        shootRoutine = null;
    }

    private IEnumerator ShootLoop_LineLockedDepth_Bullet()
    {
        while (true)
        {
            if (canShoot)
            {
                if (isMoving)
                {
                    if (BlockGridManager.Instance != null)
                    {
                        int side;
                        int lineIndex;

                        bool ok = BlockGridManager.Instance.TryResolveShooterLine(transform.position, out side, out lineIndex);
                        if (ok)
                        {
                            int lineKey = BlockGridManager.Instance.BuildLineKey(side, lineIndex);

                            if (!lockedDepthLines.Contains(lineKey))
                            {
                                Block target;
                                bool hasTarget = BlockGridManager.Instance.TryGetTargetByLine(shooterColor, side, lineIndex, out target);

                                if (hasTarget)
                                {
                                    FireBullet(target);
                                    lockedDepthLines.Add(lineKey);
                                }
                            }
                        }
                    }
                }
            }

            yield return new WaitForSeconds(bulletFireCooldown);
        }
    }

    private void FireBullet(Block targetBlock)
    {
        if (bulletPrefab == null)
        {
            return;
        }

        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = transform.rotation;

        if (bulletSpawnPoint != null)
        {
            spawnPos = bulletSpawnPoint.position;
            spawnRot = bulletSpawnPoint.rotation;
        }

        GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, spawnRot);
        Bullet bulletScript = bulletObj.GetComponent<Bullet>();

        if (bulletScript != null)
        {
            bulletScript.SetTarget(targetBlock);
        }
    }
}