using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Starting shooters in order (left to right)")]
    [SerializeField] private Shooter[] startingShootersInOrder;

    private void Start()
    {
        ShooterQueueManager.Instance.InitializeBottomQueue(startingShootersInOrder);
    }
}