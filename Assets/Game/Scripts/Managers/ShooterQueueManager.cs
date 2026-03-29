using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class ShooterQueueManager : MonoBehaviour
{
    public static ShooterQueueManager Instance;

    public Transform runtimeQueueRoot;

    public Transform[] frontSlots;

    public SplinePathDefinition defaultSplinePath;

    public float queueMoveDuration = 0.15f;

    public float groupSideSpacing = 0.6f;

    public struct SpawnedPlacement
    {
        public Shooter shooter;
        public int column;
        public int depth;
    }

    private struct GroupMember
    {
        public Shooter shooter;
        public int col;
        public int depth;
    }

    private Transform[][] slotMatrix;

    private int columns;
    private int depthCount;

    private Shooter[,] queueGrid;

    private Shooter[] frontOccupants;
    private Dictionary<Shooter, int> frontIndexMap = new Dictionary<Shooter, int>();

    private void Awake()
    {
        Instance = this;
        EnsureFrontOccupantsSize();
    }

    public void ApplyLayout(QueueLayoutSettings settings)
    {
        int colcount = settings.columnCount;
        int deptcount = settings.depthCount;

        if (colcount < 1)
        {
            colcount = 1;
        }

        if (deptcount < 1)
        {
            deptcount = 1;
        }

        columns = colcount;
        depthCount = deptcount;

        if (runtimeQueueRoot == null)
        {
            GameObject go = new GameObject("QueueSlotsRuntime");
            runtimeQueueRoot = go.transform;
        }

        if (settings.createSlotObjects)
        {
            ClearChildren(runtimeQueueRoot);
        }

        slotMatrix = new Transform[columns][];

        for (int col = 0; col < columns; col++)
        {
            slotMatrix[col] = new Transform[depthCount];

            for (int dep = 0; dep < depthCount; dep++)
            {
                Vector3 pos = settings.origin + (settings.columnStep * col) + (settings.depthStep * dep);

                if (settings.createSlotObjects)
                {
                    GameObject sgo = new GameObject("Q_" + col.ToString() + "_" + dep.ToString());
                    sgo.transform.SetParent(runtimeQueueRoot);
                    sgo.transform.position = pos;
                    slotMatrix[col][dep] = sgo.transform;
                }
                else
                {
                    GameObject sgo = new GameObject();
                    sgo.hideFlags = HideFlags.HideAndDontSave;
                    sgo.transform.position = pos;
                    slotMatrix[col][dep] = sgo.transform;
                }
            }
        }

        queueGrid = new Shooter[columns, depthCount];
    }

    public Vector3 GetQueueSpawnPosition()
    {
        Transform t = GetSlot(0, 0);
        if (t == null)
        {
            return Vector3.zero;
        }

        return t.position;
    }

    public void InitializeQueueFromPlacements(List<SpawnedPlacement> placements)
    {
        EnsureFrontOccupantsSize();

        if (queueGrid == null || slotMatrix == null)
        {
            return;
        }

        for (int c = 0; c < columns; c++)
        {
            for (int d = 0; d < depthCount; d++)
            {
                queueGrid[c, d] = null;
            }
        }

        if (placements == null)
        {
            SnapAll();
            return;
        }

        for (int i = 0; i < placements.Count; i++)
        {
            Shooter s = placements[i].shooter;
            int col = placements[i].column;
            int dep = placements[i].depth;

            if (s == null)
            {
                continue;
            }

            if (!IsValidSlot(col, dep))
            {
                continue;
            }

            queueGrid[col, dep] = s;
        }

        SnapAll();
    }

    public void TryActivateShooter(Shooter clicked)
    {
        if (clicked == null)
        {
            return;
        }

        if (!clicked.IsAlive)
        {
            return;
        }

        if (clicked.IsBusy)
        {
            return;
        }

        if (defaultSplinePath == null || defaultSplinePath.splineContainer == null)
        {
            return;
        }

        EnsureFrontOccupantsSize();

        bool inQueue = TryFindInQueue(clicked, out int col, out int dep);
        bool inFront = frontIndexMap.ContainsKey(clicked);

        if (!inQueue && !inFront)
        {
            return;
        }

        if (inQueue)
        {
            if (dep != 0)
            {
                return;
            }

            int gid = clicked.linkGroupId;

            if (gid > 0)
            {
                TryActivateGroup(gid);
                return;
            }

            int reservedFrontIndex = GetFirstEmptyFrontSlotIndex();
            if (reservedFrontIndex < 0)
            {
                return;
            }

            ReserveFrontSlot(clicked, reservedFrontIndex);

            PopHeadFromColumn(col);
            AnimateColumn(col);

            clicked.StartMoveOnSpline(defaultSplinePath, Vector3.zero, () =>
            {
                PlaceToFrontSlot(clicked, reservedFrontIndex);
            });

            return;
        }

        if (inFront)
        {
            int frontIndex = GetOrReserveFrontIndex(clicked);
            if (frontIndex < 0)
            {
                return;
            }

            clicked.StartMoveOnSpline(defaultSplinePath, Vector3.zero, () =>
            {
                PlaceToFrontSlot(clicked, frontIndex);
            });

            return;
        }
    }

    private void TryActivateGroup(int groupId)
    {
        List<GroupMember> members = GatherGroupMembers(groupId);

        if (members.Count <= 0)
        {
            return;
        }

        HashSet<int> uniqueCols = new HashSet<int>();

        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].depth != 0)
            {
                return;
            }

            if (uniqueCols.Contains(members[i].col))
            {
                return;
            }

            uniqueCols.Add(members[i].col);
        }

        members.Sort((a, b) => a.col.CompareTo(b.col));

        int needed = members.Count;

        int startFront = FindContiguousFreeFrontSlots(needed);
        if (startFront < 0)
        {
            return;
        }

        for (int i = 0; i < members.Count; i++)
        {
            Shooter s = members[i].shooter;
            int slotIndex = startFront + i;

            ReserveFrontSlot(s, slotIndex);
        }

        for (int i = 0; i < members.Count; i++)
        {
            PopHeadFromColumn(members[i].col);
            AnimateColumn(members[i].col);
        }

        float center = (needed - 1) * 0.5f;

        for (int i = 0; i < members.Count; i++)
        {
            Shooter s = members[i].shooter;
            int slotIndex = startFront + i;

            float x = (i - center) * groupSideSpacing;
            Vector3 offset = new Vector3(0f, 0f, x);

            s.StartMoveOnSpline(defaultSplinePath, offset, () =>
            {
                PlaceToFrontSlot(s, slotIndex);
            });
        }
    }

    private List<GroupMember> GatherGroupMembers(int groupId)
    {
        List<GroupMember> list = new List<GroupMember>();

        if (queueGrid == null)
        {
            return list;
        }

        for (int c = 0; c < columns; c++)
        {
            for (int d = 0; d < depthCount; d++)
            {
                Shooter s = queueGrid[c, d];
                if (s == null)
                {
                    continue;
                }

                if (s.linkGroupId == groupId)
                {
                    GroupMember m;
                    m.shooter = s;
                    m.col = c;
                    m.depth = d;
                    list.Add(m);
                }
            }
        }

        return list;
    }

    private int FindContiguousFreeFrontSlots(int needed)
    {
        if (needed <= 0)
        {
            return -1;
        }

        EnsureFrontOccupantsSize();

        if (frontOccupants == null)
        {
            return -1;
        }

        if (frontOccupants.Length < needed)
        {
            return -1;
        }

        for (int start = 0; start <= frontOccupants.Length - needed; start++)
        {
            bool ok = true;

            for (int i = 0; i < needed; i++)
            {
                if (frontOccupants[start + i] != null)
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
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

        bool inQueue = TryFindInQueue(shooter, out int col, out int dep);

        if (inQueue)
        {
            RemoveAtAndCompact(col, dep);
            AnimateColumn(col);
        }

        FreeFrontReservation(shooter);
    }

    private void PopHeadFromColumn(int col)
    {
        if (queueGrid == null)
        {
            return;
        }

        if (col < 0 || col >= columns)
        {
            return;
        }

        for (int d = 0; d < depthCount - 1; d++)
        {
            queueGrid[col, d] = queueGrid[col, d + 1];
        }

        queueGrid[col, depthCount - 1] = null;
    }

    private void RemoveAtAndCompact(int col, int dep)
    {
        if (queueGrid == null)
        {
            return;
        }

        if (col < 0 || col >= columns)
        {
            return;
        }

        if (dep < 0 || dep >= depthCount)
        {
            return;
        }

        for (int d = dep; d < depthCount - 1; d++)
        {
            queueGrid[col, d] = queueGrid[col, d + 1];
        }

        queueGrid[col, depthCount - 1] = null;
    }

    private void SnapAll()
    {
        if (queueGrid == null || slotMatrix == null)
        {
            return;
        }

        for (int c = 0; c < columns; c++)
        {
            for (int d = 0; d < depthCount; d++)
            {
                Shooter s = queueGrid[c, d];
                Transform slot = GetSlot(c, d);

                if (s == null || slot == null)
                {
                    continue;
                }

                DOTween.Kill(s.transform);
                s.transform.position = slot.position;
            }
        }
    }

    private void AnimateColumn(int col)
    {
        if (queueGrid == null || slotMatrix == null)
        {
            return;
        }

        if (col < 0 || col >= columns)
        {
            return;
        }

        for (int d = 0; d < depthCount; d++)
        {
            Shooter s = queueGrid[col, d];
            Transform slot = GetSlot(col, d);

            if (s == null || slot == null)
            {
                continue;
            }

            DOTween.Kill(s.transform);
            s.transform.DOMove(slot.position, queueMoveDuration);
        }
    }

    private bool TryFindInQueue(Shooter shooter, out int col, out int dep)
    {
        col = -1;
        dep = -1;

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
                    col = c;
                    dep = d;
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsValidSlot(int col, int dep)
    {
        if (slotMatrix == null)
        {
            return false;
        }

        if (col < 0 || col >= columns)
        {
            return false;
        }

        if (dep < 0 || dep >= depthCount)
        {
            return false;
        }

        return true;
    }

    private Transform GetSlot(int col, int dep)
    {
        if (!IsValidSlot(col, dep))
        {
            return null;
        }

        return slotMatrix[col][dep];
    }

    private void PlaceToFrontSlot(Shooter shooter, int index)
    {
        if (shooter == null)
        {
            return;
        }

        if (!shooter.IsAlive)
        {
            FreeFrontReservation(shooter);
            return;
        }

        if (frontSlots == null || index < 0 || index >= frontSlots.Length)
        {
            return;
        }

        Transform slot = frontSlots[index];
        shooter.JumpToFrontSlot(slot.position, null);
    }

    private void EnsureFrontOccupantsSize()
    {
        if (frontSlots == null)
        {
            frontOccupants = new Shooter[0];
            return;
        }

        if (frontOccupants == null || frontOccupants.Length != frontSlots.Length)
        {
            frontOccupants = new Shooter[frontSlots.Length];
        }
    }

    private int GetFirstEmptyFrontSlotIndex()
    {
        EnsureFrontOccupantsSize();

        for (int i = 0; i < frontOccupants.Length; i++)
        {
            if (frontOccupants[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    private void ReserveFrontSlot(Shooter shooter, int index)
    {
        if (index < 0 || index >= frontOccupants.Length)
        {
            return;
        }

        frontOccupants[index] = shooter;
        frontIndexMap[shooter] = index;
    }

    private int GetOrReserveFrontIndex(Shooter shooter)
    {
        if (frontIndexMap.TryGetValue(shooter, out int idx))
        {
            return idx;
        }

        idx = GetFirstEmptyFrontSlotIndex();
        if (idx < 0)
        {
            return -1;
        }

        ReserveFrontSlot(shooter, idx);
        return idx;
    }

    private void FreeFrontReservation(Shooter shooter)
    {
        if (shooter == null)
        {
            return;
        }

        if (frontIndexMap.TryGetValue(shooter, out int idx))
        {
            if (idx >= 0 && idx < frontOccupants.Length)
            {
                if (frontOccupants[idx] == shooter)
                {
                    frontOccupants[idx] = null;
                }
            }

            frontIndexMap.Remove(shooter);
        }
        else
        {
            for (int i = 0; i < frontOccupants.Length; i++)
            {
                if (frontOccupants[i] == shooter)
                {
                    frontOccupants[i] = null;
                }
            }
        }
    }

    private void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            Destroy(t.GetChild(i).gameObject);
        }
    }
}