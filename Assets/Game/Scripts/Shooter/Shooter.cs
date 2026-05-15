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
    [Header("Identity")]
    public BlockColor shooterColor;
    public Color linkColor = Color.white;
    public int linkGroupId = 0;

    [Header("Shots")]
    public int shotsTotal = 5;
    public TMP_Text shotsText;
    public float bulletFireCooldown = 0.15f;
    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;

    [Header("Fire Bounce")]
    public Transform fireBounceTarget;
    public Vector3 fireBounceLocalOffset = new Vector3(0f, 0f, -0.08f);
    public float fireBounceDuration = 0.12f;

    [Header("Rotation")]
    public float rotationSpeedDegPerSec = 720f;

    [Header("Jump to Spline")]
    public float toSplineJumpDuration = 0.35f;
    public float toSplineJumpPower = 1.2f;
    public int toSplineJumpNumJumps = 1;
    public Vector3 toSplineJumpRotationEuler;
    public float toSplineJumpExtraSpinY = 360f;

    [Header("Spline Rotation")]
    public bool useSplineTangentRotation = true;
    public bool invertSplineTangent = false;
    public Vector3 splineRotationOffsetEuler;
    public Vector3 splineFixedRotationEuler;
    public float splineRotationLookAheadT = 0.03f;

    [Header("Jump to Front Slot")]
    public float frontJumpDuration = 0.35f;
    public float frontJumpPower = 1.2f;
    public int frontJumpNumJumps = 1;
    public Vector3 frontJumpRotationEuler;
    public float frontJumpExtraSpinY = 360f;

    [Header("Destroy FX")]
    public float destroyDuration = 0.22f;
    public float destroyDelay = 0f;
    public float destroyMoveUp = 0.15f;

    public bool IsBusy { get; private set; }
    public bool IsAlive { get; private set; } = true;
    public int shotsRemaining { get; private set; }
    public Vector3 FormationOffset => formationOffset;

    private Vector3 formationOffset;
    private bool keepFormationOffsetOnSpline;

    private Coroutine moveRoutine;
    private Coroutine shootRoutine;

    private bool canShoot;
    private bool isMoving;
    private bool destroyWhenNoPending;

    private readonly HashSet<int> lockedLines = new HashSet<int>();
    private readonly Dictionary<int, Block> pendingTargets = new Dictionary<int, Block>();

    private Quaternion rotationTarget;
    private Vector3 fireBounceStartLocalPos;

    private void Awake()
    {
        shotsRemaining = shotsTotal;
        rotationTarget = transform.rotation;
        formationOffset = Vector3.zero;
        keepFormationOffsetOnSpline = true;

        if (fireBounceTarget != null)
            fireBounceStartLocalPos = fireBounceTarget.localPosition;

        UpdateShotsText();
    }

    public void ApplyShots(int shots)
    {
        shotsTotal = Mathf.Max(0, shots);
        shotsRemaining = shotsTotal;
        UpdateShotsText();
    }

 

    public void StartMoveOnSpline(SplinePathDefinition path, System.Action onFinished) =>
        StartMoveOnSpline(path, Vector3.zero, keepOffset: true, onSplineReached: null, onFinished);

    public void StartMoveOnSpline(SplinePathDefinition path, Vector3 offset, System.Action onFinished) =>
        StartMoveOnSpline(path, offset, keepOffset: true, onSplineReached: null, onFinished);

    public void StartMoveOnSpline(SplinePathDefinition path, Vector3 offset, System.Action onSplineReached, System.Action onFinished) =>
        StartMoveOnSpline(path, offset, keepOffset: true, onSplineReached, onFinished);

    public void StartMoveOnSpline(
        SplinePathDefinition path,
        Vector3 offset,
        bool keepOffset,
        System.Action onSplineReached,
        System.Action onFinished)
    {
        if (!IsAlive || IsBusy) return;

        if (shotsRemaining <= 0) { DestroySelf(); return; }

        if (path?.splineContainer == null) return;

        formationOffset = offset;
        keepFormationOffsetOnSpline = keepOffset;
        IsBusy = true;

        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveAlongSpline(path, onSplineReached, onFinished));
    }

   
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
                transform.SetPositionAndRotation(targetPos, Quaternion.Euler(frontJumpRotationEuler));
                IsBusy = false;
                onFinished?.Invoke();
            });
    }

    
    public void ShiftToFrontSlot(Vector3 targetPos, System.Action onFinished)
    {
        if (!IsAlive) return;

        IsBusy = true;
        DOTween.Kill(transform);

        Quaternion savedRotation = transform.rotation;

        transform.DOJump(targetPos, frontJumpPower, frontJumpNumJumps, frontJumpDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                if (!IsAlive) return;
                transform.SetPositionAndRotation(targetPos, savedRotation);
                IsBusy = false;
                onFinished?.Invoke();
            });
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

        if (success) lockedLines.Add(lineKey);

        if (destroyWhenNoPending && pendingTargets.Count == 0)
            DestroySelf();
    }



    public void DestroySelf()
    {
        if (!IsAlive) return;

        IsAlive = false;
        IsBusy = false;

        ReleaseAllPendingTargets();

        if (moveRoutine != null) { StopCoroutine(moveRoutine); moveRoutine = null; }
        StopShooting();

        ShooterQueueManager.Instance?.NotifyShooterDestroyed(this);
        DOTween.Kill(transform);

        DOTween.Sequence()
            .SetDelay(destroyDelay)
            .Join(transform.DOMove(transform.position + Vector3.up * destroyMoveUp, destroyDuration).SetEase(Ease.OutQuad))
            .Join(transform.DOScale(Vector3.zero, destroyDuration).SetEase(Ease.InBack))
            .OnComplete(() => Destroy(gameObject));
    }

   

    private IEnumerator MoveAlongSpline(SplinePathDefinition path, System.Action onSplineReached, System.Action onFinished)
    {
        canShoot = false;
        isMoving = false;
        destroyWhenNoPending = false;

        lockedLines.Clear();
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
        float4x4 localToWorld = float4x4.TRS(root.position, root.rotation, new float3(scale.x, scale.y, scale.z));
        float splineLength = SplineUtility.CalculateLength(spline, localToWorld);

        float lookAhead = Mathf.Min(1f, splineRotationLookAheadT);
        bool hasEntryOffset = formationOffset.sqrMagnitude > 0.000001f;
        bool hasSplineOffset = keepFormationOffsetOnSpline && hasEntryOffset;

        Quaternion jumpEndRotation = Quaternion.Euler(toSplineJumpRotationEuler);
        Quaternion fixedRotation = Quaternion.Euler(splineFixedRotationEuler);
        Quaternion splineOffsetRot = Quaternion.Euler(splineRotationOffsetEuler);

        Vector3 startWorld = EvaluateSplineWorldPos(spline, root, 0f);

        if (hasEntryOffset)
            startWorld += CalculateFormationWorldOffset(spline, root, lookAhead, formationOffset);

      
        bool jumpDone = false;
        DOTween.Kill(transform);
        transform.DOJump(startWorld, toSplineJumpPower, toSplineJumpNumJumps, toSplineJumpDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => jumpDone = true);

        while (!jumpDone)
        {
            if (!IsAlive) yield break;
            yield return null;
        }

        transform.SetPositionAndRotation(startWorld, jumpEndRotation);
        onSplineReached?.Invoke();
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

            Vector3 posWorld = EvaluateSplineWorldPos(spline, root, t);
            Vector3 forwardDir = transform.forward;
            Vector3 upDir = Vector3.up;

            if (hasSplineOffset || useSplineTangentRotation)
            {
                float tLook = Mathf.Min(1f, t + splineRotationLookAheadT);
                GetSplineTangentAndUp(spline, root, tLook, out forwardDir, out upDir);

                if (hasSplineOffset)
                {
                    Vector3 right = Vector3.Cross(upDir, forwardDir).normalized;
                    posWorld += right * formationOffset.x + upDir * formationOffset.y + forwardDir * formationOffset.z;
                }
            }

            transform.position = posWorld;

            rotationTarget = useSplineTangentRotation
                ? Quaternion.LookRotation(invertSplineTangent ? -forwardDir : forwardDir, upDir) * splineOffsetRot
                : fixedRotation;

            StepRotation();
            yield return null;
        }

        isMoving = false;
        StopShooting();
        IsBusy = false;
        onFinished?.Invoke();
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

            TryFireAtCurrentLine();

            yield return new WaitForSeconds(bulletFireCooldown);
        }
    }

    private void TryFireAtCurrentLine()
    {
        if (!canShoot || !isMoving || BlockGridManager.Instance == null) return;

        if (!BlockGridManager.Instance.TryResolveShooterLine(transform.position, out int side, out int lineIndex))
            return;

        int lineKey = BlockGridManager.Instance.BuildLineKey(side, lineIndex);

        if (lockedLines.Contains(lineKey) || pendingTargets.ContainsKey(lineKey)) return;

        if (!BlockGridManager.Instance.TryReserveTargetByLine(shooterColor, side, lineIndex, out Block target))
            return;

        if (FireBullet(target, lineKey))
            pendingTargets[lineKey] = target;
        else if (target != null && !target.IsDying)
            target.IsTargeted = false;
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

    private void PlayFireBounce()
    {
        if (fireBounceTarget == null) return;

        DOTween.Kill(fireBounceTarget);
        fireBounceTarget.localPosition = fireBounceStartLocalPos;

        DOTween.Sequence()
            .Append(fireBounceTarget.DOLocalMove(fireBounceStartLocalPos + fireBounceLocalOffset, fireBounceDuration * 0.35f).SetEase(Ease.OutQuad))
            .Append(fireBounceTarget.DOLocalMove(fireBounceStartLocalPos, fireBounceDuration * 0.65f).SetEase(Ease.OutBack));
    }

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

    private static Vector3 EvaluateSplineWorldPos(Spline spline, Transform root, float t)
    {
        float3 local = SplineUtility.EvaluatePosition(spline, t);
        return root.TransformPoint(new Vector3(local.x, local.y, local.z));
    }

  
    private static void GetSplineTangentAndUp(Spline spline, Transform root, float t, out Vector3 forward, out Vector3 up)
    {
        float3 tanLocal = SplineUtility.EvaluateTangent(spline, t);
        float3 upLocal = SplineUtility.EvaluateUpVector(spline, t);

        Vector3 tanWorld = root.TransformDirection(new Vector3(tanLocal.x, tanLocal.y, tanLocal.z));
        Vector3 upWorld = root.TransformDirection(new Vector3(upLocal.x, upLocal.y, upLocal.z));

        forward = tanWorld.sqrMagnitude > 0.000001f ? tanWorld.normalized : Vector3.forward;
        up = upWorld.sqrMagnitude > 0.000001f ? upWorld.normalized : Vector3.up;
    }


    private static Vector3 CalculateFormationWorldOffset(Spline spline, Transform root, float lookAhead, Vector3 offset)
    {
        GetSplineTangentAndUp(spline, root, lookAhead, out Vector3 forward, out Vector3 up);
        Vector3 right = Vector3.Cross(up, forward).normalized;
        return right * offset.x + up * offset.y + forward * offset.z;
    }
}
