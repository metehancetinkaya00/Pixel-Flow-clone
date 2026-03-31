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

    [Header("Plates")]
    public GameObject platePrefab;
    public int plateCount = 4;
    public Transform plateLineRoot;
    public Vector3 plateLineOrigin = Vector3.zero;
    public Vector3 plateLineStep = new Vector3(0.8f, 0f, 0f);
    public Vector3 plateLineRotationEuler = Vector3.zero;
    public Vector3 plateChildLocalOffset = new Vector3(0f, -1f, 0f);
    public Vector3 plateChildLocalRotationEuler = Vector3.zero;
    public float plateMoveDuration = 0.2f;
    public float plateAttachDuration = 0.1f;
    public Vector3 platePickupSpinEuler = new Vector3(0f, 360f, 0f);
    public Vector3 plateReturnSpinEuler = new Vector3(0f, 360f, 0f);

    public struct SpawnedPlacement
    {
        public Shooter shooter;
        public int column;
        public int depth;
    }

    private struct QueueGroupMember
    {
        public Shooter shooter;
        public int column;
        public int depth;
    }

    private struct FrontGroupMember
    {
        public Shooter shooter;
        public int slotIndex;
    }

    private Transform[][] slotMatrix;
    private int columns;
    private int depthCount;

    private Shooter[,] queueGrid;
    private Shooter[] frontShooters;
    private readonly Dictionary<Shooter, int> frontSlotLookup = new Dictionary<Shooter, int>();

    private readonly List<ShootersPlate> plateStack = new List<ShootersPlate>();
    private readonly Dictionary<Shooter, ShootersPlate> shooterPlateMap = new Dictionary<Shooter, ShootersPlate>();
    private readonly HashSet<ShootersPlate> blockedPlates = new HashSet<ShootersPlate>();
    private Transform[] plateSlots;

    private void Awake()
    {
        Instance = this;
        EnsureFrontSlots();
    }

    public void ApplyLayout(QueueLayoutSettings settings)
    {
        columns = Mathf.Max(1, settings.columnCount);
        depthCount = Mathf.Max(1, settings.depthCount);

        if (runtimeQueueRoot == null)
        {
            runtimeQueueRoot = new GameObject("QueueSlotsRuntime").transform;
        }

        if (settings.createSlotObjects)
        {
            ClearChildren(runtimeQueueRoot);
        }

        slotMatrix = new Transform[columns][];

        for (int column = 0; column < columns; column++)
        {
            slotMatrix[column] = new Transform[depthCount];

            for (int depth = 0; depth < depthCount; depth++)
            {
                Vector3 position = settings.origin + (settings.columnStep * column) + (settings.depthStep * depth);

                if (settings.createSlotObjects)
                {
                    GameObject slotObject = new GameObject("Q_" + column + "_" + depth);
                    slotObject.transform.SetParent(runtimeQueueRoot);
                    slotObject.transform.position = position;
                    slotMatrix[column][depth] = slotObject.transform;
                }
                else
                {
                    GameObject slotObject = new GameObject();
                    slotObject.hideFlags = HideFlags.HideAndDontSave;
                    slotObject.transform.position = position;
                    slotMatrix[column][depth] = slotObject.transform;
                }
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

        if (queueGrid == null || slotMatrix == null)
        {
            return;
        }

        for (int column = 0; column < columns; column++)
        {
            for (int depth = 0; depth < depthCount; depth++)
            {
                queueGrid[column, depth] = null;
            }
        }

        for (int i = 0; i < frontShooters.Length; i++)
        {
            frontShooters[i] = null;
        }

        frontSlotLookup.Clear();
        RebuildPlateLine();

        if (placements == null)
        {
            SnapAll();
            return;
        }

        for (int i = 0; i < placements.Count; i++)
        {
            Shooter shooter = placements[i].shooter;
            int column = placements[i].column;
            int depth = placements[i].depth;

            if (shooter == null || !IsValidSlot(column, depth))
            {
                continue;
            }

            queueGrid[column, depth] = shooter;
        }

        SnapAll();
    }

    public void TryActivateShooter(Shooter clicked)
    {
        if (clicked == null || !clicked.IsAlive || clicked.IsBusy)
        {
            return;
        }

        if (defaultSplinePath == null || defaultSplinePath.splineContainer == null)
        {
            return;
        }

        bool inQueue = TryFindInQueue(clicked, out int column, out int depth);
        bool inFront = frontSlotLookup.ContainsKey(clicked);

        if (!inQueue && !inFront)
        {
            return;
        }

        if (inQueue)
        {
            if (depth != 0)
            {
                return;
            }

            if (clicked.linkGroupId > 0)
            {
                ActivateQueuedGroup(clicked.linkGroupId);
                return;
            }

            ActivateQueuedShooter(clicked, column);
            return;
        }

        if (clicked.linkGroupId > 0)
        {
            ActivateFrontGroup(clicked.linkGroupId);
            return;
        }

        ActivateFrontShooter(clicked);
    }

    private void ActivateQueuedShooter(Shooter shooter, int column)
    {
        int slotIndex = GetFirstEmptyFrontSlot();
        if (slotIndex < 0)
        {
            return;
        }

        if (!TryAssignPlate(shooter, Vector3.zero))
        {
            return;
        }

        ReserveFrontSlot(shooter, slotIndex);
        PopColumnHead(column);
        AnimateColumn(column);

        shooter.StartMoveOnSpline(
            defaultSplinePath,
            Vector3.zero,
            () => AttachPlateToShooter(shooter),
            () =>
            {
                ReleasePlate(shooter);
                PlaceToReservedFrontSlot(shooter);
            });
    }

    private void ActivateFrontShooter(Shooter shooter)
    {
        int slotIndex = GetFrontSlot(shooter);
        if (slotIndex < 0)
        {
            return;
        }

        if (!TryAssignPlate(shooter, Vector3.zero))
        {
            return;
        }

        shooter.StartMoveOnSpline(
            defaultSplinePath,
            Vector3.zero,
            () => AttachPlateToShooter(shooter),
            () =>
            {
                ReleasePlate(shooter);
                PlaceToReservedFrontSlot(shooter);
            });
    }

    private void ActivateQueuedGroup(int groupId)
    {
        List<QueueGroupMember> members = GatherQueuedGroup(groupId);
        if (members.Count == 0 || plateStack.Count < members.Count)
        {
            return;
        }

        HashSet<int> usedColumns = new HashSet<int>();

        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].depth != 0 || usedColumns.Contains(members[i].column))
            {
                return;
            }

            usedColumns.Add(members[i].column);
        }

        members.Sort((a, b) => a.column.CompareTo(b.column));

        int startSlot = FindContiguousFrontSpace(members.Count);
        if (startSlot < 0)
        {
            return;
        }

        float center = (members.Count - 1) * 0.5f;

        for (int i = 0; i < members.Count; i++)
        {
            Shooter shooter = members[i].shooter;
            Vector3 offset = new Vector3(0f, 0f, (i - center) * groupSideSpacing);

            if (!TryAssignPlate(shooter, offset))
            {
                for (int j = 0; j < i; j++)
                {
                    ReleasePlate(members[j].shooter);
                }

                return;
            }

            ReserveFrontSlot(shooter, startSlot + i);
        }

        for (int i = 0; i < members.Count; i++)
        {
            PopColumnHead(members[i].column);
            AnimateColumn(members[i].column);
        }

        for (int i = 0; i < members.Count; i++)
        {
            Shooter shooter = members[i].shooter;
            Vector3 offset = new Vector3(0f, 0f, (i - center) * groupSideSpacing);

            shooter.StartMoveOnSpline(
                defaultSplinePath,
                offset,
                () => AttachPlateToShooter(shooter),
                () =>
                {
                    ReleasePlate(shooter);
                    PlaceToReservedFrontSlot(shooter);
                });
        }
    }

    private void ActivateFrontGroup(int groupId)
    {
        List<FrontGroupMember> members = GatherFrontGroup(groupId);
        if (members.Count == 0 || plateStack.Count < members.Count)
        {
            return;
        }

        float center = (members.Count - 1) * 0.5f;

        for (int i = 0; i < members.Count; i++)
        {
            Shooter shooter = members[i].shooter;
            Vector3 offset = new Vector3(0f, 0f, (i - center) * groupSideSpacing);

            if (!TryAssignPlate(shooter, offset))
            {
                for (int j = 0; j < i; j++)
                {
                    ReleasePlate(members[j].shooter);
                }

                return;
            }
        }

        for (int i = 0; i < members.Count; i++)
        {
            Shooter shooter = members[i].shooter;
            Vector3 offset = new Vector3(0f, 0f, (i - center) * groupSideSpacing);

            shooter.StartMoveOnSpline(
                defaultSplinePath,
                offset,
                () => AttachPlateToShooter(shooter),
                () =>
                {
                    ReleasePlate(shooter);
                    PlaceToReservedFrontSlot(shooter);
                });
        }
    }

    private List<QueueGroupMember> GatherQueuedGroup(int groupId)
    {
        List<QueueGroupMember> result = new List<QueueGroupMember>();

        if (queueGrid == null)
        {
            return result;
        }

        for (int column = 0; column < columns; column++)
        {
            for (int depth = 0; depth < depthCount; depth++)
            {
                Shooter shooter = queueGrid[column, depth];

                if (shooter != null && shooter.linkGroupId == groupId)
                {
                    QueueGroupMember member;
                    member.shooter = shooter;
                    member.column = column;
                    member.depth = depth;
                    result.Add(member);
                }
            }
        }

        return result;
    }

    private List<FrontGroupMember> GatherFrontGroup(int groupId)
    {
        List<FrontGroupMember> result = new List<FrontGroupMember>();

        for (int i = 0; i < frontShooters.Length; i++)
        {
            Shooter shooter = frontShooters[i];

            if (shooter != null && shooter.linkGroupId == groupId)
            {
                FrontGroupMember member;
                member.shooter = shooter;
                member.slotIndex = i;
                result.Add(member);
            }
        }

        return result;
    }

    private int FindContiguousFrontSpace(int needed)
    {
        EnsureFrontSlots();

        if (needed <= 0 || frontShooters.Length < needed)
        {
            return -1;
        }

        for (int start = 0; start <= frontShooters.Length - needed; start++)
        {
            bool free = true;

            for (int i = 0; i < needed; i++)
            {
                if (frontShooters[start + i] != null)
                {
                    free = false;
                    break;
                }
            }

            if (free)
            {
                return start;
            }
        }

        return -1;
    }

    public void NotifyShooterDestroyed(Shooter shooter)
    {
        if (shooter == null)
        {
            return;
        }

        if (TryFindInQueue(shooter, out int column, out int depth))
        {
            RemoveFromColumn(column, depth);
            AnimateColumn(column);
        }

        ReleasePlate(shooter);
        ReleaseFrontSlot(shooter);
    }

    private void PopColumnHead(int column)
    {
        if (queueGrid == null || column < 0 || column >= columns)
        {
            return;
        }

        for (int depth = 0; depth < depthCount - 1; depth++)
        {
            queueGrid[column, depth] = queueGrid[column, depth + 1];
        }

        queueGrid[column, depthCount - 1] = null;
    }

    private void RemoveFromColumn(int column, int depth)
    {
        if (queueGrid == null || column < 0 || column >= columns || depth < 0 || depth >= depthCount)
        {
            return;
        }

        for (int i = depth; i < depthCount - 1; i++)
        {
            queueGrid[column, i] = queueGrid[column, i + 1];
        }

        queueGrid[column, depthCount - 1] = null;
    }

    private void SnapAll()
    {
        if (queueGrid == null || slotMatrix == null)
        {
            return;
        }

        for (int column = 0; column < columns; column++)
        {
            for (int depth = 0; depth < depthCount; depth++)
            {
                Shooter shooter = queueGrid[column, depth];
                Transform slot = GetSlot(column, depth);

                if (shooter == null || slot == null)
                {
                    continue;
                }

                DOTween.Kill(shooter.transform);
                shooter.transform.position = slot.position;
            }
        }
    }

    private void AnimateColumn(int column)
    {
        if (queueGrid == null || slotMatrix == null || column < 0 || column >= columns)
        {
            return;
        }

        for (int depth = 0; depth < depthCount; depth++)
        {
            Shooter shooter = queueGrid[column, depth];
            Transform slot = GetSlot(column, depth);

            if (shooter == null || slot == null)
            {
                continue;
            }

            DOTween.Kill(shooter.transform);
            shooter.transform.DOMove(slot.position, queueMoveDuration);
        }
    }

    private bool TryFindInQueue(Shooter shooter, out int column, out int depth)
    {
        column = -1;
        depth = -1;

        if (queueGrid == null)
        {
            return false;
        }

        for (int c = 0; c < columns; c++)
        {
            for (int d = 0; d < depthCount; d++)
            {
                if (queueGrid[c, d] == shooter)
                {
                    column = c;
                    depth = d;
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsValidSlot(int column, int depth)
    {
        return slotMatrix != null && column >= 0 && column < columns && depth >= 0 && depth < depthCount;
    }

    private Transform GetSlot(int column, int depth)
    {
        return IsValidSlot(column, depth) ? slotMatrix[column][depth] : null;
    }

    private void PlaceToFrontSlot(Shooter shooter, int slotIndex)
    {
        if (shooter == null || !shooter.IsAlive || frontSlots == null || slotIndex < 0 || slotIndex >= frontSlots.Length)
        {
            return;
        }

        shooter.JumpToFrontSlot(frontSlots[slotIndex].position, null);
    }

    private void PlaceToReservedFrontSlot(Shooter shooter)
    {
        int slotIndex = GetFrontSlot(shooter);

        if (slotIndex < 0)
        {
            return;
        }

        PlaceToFrontSlot(shooter, slotIndex);
    }

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
        {
            if (frontShooters[i] == null)
            {
                return i;
            }
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
        if (frontSlotLookup.TryGetValue(shooter, out int slotIndex))
        {
            return slotIndex;
        }

        slotIndex = GetFirstEmptyFrontSlot();
        if (slotIndex < 0)
        {
            return -1;
        }

        ReserveFrontSlot(shooter, slotIndex);
        return slotIndex;
    }

    private void ReleaseFrontSlot(Shooter shooter)
    {
        if (shooter == null)
        {
            return;
        }

        if (frontSlotLookup.TryGetValue(shooter, out int slotIndex))
        {
            if (slotIndex >= 0 && slotIndex < frontShooters.Length && frontShooters[slotIndex] == shooter)
            {
                frontShooters[slotIndex] = null;
            }

            frontSlotLookup.Remove(shooter);
        }
        else
        {
            for (int i = 0; i < frontShooters.Length; i++)
            {
                if (frontShooters[i] == shooter)
                {
                    frontShooters[i] = null;
                }
            }
        }

        FillFrontGaps();
    }

    private void FillFrontGaps()
    {
        EnsureFrontSlots();

        Dictionary<Shooter, int> previousSlots = new Dictionary<Shooter, int>(frontSlotLookup);
        List<Shooter> ordered = new List<Shooter>();

        for (int i = 0; i < frontShooters.Length; i++)
        {
            Shooter shooter = frontShooters[i];

            if (shooter != null)
            {
                ordered.Add(shooter);
            }

            frontShooters[i] = null;
        }

        frontSlotLookup.Clear();

        for (int i = 0; i < ordered.Count; i++)
        {
            Shooter shooter = ordered[i];
            frontShooters[i] = shooter;
            frontSlotLookup[shooter] = i;
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            Shooter shooter = ordered[i];

            if (!shooter.IsAlive || shooter.IsBusy || frontSlots[i] == null)
            {
                continue;
            }

            if (previousSlots.TryGetValue(shooter, out int oldSlot) && oldSlot == i)
            {
                continue;
            }

            shooter.ShiftToFrontSlot(frontSlots[i].position, null);
        }
    }

    private bool TryAssignPlate(Shooter shooter, Vector3 formationOffset)
    {
        if (shooter == null)
        {
            return false;
        }

        if (shooterPlateMap.ContainsKey(shooter))
        {
            return true;
        }

        ShootersPlate plate = TakePlate();
        if (plate == null)
        {
            return false;
        }

        shooterPlateMap[shooter] = plate;

        Vector3 entryPoint = GetSplineStartWorldPosition(shooter, formationOffset);
        float duration = Mathf.Max(0f, shooter.toSplineJumpDuration);

        Quaternion entryRotation =
            Quaternion.Euler(shooter.toSplineJumpRotationEuler) *
            Quaternion.Euler(plateChildLocalRotationEuler);

        plate.MoveToSplineStart(
            entryPoint,
            entryRotation,
            duration,
            platePickupSpinEuler);

        CompactPlates(false);
        return true;
    }

    private void AttachPlateToShooter(Shooter shooter)
    {
        if (shooter == null)
        {
            return;
        }

        if (!shooterPlateMap.TryGetValue(shooter, out ShootersPlate plate))
        {
            return;
        }

        plate.AttachToShooter(
            shooter.transform,
            plateChildLocalOffset,
            plateChildLocalRotationEuler,
            plateAttachDuration);
    }

    private void ReleasePlate(Shooter shooter)
    {
        if (shooter == null)
        {
            return;
        }

        if (!shooterPlateMap.TryGetValue(shooter, out ShootersPlate plate))
        {
            return;
        }

        shooterPlateMap.Remove(shooter);

        if (!plateStack.Contains(plate))
        {
            plateStack.Add(plate);
        }

        blockedPlates.Add(plate);
        CompactPlates(false);

        int slotIndex = plateStack.IndexOf(plate);

        MoveSpecificPlateToSlot(
            plate,
            slotIndex,
            false,
            plateReturnSpinEuler,
            () => blockedPlates.Remove(plate));
    }

    private ShootersPlate TakePlate()
    {
        for (int i = plateStack.Count - 1; i >= 0; i--)
        {
            ShootersPlate plate = plateStack[i];

            if (plate == null)
            {
                plateStack.RemoveAt(i);
                continue;
            }

            if (blockedPlates.Contains(plate))
            {
                continue;
            }

            plateStack.RemoveAt(i);
            return plate;
        }

        return null;
    }

    private Vector3 GetSplineStartWorldPosition(Shooter shooter, Vector3 formationOffset)
    {
        if (defaultSplinePath == null || defaultSplinePath.splineContainer == null)
        {
            return shooter != null ? shooter.transform.position : Vector3.zero;
        }

        SplineContainer container = defaultSplinePath.splineContainer;
        if (container.Splines == null || container.Splines.Count == 0)
        {
            return shooter != null ? shooter.transform.position : Vector3.zero;
        }

        int index = Mathf.Clamp(defaultSplinePath.splineIndex, 0, container.Splines.Count - 1);
        Spline spline = container.Splines[index];
        Transform root = container.transform;

        float lookAhead = shooter != null ? Mathf.Min(1f, shooter.splineRotationLookAheadT) : 0.03f;

        float3 startLocal = SplineUtility.EvaluatePosition(spline, 0f);
        Vector3 startWorld = root.TransformPoint(new Vector3(startLocal.x, startLocal.y, startLocal.z));

        if (formationOffset.sqrMagnitude > 0.000001f)
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

        return startWorld;
    }

    private void RebuildPlateLine()
    {
        plateStack.Clear();
        shooterPlateMap.Clear();
        blockedPlates.Clear();

        if (plateLineRoot == null)
        {
            plateLineRoot = new GameObject("PlateLineRuntime").transform;
        }

        ClearChildren(plateLineRoot);

        if (platePrefab == null || plateCount <= 0)
        {
            plateSlots = new Transform[0];
            return;
        }

        plateSlots = new Transform[plateCount];

        for (int i = 0; i < plateCount; i++)
        {
            Vector3 slotPos = plateLineOrigin + (plateLineStep * i);

            GameObject slotObject = new GameObject("PlateSlot_" + i);
            slotObject.transform.SetParent(plateLineRoot);
            slotObject.transform.position = slotPos;
            slotObject.transform.rotation = Quaternion.Euler(plateLineRotationEuler);
            plateSlots[i] = slotObject.transform;

            GameObject plateObject = Instantiate(
                platePrefab,
                slotPos,
                Quaternion.Euler(plateLineRotationEuler),
                plateLineRoot);

            ShootersPlate plate = plateObject.GetComponent<ShootersPlate>();
            if (plate == null)
            {
                plate = plateObject.AddComponent<ShootersPlate>();
            }

            plateStack.Add(plate);
        }

        CompactPlates(true);
    }

    private void CompactPlates(bool instant)
    {
        if (plateSlots == null)
        {
            return;
        }

        float duration = instant ? 0f : plateMoveDuration;
        Quaternion lineRotation = Quaternion.Euler(plateLineRotationEuler);
        int visualSlot = 0;

        for (int i = 0; i < plateStack.Count && visualSlot < plateSlots.Length; i++)
        {
            ShootersPlate plate = plateStack[i];

            if (plate == null || blockedPlates.Contains(plate))
            {
                continue;
            }

            plate.MoveToLine(
                plateLineRoot,
                plateSlots[visualSlot].position,
                lineRotation,
                duration,
                Vector3.zero,
                null);

            visualSlot++;
        }
    }

    private void MoveSpecificPlateToSlot(
        ShootersPlate plate,
        int slotIndex,
        bool instant,
        Vector3 spinEuler,
        System.Action onComplete)
    {
        if (plate == null || plateSlots == null || slotIndex < 0 || slotIndex >= plateSlots.Length)
        {
            return;
        }

        float duration = instant ? 0f : plateMoveDuration;
        Quaternion lineRotation = Quaternion.Euler(plateLineRotationEuler);

        plate.MoveToLine(
            plateLineRoot,
            plateSlots[slotIndex].position,
            lineRotation,
            duration,
            spinEuler,
            onComplete);
    }

    private void ClearChildren(Transform t)
    {
        if (t == null)
        {
            return;
        }

        for (int i = t.childCount - 1; i >= 0; i--)
        {
            Destroy(t.GetChild(i).gameObject);
        }
    }
}