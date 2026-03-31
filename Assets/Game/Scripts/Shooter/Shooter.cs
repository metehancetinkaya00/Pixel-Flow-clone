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
    private Vector3 formationOffset;
    public Vector3 FormationOffset => formationOffset;

    [Header("Visual")]
    public Color linkColor = Color.white;
    public BlockColor shooterColor;
    public int linkGroupId = 0;

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
    private bool destroyWhenNoPending;

    private readonly HashSet<int> lockedDepthLines = new HashSet<int>();
    private readonly Dictionary<int, Block> pendingTargets = new Dictionary<int, Block>();

    private Quaternion rotationTarget;

    private void Awake()
    {
        shotsRemaining = shotsTotal;
        rotationTarget = transform.rotation;
        formationOffset = Vector3.zero;
        UpdateShotsText();
    }

    public void ApplyShots(int shots)
    {
        shotsTotal = Mathf.Max(0, shots);
        shotsRemaining = shotsTotal;
        UpdateShotsText();
    }

    public void StartMoveOnSpline(SplinePathDefinition splinePath, System.Action onFinished)
    {
        StartMoveOnSpline(splinePath, Vector3.zero, null, onFinished);
    }

    public void StartMoveOnSpline(SplinePathDefinition splinePath, Vector3 offset, System.Action onFinished)
    {
        StartMoveOnSpline(splinePath, offset, null, onFinished);
    }

    public void StartMoveOnSpline(SplinePathDefinition splinePath, Vector3 offset, System.Action onReachedSplineStart, System.Action onFinished)
    {
        if (!IsAlive || IsBusy)
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

        formationOffset = offset;
        IsBusy = true;

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }

        moveRoutine = StartCoroutine(MoveAlongSpline(splinePath, onReachedSplineStart, onFinished));
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
            onFinished?.Invoke();
        });
    }

    public void ShiftToFrontSlot(Vector3 targetPosition, System.Action onFinished)
    {
        if (!IsAlive)
        {
            return;
        }

        IsBusy = true;
        DOTween.Kill(transform);

        Quaternion currentRotation = transform.rotation;

        transform.DOJump(targetPosition, frontJumpPower, frontJumpNumJumps, frontJumpDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                if (!IsAlive)
                {
                    return;
                }

                transform.position = targetPosition;
                transform.rotation = currentRotation;
                IsBusy = false;
                onFinished?.Invoke();
            });
    }

    private IEnumerator MoveAlongSpline(SplinePathDefinition splinePath, System.Action onReachedSplineStart, System.Action onFinished)
    {
        canShoot = false;
        isMoving = false;
        destroyWhenNoPending = false;

        lockedDepthLines.Clear();
        ReleaseAllPendingTargets();

        StopShooting();
        shootRoutine = StartCoroutine(ShootLoop());

        SplineContainer container = splinePath.splineContainer;
        if (container == null || container.Splines == null || container.Splines.Count == 0)
        {
            StopShooting();
            IsBusy = false;
            onFinished?.Invoke();
            yield break;
        }

        int index = Mathf.Clamp(splinePath.splineIndex, 0, container.Splines.Count - 1);
        Spline spline = container.Splines[index];
        Transform root = container.transform;

        Vector3 scale = root.lossyScale;
        float4x4 matrix = float4x4.TRS(root.position, root.rotation, new float3(scale.x, scale.y, scale.z));
        float splineLength = SplineUtility.CalculateLength(spline, matrix);

        float lookAhead = Mathf.Min(1f, splineRotationLookAheadT);
        bool hasOffset = formationOffset.sqrMagnitude > 0.000001f;

        Quaternion jumpEndRotation = Quaternion.Euler(toSplineJumpRotationEuler);
        Quaternion fixedRotation = Quaternion.Euler(splineFixedRotationEuler);
        Quaternion splineOffsetRotation = Quaternion.Euler(splineRotationOffsetEuler);

        float3 startLocal = SplineUtility.EvaluatePosition(spline, 0f);
        Vector3 startWorld = root.TransformPoint(new Vector3(startLocal.x, startLocal.y, startLocal.z));

        if (hasOffset)
        {
            float3 startTanLocal = SplineUtility.EvaluateTangent(spline, lookAhead);
            float3 startUpLocal = SplineUtility.EvaluateUpVector(spline, lookAhead);

            Vector3 startTanWorld = root.TransformDirection(new Vector3(startTanLocal.x, startTanLocal.y, startTanLocal.z));
            Vector3 startUpWorld = root.TransformDirection(new Vector3(startUpLocal.x, startUpLocal.y, startUpLocal.z));

            Vector3 startForward = startTanWorld.sqrMagnitude > 0.000001f ? startTanWorld.normalized : Vector3.forward;
            Vector3 startUp = startUpWorld.sqrMagnitude > 0.000001f ? startUpWorld.normalized : Vector3.up;
            Vector3 startRight = Vector3.Cross(startUp, startForward).normalized;

            startWorld += (startRight * formationOffset.x) + (startUp * formationOffset.y) + (startForward * formationOffset.z);
        }

        bool jumpFinished = false;

        DOTween.Kill(transform);

        Vector3 jumpRotateTargetEuler = toSplineJumpRotationEuler + new Vector3(0f, toSplineJumpExtraSpinY, 0f);

        Sequence startSeq = DOTween.Sequence();
        startSeq.Join(transform.DOJump(startWorld, toSplineJumpPower, toSplineJumpNumJumps, toSplineJumpDuration).SetEase(Ease.OutQuad));
        startSeq.Join(transform.DORotate(jumpRotateTargetEuler, toSplineJumpDuration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
        startSeq.OnComplete(() => jumpFinished = true);

        while (!jumpFinished)
        {
            if (!IsAlive)
            {
                yield break;
            }

            yield return null;
        }

        transform.position = startWorld;
        transform.rotation = jumpEndRotation;
        onReachedSplineStart?.Invoke();
        canShoot = true;

        if (splineLength <= 0.0001f)
        {
            StopShooting();
            IsBusy = false;
            onFinished?.Invoke();
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

            t += (splinePath.moveSpeed / splineLength) * Time.deltaTime;
            if (t > 1f)
            {
                t = 1f;
            }

            float3 posLocal = SplineUtility.EvaluatePosition(spline, t);
            Vector3 basePosWorld = root.TransformPoint(new Vector3(posLocal.x, posLocal.y, posLocal.z));

            Vector3 forwardDir = transform.forward;
            Vector3 upDir = Vector3.up;
            Vector3 posWorld = basePosWorld;

            if (hasOffset || useSplineTangentRotation)
            {
                float tRot = Mathf.Min(1f, t + splineRotationLookAheadT);

                float3 tanLocal = SplineUtility.EvaluateTangent(spline, tRot);
                float3 upLocal = SplineUtility.EvaluateUpVector(spline, tRot);

                Vector3 tanWorld = root.TransformDirection(new Vector3(tanLocal.x, tanLocal.y, tanLocal.z));
                Vector3 upWorld = root.TransformDirection(new Vector3(upLocal.x, upLocal.y, upLocal.z));

                forwardDir = tanWorld.sqrMagnitude > 0.000001f ? tanWorld.normalized : transform.forward;
                upDir = upWorld.sqrMagnitude > 0.000001f ? upWorld.normalized : Vector3.up;

                if (hasOffset)
                {
                    Vector3 rightDir = Vector3.Cross(upDir, forwardDir).normalized;
                    posWorld += (rightDir * formationOffset.x) + (upDir * formationOffset.y) + (forwardDir * formationOffset.z);
                }
            }

            transform.position = posWorld;

            if (useSplineTangentRotation)
            {
                Vector3 f = invertSplineTangent ? -forwardDir : forwardDir;
                rotationTarget = Quaternion.LookRotation(f, upDir) * splineOffsetRotation;
            }
            else
            {
                rotationTarget = fixedRotation;
            }

            StepRotation();
            yield return null;
        }

        isMoving = false;
        StopShooting();
        IsBusy = false;
        onFinished?.Invoke();
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

    public void OnBulletResolved(int lineKey, bool success)
    {
        if (!IsAlive)
        {
            return;
        }

        if (pendingTargets.TryGetValue(lineKey, out Block block))
        {
            pendingTargets.Remove(lineKey);

            if (!success && block != null && !block.IsDying)
            {
                block.IsTargeted = false;
            }
        }

        if (success)
        {
            lockedDepthLines.Add(lineKey);
        }

        if (destroyWhenNoPending && pendingTargets.Count == 0)
        {
            DestroySelf();
        }
    }

    private IEnumerator ShootLoop()
    {
        while (true)
        {
            if (!IsAlive)
            {
                yield break;
            }

            if (shotsRemaining <= 0)
            {
                destroyWhenNoPending = true;

                if (pendingTargets.Count == 0)
                {
                    DestroySelf();
                    yield break;
                }

                yield return null;
                continue;
            }

            if (canShoot && isMoving && BlockGridManager.Instance != null)
            {
                int side;
                int lineIndex;

                if (BlockGridManager.Instance.TryResolveShooterLine(transform.position, out side, out lineIndex))
                {
                    int lineKey = BlockGridManager.Instance.BuildLineKey(side, lineIndex);

                    if (!lockedDepthLines.Contains(lineKey) && !pendingTargets.ContainsKey(lineKey))
                    {
                        Block target;

                        if (BlockGridManager.Instance.TryReserveTargetByLine(shooterColor, side, lineIndex, out target))
                        {
                            if (FireBullet(target, lineKey))
                            {
                                pendingTargets[lineKey] = target;
                            }
                            else if (target != null && !target.IsDying)
                            {
                                target.IsTargeted = false;
                            }
                        }
                    }
                }
            }

            yield return new WaitForSeconds(bulletFireCooldown);
        }
    }

    private bool FireBullet(Block targetBlock, int lineKey)
    {
        if (shotsRemaining <= 0 || bulletPrefab == null || targetBlock == null)
        {
            return false;
        }

        Vector3 spawnPos = bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position;
        Quaternion spawnRot = bulletSpawnPoint != null ? bulletSpawnPoint.rotation : transform.rotation;

        GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, spawnRot);
        Bullet bulletScript = bulletObj.GetComponent<Bullet>();

        if (bulletScript == null)
        {
            Destroy(bulletObj);
            return false;
        }

        shotsRemaining--;
        UpdateShotsText();

        if (shotsRemaining <= 0)
        {
            destroyWhenNoPending = true;
        }

        bulletScript.Init(this, lineKey, targetBlock);
        return true;
    }

    private void UpdateShotsText()
    {
        if (shotsText != null)
        {
            shotsText.text = shotsRemaining.ToString();
        }
    }

    private void ReleaseAllPendingTargets()
    {
        foreach (var kv in pendingTargets)
        {
            Block block = kv.Value;
            if (block != null && !block.IsDying)
            {
                block.IsTargeted = false;
            }
        }

        pendingTargets.Clear();
    }

    public void DestroySelf()
    {
        if (!IsAlive)
        {
            return;
        }

        ReleaseAllPendingTargets();
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

        Vector3 endPos = t.position + Vector3.up * destroyMoveUp;

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