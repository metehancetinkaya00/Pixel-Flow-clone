using UnityEngine;
using System.Collections.Generic;

public class ShooterQueueManager : MonoBehaviour
{
    public static ShooterQueueManager Instance { get; private set; }

    [SerializeField] private Transform[] bottomSlots;
    [SerializeField] private Transform[] frontSlots;
    [SerializeField] private PathDefinition defaultPath;

    private readonly List<Shooter> bottomList = new List<Shooter>();
    private readonly List<Shooter> frontList = new List<Shooter>();

    private readonly Shooter[] frontOccupants = new Shooter[256];

    private void Awake()
    {
        Instance = this;
    }

    public void InitializeBottomQueue(IEnumerable<Shooter> shootersInOrder)
    {
        bottomList.Clear();
        frontList.Clear();

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

            bottomList.Add(s);
        }

        SnapBottomToSlots();
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

        bool isBottom = bottomList.Contains(clicked);
        bool isFront = frontList.Contains(clicked);

        if (!isBottom && !isFront)
        {
            return;
        }

        int freeFrontIndex = GetFirstEmptyFrontSlotIndex();
        if (freeFrontIndex < 0)
        {
            return;
        }

        if (isBottom)
        {
            bottomList.Remove(clicked);
            SnapBottomToSlots();
        }

        if (isFront)
        {
            int currentFrontIndex = FindFrontSlotIndexOf(clicked);
            if (currentFrontIndex >= 0 && currentFrontIndex < frontSlots.Length)
            {
                frontOccupants[currentFrontIndex] = null;
            }
        }

        if (!frontList.Contains(clicked))
        {
            frontList.Add(clicked);
        }

        clicked.StartMove(defaultPath, () =>
        {
            PlaceToFrontSlot(clicked, freeFrontIndex);
        });
    }

    private void PlaceToFrontSlot(Shooter shooter, int slotIndex)
    {
        if (shooter == null)
        {
            return;
        }

        if (!shooter.IsAlive)
        {
            return;
        }

        if (slotIndex < 0 || slotIndex >= frontSlots.Length)
        {
            return;
        }

        Transform slot = frontSlots[slotIndex];
        shooter.transform.position = slot.position;

        frontOccupants[slotIndex] = shooter;

        if (!frontList.Contains(shooter))
        {
            frontList.Add(shooter);
        }
    }

    private int FindFrontSlotIndexOf(Shooter shooter)
    {
        for (int i = 0; i < frontSlots.Length; i++)
        {
            if (frontOccupants[i] == shooter)
            {
                return i;
            }
        }

        return -1;
    }

    private int GetFirstEmptyFrontSlotIndex()
    {
        for (int i = 0; i < frontSlots.Length; i++)
        {
            if (frontOccupants[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    private void SnapBottomToSlots()
    {
        for (int i = 0; i < bottomSlots.Length; i++)
        {
            if (i >= bottomList.Count)
            {
                break;
            }

            bottomList[i].transform.position = bottomSlots[i].position;
        }
    }

    public void NotifyShooterDestroyed(Shooter shooter)
    {
        if (shooter == null)
        {
            return;
        }

        if (bottomList.Contains(shooter))
        {
            bottomList.Remove(shooter);
            SnapBottomToSlots();
        }

        if (frontList.Contains(shooter))
        {
            frontList.Remove(shooter);
        }

        for (int i = 0; i < frontSlots.Length; i++)
        {
            if (frontOccupants[i] == shooter)
            {
                frontOccupants[i] = null;
            }
        }
    }

    public bool CanShooterShoot(Shooter shooter)
    {
        if (shooter == null)
        {
            return false;
        }

        if (!shooter.IsAlive)
        {
            return false;
        }

        if (shooter.shotsRemaining <= 0)
        {
            return false;
        }

        return true;
    }
}