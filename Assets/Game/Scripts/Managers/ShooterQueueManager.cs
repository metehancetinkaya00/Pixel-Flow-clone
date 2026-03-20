using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class ShooterQueueManager : MonoBehaviour
{
    [System.Serializable]
    public class QueueColumn
    {
        public Transform[] slots;
    }

    public static ShooterQueueManager Instance;

    public QueueColumn[] queueColumns;

    public Transform[] frontSlots;

    public SplinePathDefinition defaultSplinePath;

    public int selectableHeadColumnsCount = 0;
    public float queueMoveDuration = 0.15f;

    private List<Shooter>[] columnLists;

    private readonly List<Shooter> frontList = new List<Shooter>();
    private Shooter[] frontOccupants;
    private Dictionary<Shooter, int> frontIndexMap = new Dictionary<Shooter, int>();

    private void Awake()
    {
        Instance = this;
        EnsureFrontOccupantsSize();
        RebuildColumns();
    }

    public Vector3 GetQueueSpawnPosition()
    {
        if (queueColumns == null)
        {
            return Vector3.zero;
        }

        for (int c = 0; c < queueColumns.Length; c++)
        {
            if (queueColumns[c] == null)
            {
                continue;
            }

            if (queueColumns[c].slots == null)
            {
                continue;
            }

            if (queueColumns[c].slots.Length <= 0)
            {
                continue;
            }

            if (queueColumns[c].slots[0] == null)
            {
                continue;
            }

            return queueColumns[c].slots[0].position;
        }

        return Vector3.zero;
    }

    public void InitializeQueue(Shooter[] shootersInOrder)
    {
        EnsureFrontOccupantsSize();
        RebuildColumns();
        ClearColumns();

        if (shootersInOrder == null)
        {
            return;
        }

        int idx = 0;

        for (int c = 0; c < GetColumnCount(); c++)
        {
            int cap = GetColumnCapacity(c);

            for (int d = 0; d < cap; d++)
            {
                if (idx >= shootersInOrder.Length)
                {
                    break;
                }

                Shooter s = shootersInOrder[idx];
                idx += 1;

                if (s == null)
                {
                    continue;
                }

                columnLists[c].Add(s);
            }
        }

        SnapAllColumns();
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

        if (clicked.shotsRemaining <= 0)
        {
            clicked.DestroySelf();
            return;
        }

        if (defaultSplinePath == null || defaultSplinePath.splineContainer == null)
        {
            return;
        }

        EnsureFrontOccupantsSize();
        RebuildColumnsIfNeeded();

        bool inQueue = TryFindInQueue(clicked, out int col, out int depth);
        bool inFront = frontList.Contains(clicked);

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

            if (selectableHeadColumnsCount > 0)
            {
                if (col >= selectableHeadColumnsCount)
                {
                    return;
                }
            }

            int reservedFrontIndex = GetFirstEmptyFrontSlotIndex();
            if (reservedFrontIndex < 0)
            {
                return;
            }

            ReserveFrontSlot(clicked, reservedFrontIndex);

            columnLists[col].RemoveAt(0);
            AnimateColumn(col);

            clicked.StartMoveOnSpline(defaultSplinePath, () =>
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

            clicked.StartMoveOnSpline(defaultSplinePath, () =>
            {
                PlaceToFrontSlot(clicked, frontIndex);
            });

            return;
        }
    }

    public void NotifyShooterDestroyed(Shooter shooter)
    {
        if (shooter == null)
        {
            return;
        }

        EnsureFrontOccupantsSize();
        RebuildColumnsIfNeeded();

        bool inQueue = TryFindInQueue(shooter, out int col, out int depth);

        if (inQueue)
        {
            columnLists[col].RemoveAt(depth);
            AnimateColumn(col);
        }

        if (frontList.Contains(shooter))
        {
            frontList.Remove(shooter);
        }

        FreeFrontReservation(shooter);
    }

    private void SnapAllColumns()
    {
        for (int c = 0; c < GetColumnCount(); c++)
        {
            SnapColumn(c);
        }
    }

    private void SnapColumn(int col)
    {
        if (!IsValidColumn(col))
        {
            return;
        }

        List<Shooter> list = columnLists[col];
        int cap = GetColumnCapacity(col);

        int count = list.Count;
        if (count > cap)
        {
            count = cap;
        }

        for (int i = 0; i < count; i++)
        {
            Shooter s = list[i];
            Transform slot = GetSlot(col, i);

            if (s == null || slot == null)
            {
                continue;
            }

            DOTween.Kill(s.transform);
            s.transform.position = slot.position;
        }
    }

    private void AnimateColumn(int col)
    {
        if (!IsValidColumn(col))
        {
            return;
        }

        List<Shooter> list = columnLists[col];
        int cap = GetColumnCapacity(col);

        int count = list.Count;
        if (count > cap)
        {
            count = cap;
        }

        for (int i = 0; i < count; i++)
        {
            Shooter s = list[i];
            Transform slot = GetSlot(col, i);

            if (s == null || slot == null)
            {
                continue;
            }

            DOTween.Kill(s.transform);
            s.transform.DOMove(slot.position, queueMoveDuration);
        }
    }

    private bool TryFindInQueue(Shooter shooter, out int col, out int depth)
    {
        col = -1;
        depth = -1;

        if (columnLists == null)
        {
            return false;
        }

        for (int c = 0; c < columnLists.Length; c++)
        {
            List<Shooter> list = columnLists[c];
            if (list == null)
            {
                continue;
            }

            for (int d = 0; d < list.Count; d++)
            {
                if (list[d] == shooter)
                {
                    col = c;
                    depth = d;
                    return true;
                }
            }
        }

        return false;
    }

    private void RebuildColumns()
    {
        int cols = GetColumnCount();

        if (cols <= 0)
        {
            columnLists = new List<Shooter>[0];
            return;
        }

        columnLists = new List<Shooter>[cols];

        for (int i = 0; i < cols; i++)
        {
            columnLists[i] = new List<Shooter>();
        }
    }

    private void RebuildColumnsIfNeeded()
    {
        int cols = GetColumnCount();

        if (cols <= 0)
        {
            columnLists = new List<Shooter>[0];
            return;
        }

        if (columnLists == null || columnLists.Length != cols)
        {
            RebuildColumns();
        }
    }

    private void ClearColumns()
    {
        if (columnLists == null)
        {
            return;
        }

        for (int i = 0; i < columnLists.Length; i++)
        {
            if (columnLists[i] == null)
            {
                continue;
            }

            columnLists[i].Clear();
        }
    }

    private int GetColumnCount()
    {
        if (queueColumns == null)
        {
            return 0;
        }

        return queueColumns.Length;
    }

    private int GetColumnCapacity(int col)
    {
        if (!IsValidColumn(col))
        {
            return 0;
        }

        Transform[] slots = queueColumns[col].slots;

        if (slots == null)
        {
            return 0;
        }

        return slots.Length;
    }

    private bool IsValidColumn(int col)
    {
        if (queueColumns == null)
        {
            return false;
        }

        if (col < 0 || col >= queueColumns.Length)
        {
            return false;
        }

        if (queueColumns[col] == null)
        {
            return false;
        }

        if (queueColumns[col].slots == null)
        {
            return false;
        }

        return true;
    }

    private Transform GetSlot(int col, int depth)
    {
        if (!IsValidColumn(col))
        {
            return null;
        }

        Transform[] slots = queueColumns[col].slots;

        if (depth < 0 || depth >= slots.Length)
        {
            return null;
        }

        return slots[depth];
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

        if (!frontList.Contains(shooter))
        {
            frontList.Add(shooter);
        }
    }

    private int GetOrReserveFrontIndex(Shooter shooter)
    {
        if (frontIndexMap.ContainsKey(shooter))
        {
            return frontIndexMap[shooter];
        }

        int idx = GetFirstEmptyFrontSlotIndex();
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

        if (frontIndexMap.ContainsKey(shooter))
        {
            int idx = frontIndexMap[shooter];

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
}