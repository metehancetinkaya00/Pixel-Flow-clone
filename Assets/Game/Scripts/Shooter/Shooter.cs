using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using UnityEngine.Splines;
using Unity.Mathematics;

[RequireComponent(typeof(Collider))]
public class Shooter : MonoBehaviour
{
    public BlockColor shooterColor;

    public bool IsBusy;
    public bool IsAlive = true;

    public int shotsTotal = 5;
    public int shotsRemaining;

    public TMP_Text shotsText;

    public float bulletFireCooldown = 0.15f;
    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;

    public float rotationSpeedDegPerSec = 720f;

    public float toSplineJumpDuration = 0.35f;
    public float toSplineJumpPower = 1.2f;
    public int toSplineJumpNumJumps = 1;
    public Vector3 toSplineJumpRotationEuler;
    public float toSplineJumpExtraSpinY = 360f;

    public bool useSplineTangentRotation = true;
    public bool invertSplineTangent = false;
    public Vector3 splineRotationOffsetEuler;
    public Vector3 splineFixedRotationEuler;
    public float splineRotationLookAheadT = 0.03f;

    public float frontJumpDuration = 0.35f;
    public float frontJumpPower = 1.2f;
    public int frontJumpNumJumps = 1;
    public Vector3 frontJumpRotationEuler;
    public float frontJumpExtraSpinY = 360f;

    public float destroyDuration = 0.22f;
    public float destroyDelay = 0f;
    public float destroySpinY = 360f;
    public float destroyMoveUp = 0.15f;

    private Coroutine moveRoutine;
    private Coroutine shootRoutine;

    private bool canShoot;
    private bool isMoving;

    private HashSet<int> lockedDepthLines = new HashSet<int>();

    private Quaternion rotationTarget;

    private void Awake()
    {
        shotsRemaining = shotsTotal;
        rotationTarget = transform.rotation;
        UpdateShotsText();
    }

    public void ApplyShots(int shots)
    {
        if (shots < 0)
        {
            shots = 0;
        }

        shotsTotal = shots;
        shotsRemaining = shotsTotal;
        UpdateShotsText();
    }

    public void StartMoveOnSpline(SplinePathDefinition splinePath, System.Action onSplineFinished)
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

        if (splinePath == null || splinePath.splineContainer == null)
        {
            return;
        }

        IsBusy = true;

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }

        moveRoutine = StartCoroutine(MoveAlongSpline(splinePath, onSplineFinished));
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

    private IEnumerator MoveAlongSpline(SplinePathDefinition splinePath, System.Action onSplineFinished)
    {
        canShoot = false;
        isMoving = false;

        lockedDepthLines.Clear();

        if (shootRoutine != null)
        {
            StopCoroutine(shootRoutine);
        }

        shootRoutine = StartCoroutine(ShootLoop_LineLockedDepth_Bullet());

        SplineContainer container = splinePath.splineContainer;

        if (container == null)
        {
            StopShooting();
            IsBusy = false;

            if (onSplineFinished != null)
            {
                onSplineFinished();
            }

            yield break;
        }

        if (container.Splines == null || container.Splines.Count == 0)
        {
            StopShooting();
            IsBusy = false;

            if (onSplineFinished != null)
            {
                onSplineFinished();
            }

            yield break;
        }

        int index = splinePath.splineIndex;

        if (index < 0)
        {
            index = 0;
        }

        if (index >= container.Splines.Count)
        {
            index = container.Splines.Count - 1;
        }

        Spline spline = container.Splines[index];

        float4x4 splineMatrix = GetSplineMatrix(container.transform);

        float3 startLocal = SplineUtility.EvaluatePosition(spline, 0f);
        Vector3 startWorld = LocalToWorldPoint(container.transform, startLocal);

        bool jumpFinished = false;

        DOTween.Kill(transform);

        rotationTarget = Quaternion.Euler(toSplineJumpRotationEuler);

        Vector3 jumpRotateTargetEuler = toSplineJumpRotationEuler + new Vector3(0f, toSplineJumpExtraSpinY, 0f);

        Sequence startSeq = DOTween.Sequence();
        startSeq.Join(transform.DOJump(startWorld, toSplineJumpPower, toSplineJumpNumJumps, toSplineJumpDuration).SetEase(Ease.OutQuad));
        startSeq.Join(transform.DORotate(jumpRotateTargetEuler, toSplineJumpDuration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
        startSeq.OnComplete(() =>
        {
            jumpFinished = true;
        });

        isMoving = true;

        while (!jumpFinished)
        {
            if (!IsAlive)
            {
                yield break;
            }

            StepRotation();
            yield return null;
        }

        transform.position = startWorld;
        transform.rotation = Quaternion.Euler(toSplineJumpRotationEuler);

        isMoving = false;
        canShoot = true;

        float splineLength = SplineUtility.CalculateLength(spline, splineMatrix);

        if (splineLength <= 0.0001f)
        {
            isMoving = false;
            StopShooting();
            IsBusy = false;

            if (onSplineFinished != null)
            {
                onSplineFinished();
            }

            yield break;
        }

        float t = 0f;

        while (t < 1f)
        {
            if (!IsAlive)
            {
                yield break;
            }

            isMoving = true;

            float dt = (splinePath.moveSpeed / splineLength) * Time.deltaTime;
            t += dt;

            if (t > 1f)
            {
                t = 1f;
            }

            float3 posLocal = SplineUtility.EvaluatePosition(spline, t);
            Vector3 posWorld = LocalToWorldPoint(container.transform, posLocal);

            transform.position = posWorld;

            float tRot = t + splineRotationLookAheadT;
            if (tRot > 1f)
            {
                tRot = 1f;
            }

            float3 tanLocal = SplineUtility.EvaluateTangent(spline, tRot);
            float3 upLocal = SplineUtility.EvaluateUpVector(spline, tRot);

            Vector3 tanWorld = LocalToWorldDir(container.transform, tanLocal);
            Vector3 upWorld = LocalToWorldDir(container.transform, upLocal);

            Quaternion targetRotation;

            if (useSplineTangentRotation)
            {
                Vector3 forwardDir = tanWorld.sqrMagnitude > 0.000001f ? tanWorld.normalized : transform.forward;

                if (invertSplineTangent)
                {
                    forwardDir = -forwardDir;
                }

                Vector3 upDir = upWorld.sqrMagnitude > 0.000001f ? upWorld.normalized : Vector3.up;

                targetRotation = Quaternion.LookRotation(forwardDir, upDir) * Quaternion.Euler(splineRotationOffsetEuler);
            }
            else
            {
                targetRotation = Quaternion.Euler(splineFixedRotationEuler);
            }

            rotationTarget = targetRotation;
            StepRotation();

            yield return null;
        }

        isMoving = false;

        StopShooting();

        IsBusy = false;

        if (onSplineFinished != null)
        {
            onSplineFinished();
        }
    }

    private float4x4 GetSplineMatrix(Transform tr)
    {
        return float4x4.TRS(tr.position, tr.rotation, tr.lossyScale);
    }

    private Vector3 LocalToWorldPoint(Transform tr, float3 local)
    {
        return tr.TransformPoint(new Vector3(local.x, local.y, local.z));
    }

    private Vector3 LocalToWorldDir(Transform tr, float3 localDir)
    {
        Vector3 v = new Vector3(localDir.x, localDir.y, localDir.z);
        Vector3 w = tr.TransformDirection(v);
        return w;
    }

    private void StepRotation()
    {
        float maxDegrees = rotationSpeedDegPerSec * Time.deltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotationTarget, maxDegrees);
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

        Vector3 startPos = t.position;
        Vector3 endPos = startPos + Vector3.up * destroyMoveUp;

        Sequence seq = DOTween.Sequence();

        seq.Join(t.DOMove(endPos, destroyDuration).SetEase(Ease.OutQuad));
        seq.Join(t.DOScale(Vector3.zero, destroyDuration).SetEase(Ease.InBack));
        seq.Join(t.DORotate(new Vector3(0f, destroySpinY, 0f), destroyDuration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));

        seq.SetDelay(destroyDelay);

        seq.OnComplete(() =>
        {
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        });
    }
}