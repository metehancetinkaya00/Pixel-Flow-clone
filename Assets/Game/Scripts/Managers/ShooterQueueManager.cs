using UnityEngine;
using System.Collections.Generic;

public class ShooterQueueManager : MonoBehaviour
{
    public static ShooterQueueManager Instance { get; private set; }

    [Header("Bottom Slots (Start Queue Positions)")]
    [SerializeField] private Transform[] bottomSlots;

    [Header("Front Slots (Near Grid)")]
    [SerializeField] private Transform[] frontSlots;

    [Header("Path")]
    [SerializeField] private PathDefinition defaultPath;

    [Header("Rotation Lock (Shooter stays in this rotation)")]
    [SerializeField] private Vector3 lockedEuler = Vector3.zero;

    // Queue
    private readonly Queue<Shooter> bottomQueue = new Queue<Shooter>();

    // list for bubble sort
    private readonly List<Shooter> bottomList = new List<Shooter>();

    private void Awake()
    {
        Instance = this;
    }

    public void InitializeBottomQueue(IEnumerable<Shooter> shootersInOrder)
    {
        bottomQueue.Clear();
        bottomList.Clear();

        foreach (var shootorder in shootersInOrder)
        {
            bottomQueue.Enqueue(shootorder);
            bottomList.Add(shootorder);
        }

        SnapBottomToSlots();
    }

    public void TryActivateShooter(Shooter clicked)
    {
        if (clicked == null)
        {
            return;
        }

        if (clicked.IsBusy)
        {
            return;
        }

        if (!bottomList.Contains(clicked))
        {
            return;
        }

        int freeFrontIndex = GetFirstEmptyFrontSlotIndex();
        if (freeFrontIndex < 0)
        {
            return;
        }

        bottomList.Remove(clicked);

        RebuildQueueFromList();

        BubbleSortBottomByWorldX();
        SnapBottomToSlots();

        Quaternion lockedRot = Quaternion.Euler(lockedEuler);

        clicked.StartMove(defaultPath, lockedRot, onFinished: () =>
        {
            PlaceToFrontSlot(clicked, freeFrontIndex, lockedRot);
        });
    }

    private void PlaceToFrontSlot(Shooter shooter, int slotIndex, Quaternion lockedRot)
    {
        Transform slot = frontSlots[slotIndex];
        shooter.transform.position = slot.position;
        shooter.transform.rotation = lockedRot;
    }

    private int GetFirstEmptyFrontSlotIndex()
    {
        for (int i = 0; i < frontSlots.Length; i++)
        {
            bool occupied = false;

            Collider[] hits = Physics.OverlapSphere(frontSlots[i].position, 0.15f);
            for (int h = 0; h < hits.Length; h++)
            {
                if (hits[h].GetComponentInParent<Shooter>() != null)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied)
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

    private void RebuildQueueFromList()
    {
        bottomQueue.Clear();
        for (int i = 0; i < bottomList.Count; i++)
        {
            bottomQueue.Enqueue(bottomList[i]);
        }
    }

    private void BubbleSortBottomByWorldX()
    {
        int btmcount = bottomList.Count;

        for (int i = 0; i < btmcount - 1; i++)
        {
            for (int j = 0; j < btmcount - i - 1; j++)
            {
                if (bottomList[j].transform.position.x > bottomList[j + 1].transform.position.x)
                {
                    var tmp = bottomList[j];
                    bottomList[j] = bottomList[j + 1];
                    bottomList[j + 1] = tmp;
                }
            }
        }
    }
}