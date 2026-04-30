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
    private bool keepFormationOffsetOnSpline;
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
    public Transform fireBounceTarget;
    public Vector3 fireBounceLocalOffset = new Vector3(0f, 0f, -0.08f);
    public float fireBounceDuration = 0.12f;


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

    // ---------------------------------------------------------------
    private Coroutine moveRoutine;
    private Coroutine shootRoutine;

    private bool canShoot;
    private bool isMoving;
    private bool destroyWhenNoPending;

    private readonly HashSet<int> lockedDepthLines = new HashSet<int>();
    private readonly Dictionary<int, Block> pendingTargets = new Dictionary<int, Block>();

    private Quaternion rotationTarget;
    // ---------------------------------------------------------------
    private Vector3 fireBounceStartLocalPosition;
    private void Awake()
    {
        shotsRemaining = shotsTotal;
        rotationTarget = transform.rotation;
        formationOffset = Vector3.zero;
        keepFormationOffsetOnSpline = true;

        if (fireBounceTarget != null)
        {
            fireBounceStartLocalPosition = fireBounceTarget.localPosition;
        }

        UpdateShotsText();
    }

    public void ApplyShots(int shots)
    {
        shotsTotal = Mathf.Max(0, shots);
        shotsRemaining = shotsTotal;
        UpdateShotsText();
    }

    // ---------------------------------------------------------------
 

    public void StartMoveOnSpline(SplinePathDefinition path, System.Action onFinished) =>
        StartMoveOnSpline(path, Vector3.zero, true, null, onFinished);

    public void StartMoveOnSpline(SplinePathDefinition path, Vector3 offset, System.Action onFinished) =>
        StartMoveOnSpline(path, offset, true, null, onFinished);

    public void StartMoveOnSpline(SplinePathDefinition path, Vector3 offset, System.Action onReachedStart, System.Action onFinished) =>
        StartMoveOnSpline(path, offset, true, onReachedStart, onFinished);

    public void StartMoveOnSpline(
        SplinePathDefinition path,
        Vector3 offset,
        bool keepOffsetOnSpline,
        System.Action onReachedStart,
        System.Action onFinished)
    {
        if (!IsAlive || IsBusy) return;

        if (shotsRemaining <= 0) { DestroySelf(); return; }

        if (path?.splineContainer == null) return;

        formationOffset = offset;
        keepFormationOffsetOnSpline = keepOffsetOnSpline;
        IsBusy = true;

        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveAlongSpline(path, onReachedStart, onFinished));
    }

    // ---------------------------------------------------------------


    public void JumpToFrontSlot(Vector3 targetPos, System.Action onFinished)
    {
        if (!IsAlive) return;

        IsBusy = true;
        DOTween.Kill(transform);

        Vector3 spinTarget = frontJumpRotationEuler + new Vector3(0f, frontJumpExtraSpinY, 0f);

        DOTween.Sequence()
            .Join(transform.DOJump(targetPos, frontJumpPower, frontJumpNumJumps, frontJumpDuration).SetEase(Ease.OutQuad))
            .Join(transform.DORotate(spinTarget, frontJumpDuration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad))
            .OnComplete(() =>
            {
                if (!IsAlive) return;
                transform.position = targetPos;
                transform.rotation = Quaternion.Euler(frontJumpRotationEuler);
                IsBusy = false;
                onFinished?.Invoke();
            });
    }


    public void ShiftToFrontSlot(Vector3 targetPos, System.Action onFinished)
    {
        if (!IsAlive) return;

        IsBusy = true;
        DOTween.Kill(transform);

        Quaternion savedRot = transform.rotation;

        transform.DOJump(targetPos, frontJumpPower, frontJumpNumJumps, frontJumpDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                if (!IsAlive) return;
                transform.position = targetPos;
                transform.rotation = savedRot;
                IsBusy = false;
                onFinished?.Invoke();
            });
    }

    // ---------------------------------------------------------------
  

    private IEnumerator MoveAlongSpline(SplinePathDefinition path, System.Action onReachedStart, System.Action onFinished)
    {
        canShoot = false;
        isMoving = false;
        destroyWhenNoPending = false;

        lockedDepthLines.Clear();
        ReleaseAllPendingTargets();

        StopShooting();
        shootRoutine = StartCoroutine(ShootLoop());

        SplineContainer container = path.splineContainer;
        if (container?.Splines == null || container.Splines.Count == 0)
        {
            StopShooting();
            IsBusy = false;
            onFinished?.Invoke();
            yield break;
        }

        int splineIdx = Mathf.Clamp(path.splineIndex, 0, container.Splines.Count - 1);
        Spline spline = container.Splines[splineIdx];
        Transform root = container.transform;

        Vector3 scale = root.lossyScale;
        float4x4 mat = float4x4.TRS(root.position, root.rotation, new float3(scale.x, scale.y, scale.z));
        float splineLength = SplineUtility.CalculateLength(spline, mat);

        float lookAhead = Mathf.Min(1f, splineRotationLookAheadT);
        bool hasEntryOffset = formationOffset.sqrMagnitude > 0.000001f;
        bool hasSplineOffset = keepFormationOffsetOnSpline && hasEntryOffset;

        Quaternion jumpEndRot = Quaternion.Euler(toSplineJumpRotationEuler);
        Quaternion fixedRot = Quaternion.Euler(splineFixedRotationEuler);
        Quaternion splineOffsetRot = Quaternion.Euler(splineRotationOffsetEuler);


        float3 startLocal = SplineUtility.EvaluatePosition(spline, 0f);
        Vector3 startWorld = root.TransformPoint(new Vector3(startLocal.x, startLocal.y, startLocal.z));

        if (hasEntryOffset)
        {
            float3 tanLocal = SplineUtility.EvaluateTangent(spline, lookAhead);
            float3 upLocal = SplineUtility.EvaluateUpVector(spline, lookAhead);

            Vector3 fwd = root.TransformDirection(new Vector3(tanLocal.x, tanLocal.y, tanLocal.z));
            Vector3 up = root.TransformDirection(new Vector3(upLocal.x, upLocal.y, upLocal.z));

            fwd = fwd.sqrMagnitude > 0.000001f ? fwd.normalized : Vector3.forward;
            up = up.sqrMagnitude > 0.000001f ? up.normalized : Vector3.up;

            Vector3 right = Vector3.Cross(up, fwd).normalized;
            startWorld += right * formationOffset.x + up * formationOffset.y + fwd * formationOffset.z;
        }

      
        bool jumpDone = false;

        DOTween.Kill(transform);

        Sequence startSeq = DOTween.Sequence();
        startSeq.Append(transform.DOJump(startWorld, toSplineJumpPower, toSplineJumpNumJumps, toSplineJumpDuration).SetEase(Ease.OutQuad));
        startSeq.OnComplete(() => jumpDone = true);

        while (!jumpDone)
        {
            if (!IsAlive) yield break;
            yield return null;
        }

        transform.position = startWorld;
        transform.rotation = jumpEndRot;
        onReachedStart?.Invoke();
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
            if (!IsAlive) yield break;

            isMoving = true;
            t = Mathf.Min(1f, t + (path.moveSpeed / splineLength) * Time.deltaTime);

            float3 posLocal = SplineUtility.EvaluatePosition(spline, t);
            Vector3 posWorld = root.TransformPoint(new Vector3(posLocal.x, posLocal.y, posLocal.z));
            Vector3 forwardDir = transform.forward;
            Vector3 upDir = Vector3.up;

            if (hasSplineOffset || useSplineTangentRotation)
            {
                float tRot = Mathf.Min(1f, t + splineRotationLookAheadT);
                float3 tanLocal = SplineUtility.EvaluateTangent(spline, tRot);
                float3 upLocal = SplineUtility.EvaluateUpVector(spline, tRot);

                Vector3 tanWorld = root.TransformDirection(new Vector3(tanLocal.x, tanLocal.y, tanLocal.z));
                Vector3 upWorld = root.TransformDirection(new Vector3(upLocal.x, upLocal.y, upLocal.z));

                forwardDir = tanWorld.sqrMagnitude > 0.000001f ? tanWorld.normalized : transform.forward;
                upDir = upWorld.sqrMagnitude > 0.000001f ? upWorld.normalized : Vector3.up;

                if (hasSplineOffset)
                {
                    Vector3 right = Vector3.Cross(upDir, forwardDir).normalized;
                    posWorld += right * formationOffset.x + upDir * formationOffset.y + forwardDir * formationOffset.z;
                }
            }

            transform.position = posWorld;

            rotationTarget = useSplineTangentRotation
                ? Quaternion.LookRotation(invertSplineTangent ? -forwardDir : forwardDir, upDir) * splineOffsetRot
                : fixedRot;

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
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            rotationTarget,
            rotationSpeedDegPerSec * Time.deltaTime);
    }

    private void StopShooting()
    {
        if (shootRoutine == null) return;
        StopCoroutine(shootRoutine);
        shootRoutine = null;
    }

    // ---------------------------------------------------------------
    private void PlayFireBounce()
    {
        if (fireBounceTarget == null)
        {
            return;
        }

        DOTween.Kill(fireBounceTarget);

        fireBounceTarget.localPosition = fireBounceStartLocalPosition;

        Sequence seq = DOTween.Sequence();
        seq.Append(fireBounceTarget.DOLocalMove(fireBounceStartLocalPosition + fireBounceLocalOffset, fireBounceDuration * 0.35f).SetEase(Ease.OutQuad));
        seq.Append(fireBounceTarget.DOLocalMove(fireBounceStartLocalPosition, fireBounceDuration * 0.65f).SetEase(Ease.OutBack));
    }
    
    private IEnumerator ShootLoop()
    {
        while (true)
        {
            if (!IsAlive) yield break;

            if (shotsRemaining <= 0)
            {
                destroyWhenNoPending = true;
                if (pendingTargets.Count == 0) { DestroySelf(); yield break; }
                yield return null;
                continue;
            }

            if (canShoot && isMoving && BlockGridManager.Instance != null)
            {
                if (BlockGridManager.Instance.TryResolveShooterLine(transform.position, out int side, out int lineIndex))
                {
                    int lineKey = BlockGridManager.Instance.BuildLineKey(side, lineIndex);

                    if (!lockedDepthLines.Contains(lineKey) && !pendingTargets.ContainsKey(lineKey))
                    {
                        if (BlockGridManager.Instance.TryReserveTargetByLine(shooterColor, side, lineIndex, out Block target))
                        {
                            if (FireBullet(target, lineKey))
                                pendingTargets[lineKey] = target;
                            else if (target != null && !target.IsDying)
                                target.IsTargeted = false;
                        }
                    }
                }
            }

            yield return new WaitForSeconds(bulletFireCooldown);
        }
    }

    private bool FireBullet(Block target, int lineKey)
    {
        if (shotsRemaining <= 0 || bulletPrefab == null || target == null) return false;

        Vector3 spawnPos = bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position;
        Quaternion spawnRot = bulletSpawnPoint != null ? bulletSpawnPoint.rotation : transform.rotation;

        GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, spawnRot);
        Bullet bullet = bulletObj.GetComponent<Bullet>();

        if (bullet == null) { Destroy(bulletObj); return false; }

        shotsRemaining--;
        UpdateShotsText();

        if (shotsRemaining <= 0) destroyWhenNoPending = true;

        bullet.Init(this, lineKey, target);
        PlayFireBounce();
        return true;
    }


    public void OnBulletResolved(int lineKey, bool success)
    {
        if (!IsAlive) return;

        if (pendingTargets.TryGetValue(lineKey, out Block block))
        {
            pendingTargets.Remove(lineKey);
            if (!success && block != null && !block.IsDying)
                block.IsTargeted = false;
        }

        if (success) lockedDepthLines.Add(lineKey);

        if (destroyWhenNoPending && pendingTargets.Count == 0)
            DestroySelf();
    }

    // ---------------------------------------------------------------
 

    public void DestroySelf()
    {
        if (!IsAlive) return;

        ReleaseAllPendingTargets();
        IsAlive = false;

        if (moveRoutine != null) { StopCoroutine(moveRoutine); moveRoutine = null; }
        StopShooting();
        IsBusy = false;

        ShooterQueueManager.Instance?.NotifyShooterDestroyed(this);

        DOTween.Kill(transform);

        DOTween.Sequence()
            .SetDelay(destroyDelay)
            .Join(transform.DOMove(transform.position + Vector3.up * destroyMoveUp, destroyDuration).SetEase(Ease.OutQuad))
            .Join(transform.DOScale(Vector3.zero, destroyDuration).SetEase(Ease.InBack))
            .Join(transform.DORotate(new Vector3(0f, destroySpinY, 0f), destroyDuration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad))
            .OnComplete(() => { if (gameObject != null) Destroy(gameObject); });
    }

    // ---------------------------------------------------------------
    
    private void UpdateShotsText()
    {
        if (shotsText != null) shotsText.text = shotsRemaining.ToString();
    }

    private void ReleaseAllPendingTargets()
    {
        foreach (var kv in pendingTargets)
            if (kv.Value != null && !kv.Value.IsDying)
                kv.Value.IsTargeted = false;

        pendingTargets.Clear();
    }
}