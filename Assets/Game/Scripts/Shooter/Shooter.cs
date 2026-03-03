using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;

[RequireComponent(typeof(Collider))]
public class Shooter : MonoBehaviour
{
    public BlockColor shooterColor;

    public bool IsBusy;
    public bool IsAlive = true;

    public int shotsTotal = 5;
    public int shotsRemaining;

    public Vector3 startRotationEuler;
    public Vector3[] waypointRotationEuler;

    public float rotationSpeedDegPerSec = 360f;

    public float bulletFireCooldown = 0.15f;
    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;

    public TMP_Text shotsText;

    public float moveSpeed = 8f;

    public float firstWaypointJumpDuration = 0.35f;
    public float firstWaypointJumpPower = 1.2f;
    public int firstWaypointJumpNumJumps = 1;

    public float frontJumpDuration = 0.35f;
    public float frontJumpPower = 1.2f;
    public int frontJumpNumJumps = 1;
    public Vector3 frontJumpRotationEuler;
    public float frontJumpExtraSpinY = 360f;

    private Coroutine moveRoutine;
    private Coroutine shootRoutine;

    private bool canShoot;
    private bool isMoving;

    private HashSet<int> lockedDepthLines = new HashSet<int>();

    private Quaternion rotationTarget;

    private void Awake()
    {
        shotsRemaining = shotsTotal;
        rotationTarget = Quaternion.Euler(startRotationEuler);
        UpdateShotsText();
    }

    public void StartMove(PathDefinition path, System.Action onFinished)
    {
        if (!IsAlive)
        {
            return;
        }

        if (IsBusy)
        {
            return;
        }

        if (shotsRemaining <= 0)
        {
            DestroySelf();
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

        moveRoutine = StartCoroutine(MoveAlongPath(path, onFinished));
    }

    public void JumpToFrontSlot(Vector3 targetPosition, System.Action onFinished)
    {
        if (!IsAlive)
        {
            return;
        }

        IsBusy = true;

        DOTween.Kill(transform);

        Vector3 rotateTargetEuler = frontJumpRotationEuler + new Vector3(0f, frontJumpExtraSpinY, 0f);

        Sequence seq = DOTween.Sequence();

        seq.Join(transform.DOJump(targetPosition, frontJumpPower, frontJumpNumJumps, frontJumpDuration).SetEase(Ease.OutQuad));
        seq.Join(transform.DORotate(rotateTargetEuler, frontJumpDuration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));

        seq.OnComplete(() =>
        {
            if (!IsAlive)
            {
                return;
            }

            transform.position = targetPosition;
            transform.rotation = Quaternion.Euler(frontJumpRotationEuler);

            IsBusy = false;

            if (onFinished != null)
            {
                onFinished();
            }
        });
    }

    private IEnumerator MoveAlongPath(PathDefinition path, System.Action onFinished)
    {
        rotationTarget = Quaternion.Euler(startRotationEuler);
        transform.rotation = rotationTarget;

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

        bool jumpFinished = false;

        DOTween.Kill(transform);

        transform
            .DOJump(firstWaypointt.position, firstWaypointJumpPower, firstWaypointJumpNumJumps, firstWaypointJumpDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                jumpFinished = true;
            });

        while (!jumpFinished)
        {
            if (!IsAlive)
            {
                yield break;
            }

            StepRotation();

            yield return null;
        }

        isMoving = false;
        canShoot = true;

        SetRotationTargetForWaypoint(0);

        for (int i = 1; i < path.waypoints.Length; i++)
        {
            Transform pathwaypoint = path.waypoints[i];

            isMoving = true;

            while (Vector3.Distance(transform.position, pathwaypoint.position) > 0.01f)
            {
                if (!IsAlive)
                {
                    yield break;
                }

                transform.position = Vector3.MoveTowards(
                    transform.position,
                    pathwaypoint.position,
                    moveSpeed * Time.deltaTime
                );

                StepRotation();

                yield return null;
            }

            isMoving = false;

            SetRotationTargetForWaypoint(i);

            if (!IsAlive)
            {
                yield break;
            }
        }

        StopShooting();

        IsBusy = false;

        if (onFinished != null)
        {
            onFinished();
        }
    }

    private void StepRotation()
    {
        float maxDegrees = rotationSpeedDegPerSec * Time.deltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotationTarget, maxDegrees);
    }

    private void SetRotationTargetForWaypoint(int waypointIndex)
    {
        if (waypointRotationEuler == null)
        {
            return;
        }

        if (waypointIndex < 0 || waypointIndex >= waypointRotationEuler.Length)
        {
            return;
        }

        rotationTarget = Quaternion.Euler(waypointRotationEuler[waypointIndex]);
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
            if (!IsAlive)
            {
                yield break;
            }

            if (shotsRemaining <= 0)
            {
                DestroySelf();
                yield break;
            }

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
                                    if (FireBullet(target))
                                    {
                                        lockedDepthLines.Add(lineKey);

                                        shotsRemaining -= 1;
                                        UpdateShotsText();

                                        if (shotsRemaining <= 0)
                                        {
                                            DestroySelf();
                                            yield break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            yield return new WaitForSeconds(bulletFireCooldown);
        }
    }

    private bool FireBullet(Block targetBlock)
    {
        if (bulletPrefab == null)
        {
            return false;
        }

        if (targetBlock == null)
        {
            return false;
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

        return true;
    }

    private void UpdateShotsText()
    {
        if (shotsText == null)
        {
            return;
        }

        shotsText.text = shotsRemaining.ToString();
    }

    public void DestroySelf()
    {
        if (!IsAlive)
        {
            return;
        }

        IsAlive = false;

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        StopShooting();

        IsBusy = false;

        if (ShooterQueueManager.Instance != null)
        {
            ShooterQueueManager.Instance.NotifyShooterDestroyed(this);
        }

        Transform t = transform;
        DOTween.Kill(t);

        t.DOScale(Vector3.zero, 0.18f)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                if (gameObject != null)
                {
                    Destroy(gameObject);
                }
            });
    }
}