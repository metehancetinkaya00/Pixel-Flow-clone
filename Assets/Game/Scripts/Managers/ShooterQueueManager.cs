using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class ShooterQueueManager : MonoBehaviour
{
    public static ShooterQueueManager Instance { get; private set; }

    [SerializeField] private Transform[] queueSlots;
    [SerializeField] private Transform[] frontSlots;
    [SerializeField] private PathDefinition defaultPath;

    [SerializeField] private float queueMoveDuration = 0.15f;

    private readonly List<Shooter> queueList = new List<Shooter>();
    private readonly List<Shooter> frontList = new List<Shooter>();

    private Shooter[] frontOccupants;
    private Dictionary<Shooter, int> frontIndexMap = new Dictionary<Shooter, int>();

    private void Awake()
    {
        Instance = this;
        EnsureFrontOccupantsSize();
    }

    public void InitializeQueue(IEnumerable<Shooter> shootersInOrder)
    {
        queueList.Clear();
        frontList.Clear();
        frontIndexMap.Clear();

        EnsureFrontOccupantsSize();

        for (int i = 0; i < frontOccupants.Length; i++)
        {
            frontOccupants[i] = null;
        }

        foreach (var s in shootersInOrder)
        {
            if (s == null)
            {
                continue;
            }

            queueList.Add(s);
        }

        SnapQueueToSlots();
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

        EnsureFrontOccupantsSize();

        bool isInQueue = queueList.Contains(clicked);
        bool isInFront = frontList.Contains(clicked);

        if (!isInQueue && !isInFront)
        {
            return;
        }

        if (isInQueue)
        {
            if (queueList.Count == 0)
            {
                return;
            }

            if (queueList[0] != clicked)
            {
                return;
            }

            int reservedFrontIndex = GetFirstEmptyFrontSlotIndex();
            if (reservedFrontIndex < 0)
            {
                return;
            }

            ReserveFrontSlot(clicked, reservedFrontIndex);

            queueList.RemoveAt(0);
            AnimateQueueToSlots();

            clicked.StartMove(defaultPath, () =>
            {
                PlaceToFrontSlot(clicked, reservedFrontIndex);
            });

            return;
        }

        if (isInFront)
        {
            int frontIndex = GetOrReserveFrontIndex(clicked);
            if (frontIndex < 0)
            {
                return;
            }

            clicked.StartMove(defaultPath, () =>
            {
                PlaceToFrontSlot(clicked, frontIndex);
            });

            return;
        }
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

        int reservedFrontIndex = GetFirstEmptyFrontSlotIndex();
        if (reservedFrontIndex < 0)
        {
            return -1;
        }

        ReserveFrontSlot(shooter, reservedFrontIndex);
        return reservedFrontIndex;
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

        if (index < 0 || index >= frontSlots.Length)
        {
            return;
        }

        Transform slot = frontSlots[index];
        shooter.transform.position = slot.position;
    }

    private int GetFirstEmptyFrontSlotIndex()
    {
        for (int i = 0; i < frontOccupants.Length; i++)
        {
            if (frontOccupants[i] == null)
            {
                return i;
            }
        }

        return -1;
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

    private void SnapQueueToSlots()
    {
        int count = Mathf.Min(queueList.Count, queueSlots.Length);

        for (int i = 0; i < count; i++)
        {
            Shooter s = queueList[i];
            if (s == null)
            {
                continue;
            }

            DOTween.Kill(s.transform);
            s.transform.position = queueSlots[i].position;
        }
    }

    private void AnimateQueueToSlots()
    {
        int count = Mathf.Min(queueList.Count, queueSlots.Length);

        for (int i = 0; i < count; i++)
        {
            Shooter s = queueList[i];
            if (s == null)
            {
                continue;
            }

            DOTween.Kill(s.transform);
            s.transform.DOMove(queueSlots[i].position, queueMoveDuration);
        }
    }

    public void NotifyShooterDestroyed(Shooter shooter)
    {
        if (shooter == null)
        {
            return;
        }

        if (queueList.Contains(shooter))
        {
            queueList.Remove(shooter);
            AnimateQueueToSlots();
        }

        if (frontList.Contains(shooter))
        {
            frontList.Remove(shooter);
        }

        FreeFrontReservation(shooter);
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