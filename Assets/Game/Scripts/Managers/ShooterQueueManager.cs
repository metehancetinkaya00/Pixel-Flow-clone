using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.Splines;
using Unity.Mathematics;

public class ShooterQueueManager : MonoBehaviour
{
    public static ShooterQueueManager Instance;

    [Header("Queue")]
    public Transform runtimeQueueRoot;
    public Transform[] frontSlots;
    public SplinePathDefinition defaultSplinePath;
    public float queueMoveDuration = 0.15f;
    public float groupSideSpacing = 0.6f;
    public float groupLaunchDelay = 0.08f;

    [Header("Plates")]
    public GameObject platePrefab;
    public int plateCount = 4;
    public Transform plateLineRoot;
    public Vector3 plateLineOrigin = Vector3.zero;
    public Vector3 plateLineStep = new Vector3(0.8f, 0f, 0f);
    public Vector3 plateLineRotationEuler = Vector3.zero;
    public Vector3 plateChildLocalOffset = new Vector3(0f, -1f, 0f);
    public Vector3 plateChildLocalRotationEuler = new Vector3(0f, -1f, 0f);
    public float plateMoveDuration = 0.2f;
    public float plateAttachDuration = 0.1f;
    public Vector3 platePickupSpinEuler = new Vector3(0f, 360f, 0f);
    public Vector3 plateReturnSpinEuler = new Vector3(0f, 360f, 0f);

    [Header("Audio")]
    public AudioClip shooterClickClip;
    public AudioSource shooterClickSource;
    [Range(0f, 1f)] public float shooterClickVolume = 1f;

    // ---------------------------------------------------------------
    public struct SpawnedPlacement
    {
        public Shooter shooter;
        public int column;
        public int depth;
    }

    private struct QueueGroupMember { public Shooter shooter; public int column; public int depth; }
    private struct FrontGroupMember { public Shooter shooter; public int slotIndex; }

    // ---------------------------------------------------------------
    private Transform[][] slotMatrix;
    private int columns;
    private int depthCount;

    private Shooter[,] queueGrid;
    private Shooter[] frontShooters;
    private readonly Dictionary<Shooter, int> frontSlotLookup = new Dictionary<Shooter, int>();

    private readonly List<ShootersPlate> plateStack = new List<ShootersPlate>();
    private readonly Dictionary<Shooter, ShootersPlate> shooterPlateMap = new Dictionary<Shooter, ShootersPlate>();
    private readonly HashSet<ShootersPlate> returningPlates = new HashSet<ShootersPlate>();
    private Transform[] plateSlots;
    // ---------------------------------------------------------------

    private void Awake()
    {
        Instance = this;
        EnsureFrontSlots();
    }

    // ---------------------------------------------------------------
  

    public void ApplyLayout(QueueLayoutSettings settings)
    {
        columns = Mathf.Max(1, settings.columnCount);
        depthCount = Mathf.Max(1, settings.depthCount);

        if (runtimeQueueRoot == null)
            runtimeQueueRoot = new GameObject("QueueSlotsRuntime").transform;

        if (settings.createSlotObjects)
            ClearChildren(runtimeQueueRoot);

        slotMatrix = new Transform[columns][];

        for (int col = 0; col < columns; col++)
        {
            slotMatrix[col] = new Transform[depthCount];

            for (int dep = 0; dep < depthCount; dep++)
            {
                Vector3 pos = settings.origin + settings.columnStep * col + settings.depthStep * dep;

                GameObject slotObj;

                if (settings.createSlotObjects)
                {
                    slotObj = new GameObject($"Q_{col}_{dep}");
                    slotObj.transform.SetParent(runtimeQueueRoot);
                }
                else
                {
                    slotObj = new GameObject();
                    slotObj.hideFlags = HideFlags.HideAndDontSave;
                }

                slotObj.transform.position = pos;
                slotMatrix[col][dep] = slotObj.transform;
            }
        }

        queueGrid = new Shooter[columns, depthCount];
    }

    public Vector3 GetQueueSpawnPosition()
    {
        Transform slot = GetSlot(0, 0);
        return slot != null ? slot.position : Vector3.zero;
    }

    public void InitializeQueueFromPlacements(List<SpawnedPlacement> placements)
    {
        EnsureFrontSlots();

        if (queueGrid == null || slotMatrix == null) return;

     
        for (int col = 0; col < columns; col++)
            for (int dep = 0; dep < depthCount; dep++)
                queueGrid[col, dep] = null;

        for (int i = 0; i < frontShooters.Length; i++)
            frontShooters[i] = null;

        frontSlotLookup.Clear();
        RebuildPlateLine();

        if (placements == null) { SnapAll(); return; }

        foreach (var p in placements)
        {
            if (p.shooter != null && IsValidSlot(p.column, p.depth))
                queueGrid[p.column, p.depth] = p.shooter;
        }

        SnapAll();
    }

    // ---------------------------------------------------------------
 

    public void TryActivateShooter(Shooter clicked)
    {
        if (clicked == null || !clicked.IsAlive || clicked.IsBusy) return;
        if (defaultSplinePath?.splineContainer == null) return;

        bool inQueue = TryFindInQueue(clicked, out int column, out int depth);
        bool inFront = frontSlotLookup.ContainsKey(clicked);

        if (!inQueue && !inFront) return;

        PlayClickSound();

        if (inQueue)
        {
            if (depth != 0) return; 

            if (clicked.linkGroupId > 0) ActivateQueuedGroup(clicked.linkGroupId);
            else ActivateQueuedShooter(clicked, column);
            return;
        }

        if (clicked.linkGroupId > 0) ActivateFrontGroup(clicked.linkGroupId);
        else ActivateFrontShooter(clicked);
    }

    // ---------------------------------------------------------------


    private void ActivateQueuedShooter(Shooter shooter, int column)
    {
        if (returningPlates.Count > 0) return;

        int slotIndex = GetFirstEmptyFrontSlot();
        if (slotIndex < 0) return;

        if (!TryAssignPlate(shooter, Vector3.zero)) return;

        ReserveFrontSlot(shooter, slotIndex);
        PopColumnHead(column);
        AnimateColumn(column);

        shooter.StartMoveOnSpline(
      defaultSplinePath,
      Vector3.zero,
      () => AttachPlateToShooter(shooter),
      () => { ReleasePlate(shooter); PlaceToReservedFrontSlot(shooter); });
    }

    private void ActivateFrontShooter(Shooter shooter)
    {
        if (returningPlates.Count > 0) return;

        int slotIndex = GetFrontSlot(shooter);
        if (slotIndex < 0) return;

        if (!TryAssignPlate(shooter, Vector3.zero)) return;

        shooter.StartMoveOnSpline(
     defaultSplinePath,
     Vector3.zero,
     () => AttachPlateToShooter(shooter),
     () => { ReleasePlate(shooter); PlaceToReservedFrontSlot(shooter); });
    }

    // ---------------------------------------------------------------
 

    private void ActivateQueuedGroup(int groupId)
    {
        if (returningPlates.Count > 0) return;

        var members = GatherQueuedGroup(groupId);
        if (members.Count == 0 || AvailablePlateCount() < members.Count) return;

        // Hepsi depth 0'da olmalý ve farklý kolonlarda
        var usedColumns = new HashSet<int>();
        foreach (var m in members)
        {
            if (m.depth != 0 || !usedColumns.Add(m.column)) return;
        }

        members.Sort((a, b) => a.column.CompareTo(b.column));

        int startSlot = FindContiguousFrontSpace(members.Count);
        if (startSlot < 0) return;

        float center = (members.Count - 1) * 0.5f;

       
        for (int i = 0; i < members.Count; i++)
        {
            Vector3 offset = new Vector3(0f, 0f, (i - center) * groupSideSpacing);
            if (!TryAssignPlate(members[i].shooter, offset)) return;
            ReserveFrontSlot(members[i].shooter, startSlot + i);
        }

        for (int i = 0; i < members.Count; i++)
        {
            PopColumnHead(members[i].column);
            AnimateColumn(members[i].column);
        }

        for (int i = 0; i < members.Count; i++)
        {
            Vector3 offset = new Vector3(0f, 0f, (i - center) * groupSideSpacing);
            LaunchGroupedShooter(members[i].shooter, offset, i * groupLaunchDelay);
        }
    }

    private void ActivateFrontGroup(int groupId)
    {
        if (returningPlates.Count > 0) return;

        var members = GatherFrontGroup(groupId);
        if (members.Count == 0 || AvailablePlateCount() < members.Count) return;

        float center = (members.Count - 1) * 0.5f;

        for (int i = 0; i < members.Count; i++)
        {
            Vector3 offset = new Vector3(0f, 0f, (i - center) * groupSideSpacing);
            if (!TryAssignPlate(members[i].shooter, offset)) return;
        }

        for (int i = 0; i < members.Count; i++)
        {
            Vector3 offset = new Vector3(0f, 0f, (i - center) * groupSideSpacing);
            LaunchGroupedShooter(members[i].shooter, offset, i * groupLaunchDelay);
        }
    }

    private void LaunchGroupedShooter(Shooter shooter, Vector3 offset, float delay)
    {
        DOVirtual.DelayedCall(delay, () =>
        {
            shooter.StartMoveOnSpline(
     defaultSplinePath,
     offset,
     false,
     () => AttachPlateToShooter(shooter),
     () => { ReleasePlate(shooter); PlaceToReservedFrontSlot(shooter); });
        });
    }

    // ---------------------------------------------------------------
  

    private List<QueueGroupMember> GatherQueuedGroup(int groupId)
    {
        var result = new List<QueueGroupMember>();
        if (queueGrid == null) return result;

        for (int col = 0; col < columns; col++)
            for (int dep = 0; dep < depthCount; dep++)
            {
                Shooter s = queueGrid[col, dep];
                if (s != null && s.linkGroupId == groupId)
                    result.Add(new QueueGroupMember { shooter = s, column = col, depth = dep });
            }

        return result;
    }

    private List<FrontGroupMember> GatherFrontGroup(int groupId)
    {
        var result = new List<FrontGroupMember>();

        for (int i = 0; i < frontShooters.Length; i++)
        {
            Shooter s = frontShooters[i];
            if (s != null && s.linkGroupId == groupId)
                result.Add(new FrontGroupMember { shooter = s, slotIndex = i });
        }

        return result;
    }

    // ---------------------------------------------------------------
   

    public void NotifyShooterDestroyed(Shooter shooter)
    {
        if (shooter == null) return;

        if (TryFindInQueue(shooter, out int column, out int depth))
        {
            RemoveFromColumn(column, depth);
            AnimateColumn(column);
        }

        ReleasePlate(shooter);
        ReleaseFrontSlot(shooter);
    }

    // ---------------------------------------------------------------
  

    private void PopColumnHead(int column)
    {
        if (!IsValidColumn(column)) return;
        ShiftColumnUp(column, 0);
    }

    private void RemoveFromColumn(int column, int depth)
    {
        if (!IsValidColumn(column) || depth < 0 || depth >= depthCount) return;
        ShiftColumnUp(column, depth);
    }


    private void ShiftColumnUp(int column, int fromDepth)
    {
        for (int i = fromDepth; i < depthCount - 1; i++)
            queueGrid[column, i] = queueGrid[column, i + 1];
        queueGrid[column, depthCount - 1] = null;
    }

    private void SnapAll()
    {
        if (queueGrid == null || slotMatrix == null) return;

        for (int col = 0; col < columns; col++)
            for (int dep = 0; dep < depthCount; dep++)
            {
                Shooter s = queueGrid[col, dep];
                Transform slot = GetSlot(col, dep);
                if (s == null || slot == null) continue;

                DOTween.Kill(s.transform);
                s.transform.position = slot.position;
            }
    }

    private void AnimateColumn(int column)
    {
        if (queueGrid == null || slotMatrix == null || !IsValidColumn(column)) return;

        for (int dep = 0; dep < depthCount; dep++)
        {
            Shooter s = queueGrid[column, dep];
            Transform slot = GetSlot(column, dep);
            if (s == null || slot == null) continue;

            DOTween.Kill(s.transform);
            s.transform.DOMove(slot.position, queueMoveDuration);
        }
    }

    private bool TryFindInQueue(Shooter shooter, out int column, out int depth)
    {
        column = -1;
        depth = -1;

        if (queueGrid == null) return false;

        for (int col = 0; col < columns; col++)
            for (int dep = 0; dep < depthCount; dep++)
                if (queueGrid[col, dep] == shooter) { column = col; depth = dep; return true; }

        return false;
    }

    // ---------------------------------------------------------------


    private void EnsureFrontSlots()
    {
        if (frontSlots == null)
        {
            frontShooters = new Shooter[0];
            frontSlotLookup.Clear();
            return;
        }

        if (frontShooters == null || frontShooters.Length != frontSlots.Length)
        {
            frontShooters = new Shooter[frontSlots.Length];
            frontSlotLookup.Clear();
        }
    }

    private int GetFirstEmptyFrontSlot()
    {
        EnsureFrontSlots();
        for (int i = 0; i < frontShooters.Length; i++)
            if (frontShooters[i] == null) return i;
        return -1;
    }

    private int FindContiguousFrontSpace(int needed)
    {
        EnsureFrontSlots();
        if (needed <= 0 || frontShooters.Length < needed) return -1;

        for (int start = 0; start <= frontShooters.Length - needed; start++)
        {
            bool free = true;
            for (int i = 0; i < needed; i++)
                if (frontShooters[start + i] != null) { free = false; break; }
            if (free) return start;
        }

        return -1;
    }

    private void ReserveFrontSlot(Shooter shooter, int slotIndex)
    {
        frontShooters[slotIndex] = shooter;
        frontSlotLookup[shooter] = slotIndex;
    }

    private int GetFrontSlot(Shooter shooter)
    {
        if (frontSlotLookup.TryGetValue(shooter, out int slotIndex)) return slotIndex;

        
        slotIndex = GetFirstEmptyFrontSlot();
        if (slotIndex >= 0) ReserveFrontSlot(shooter, slotIndex);
        return slotIndex;
    }

    private void ReleaseFrontSlot(Shooter shooter)
    {
        if (shooter == null) return;

        if (frontSlotLookup.TryGetValue(shooter, out int slotIndex))
        {
            if (slotIndex >= 0 && slotIndex < frontShooters.Length && frontShooters[slotIndex] == shooter)
                frontShooters[slotIndex] = null;

            frontSlotLookup.Remove(shooter);
        }
        else
        {
      
            for (int i = 0; i < frontShooters.Length; i++)
                if (frontShooters[i] == shooter) frontShooters[i] = null;
        }

        FillFrontGaps();
    }

    private void PlaceToFrontSlot(Shooter shooter, int slotIndex)
    {
        if (shooter == null || !shooter.IsAlive || frontSlots == null) return;
        if (slotIndex < 0 || slotIndex >= frontSlots.Length) return;
        shooter.JumpToFrontSlot(frontSlots[slotIndex].position, null);
    }

    private void PlaceToReservedFrontSlot(Shooter shooter) =>
        PlaceToFrontSlot(shooter, GetFrontSlot(shooter));

    
    private void FillFrontGaps()
    {
        EnsureFrontSlots();

        // Saðlýklý shooter'larý topla
        var ordered = new List<Shooter>();
        for (int i = 0; i < frontShooters.Length; i++)
        {
            if (frontShooters[i] != null && frontShooters[i].IsAlive)
                ordered.Add(frontShooters[i]);
            frontShooters[i] = null;
        }

        frontSlotLookup.Clear();

        for (int i = 0; i < ordered.Count; i++)
        {
            frontShooters[i] = ordered[i];
            frontSlotLookup[ordered[i]] = i;
        }

        bool needsRetry = false;

        for (int i = 0; i < ordered.Count; i++)
        {
            if (frontSlots[i] == null) continue;

            Shooter s = ordered[i];
            if (s.IsBusy) { needsRetry = true; continue; }

            Vector3 target = frontSlots[i].position;
            if ((s.transform.position - target).sqrMagnitude > 0.0001f)
                s.ShiftToFrontSlot(target, FillFrontGaps);
        }

      
        if (needsRetry)
            DOVirtual.DelayedCall(0.05f, FillFrontGaps);
    }

    // ---------------------------------------------------------------


    private int AvailablePlateCount() => plateStack.Count - returningPlates.Count;

    private bool TryAssignPlate(Shooter shooter, Vector3 formationOffset)
    {
        if (shooter == null) return false;
        if (shooterPlateMap.ContainsKey(shooter)) return true; 

        ShootersPlate plate = TakePlate();
        if (plate == null) return false;

        shooterPlateMap[shooter] = plate;

        Vector3 entryPoint = GetSplineStartWorldPosition(shooter, formationOffset);
        float duration = Mathf.Max(0f, shooter.toSplineJumpDuration);

        Quaternion entryRotation =
            Quaternion.Euler(shooter.toSplineJumpRotationEuler) *
            Quaternion.Euler(plateChildLocalRotationEuler);

        plate.MoveToSplineStart(entryPoint, entryRotation, duration, platePickupSpinEuler);
        return true;
    }

    private void AttachPlateToShooter(Shooter shooter)
    {
        if (shooter == null) return;
        if (!shooterPlateMap.TryGetValue(shooter, out ShootersPlate plate)) return;

        plate.AttachToShooter(
            shooter.transform,
            plateChildLocalOffset,
            plateChildLocalRotationEuler,
            plateAttachDuration);
    }

    private void ReleasePlate(Shooter shooter)
    {
        if (shooter == null) return;
        if (!shooterPlateMap.TryGetValue(shooter, out ShootersPlate plate)) return;

        shooterPlateMap.Remove(shooter);

        if (plateStack.Contains(plate)) return; 

        plateStack.Add(plate);
        returningPlates.Add(plate);

        int slotIndex = plateStack.Count - 1;
        MoveSpecificPlateToSlot(plate, slotIndex, false, plateReturnSpinEuler,
            onComplete: () => returningPlates.Remove(plate));
    }

    private ShootersPlate TakePlate()
    {
        while (plateStack.Count > 0)
        {
            int last = plateStack.Count - 1;
            ShootersPlate plate = plateStack[last];

            if (plate == null) { plateStack.RemoveAt(last); continue; }

       
            if (returningPlates.Contains(plate)) return null;

            plateStack.RemoveAt(last);
            return plate;
        }

        return null;
    }

    private void RebuildPlateLine()
    {
        plateStack.Clear();
        shooterPlateMap.Clear();
        returningPlates.Clear();

        if (plateLineRoot == null)
            plateLineRoot = new GameObject("PlateLineRuntime").transform;

        ClearChildren(plateLineRoot);

        if (platePrefab == null || plateCount <= 0)
        {
            plateSlots = new Transform[0];
            return;
        }

        plateSlots = new Transform[plateCount];

        for (int i = 0; i < plateCount; i++)
        {
            Vector3 slotPos = plateLineOrigin + plateLineStep * i;
            Quaternion lineRot = Quaternion.Euler(plateLineRotationEuler);

            GameObject slotObj = new GameObject($"PlateSlot_{i}");
            slotObj.transform.SetParent(plateLineRoot);
            slotObj.transform.position = slotPos;
            slotObj.transform.rotation = lineRot;
            plateSlots[i] = slotObj.transform;

            GameObject plateObj = Instantiate(platePrefab, slotPos, lineRot, plateLineRoot);
            ShootersPlate plate = plateObj.GetComponent<ShootersPlate>() ?? plateObj.AddComponent<ShootersPlate>();

            plateStack.Add(plate);
        }

        CompactPlates(instant: true);
    }

    private void CompactPlates(bool instant)
    {
        if (plateSlots == null) return;

        float duration = instant ? 0f : plateMoveDuration;
        Quaternion lineRot = Quaternion.Euler(plateLineRotationEuler);

        for (int i = 0; i < plateStack.Count && i < plateSlots.Length; i++)
        {
            ShootersPlate plate = plateStack[i];
            if (plate == null || returningPlates.Contains(plate)) continue;

            plate.MoveToLine(plateLineRoot, plateSlots[i].position, lineRot, duration, Vector3.zero, null);
        }
    }

    private void MoveSpecificPlateToSlot(ShootersPlate plate, int slotIndex, bool instant, Vector3 spinEuler, System.Action onComplete)
    {
        if (plate == null || plateSlots == null || slotIndex < 0 || slotIndex >= plateSlots.Length) return;

        plate.MoveToLine(
            plateLineRoot,
            plateSlots[slotIndex].position,
            Quaternion.Euler(plateLineRotationEuler),
            instant ? 0f : plateMoveDuration,
            spinEuler,
            onComplete);
    }

    // ---------------------------------------------------------------


    private Vector3 GetSplineStartWorldPosition(Shooter shooter, Vector3 formationOffset)
    {
        if (defaultSplinePath?.splineContainer == null)
            return shooter != null ? shooter.transform.position : Vector3.zero;

        SplineContainer container = defaultSplinePath.splineContainer;
        if (container.Splines == null || container.Splines.Count == 0)
            return shooter != null ? shooter.transform.position : Vector3.zero;

        int idx = Mathf.Clamp(defaultSplinePath.splineIndex, 0, container.Splines.Count - 1);
        Spline spl = container.Splines[idx];
        Transform root = container.transform;

        float lookAhead = shooter != null ? Mathf.Min(1f, shooter.splineRotationLookAheadT) : 0.03f;

        float3 startLocal = SplineUtility.EvaluatePosition(spl, 0f);
        Vector3 startWorld = root.TransformPoint(new Vector3(startLocal.x, startLocal.y, startLocal.z));

        if (formationOffset.sqrMagnitude > 0.000001f)
        {
            float3 tanLocal = SplineUtility.EvaluateTangent(spl, lookAhead);
            float3 upLocal = SplineUtility.EvaluateUpVector(spl, lookAhead);

            Vector3 forward = root.TransformDirection(new Vector3(tanLocal.x, tanLocal.y, tanLocal.z));
            Vector3 up = root.TransformDirection(new Vector3(upLocal.x, upLocal.y, upLocal.z));

            forward = forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector3.forward;
            up = up.sqrMagnitude > 0.000001f ? up.normalized : Vector3.up;
            Vector3 right = Vector3.Cross(up, forward).normalized;

            startWorld += right * formationOffset.x + up * formationOffset.y + forward * formationOffset.z;
        }

        return startWorld;
    }

    // ---------------------------------------------------------------
   

    private bool IsValidSlot(int column, int depth) =>
        slotMatrix != null && column >= 0 && column < columns && depth >= 0 && depth < depthCount;

    private bool IsValidColumn(int column) =>
        queueGrid != null && column >= 0 && column < columns;

    private Transform GetSlot(int column, int depth) =>
        IsValidSlot(column, depth) ? slotMatrix[column][depth] : null;

    private void PlayClickSound()
    {
        if (shooterClickClip != null && shooterClickSource != null)
            shooterClickSource.PlayOneShot(shooterClickClip, shooterClickVolume);
    }

    private void ClearChildren(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }
}
