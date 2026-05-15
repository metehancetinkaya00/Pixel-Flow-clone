using DG.Tweening;
using UnityEngine;

public class ShootersPlate : MonoBehaviour
{
    private Transform cachedTransform;

    private void Awake()
    {
        cachedTransform = transform;
    }

  
    public void AttachToShooter(Transform anchor, Vector3 localOffset, Vector3 localRotationEuler, float duration)
    {
        if (anchor == null) return;

        DOTween.Kill(cachedTransform);
        cachedTransform.SetParent(anchor, worldPositionStays: true);

        Quaternion targetRotation = Quaternion.Euler(localRotationEuler);

        if (duration <= 0f)
        {
            cachedTransform.localPosition = localOffset;
            cachedTransform.localRotation = targetRotation;
            return;
        }

        DOTween.Sequence()
            .Join(cachedTransform.DOLocalMove(localOffset, duration).SetEase(Ease.OutQuad))
            .Join(cachedTransform.DOLocalRotateQuaternion(targetRotation, duration).SetEase(Ease.OutQuad));
    }


    public void MoveToSplineStart(Vector3 worldTarget, Quaternion worldRotation, float duration)
    {
        DOTween.Kill(cachedTransform);
        cachedTransform.SetParent(null, worldPositionStays: true);

        if (duration <= 0f)
        {
            cachedTransform.SetPositionAndRotation(worldTarget, worldRotation);
            return;
        }

        DOTween.Sequence()
            .Join(cachedTransform.DOMove(worldTarget, duration).SetEase(Ease.OutQuad))
            .Join(cachedTransform.DORotateQuaternion(worldRotation, duration).SetEase(Ease.OutQuad))
            .OnComplete(() => cachedTransform.SetPositionAndRotation(worldTarget, worldRotation));
    }


    public void MoveToLine(Transform lineRoot, Vector3 worldTarget, Quaternion worldRotation, float duration, System.Action onComplete)
    {
        DOTween.Kill(cachedTransform);
        cachedTransform.SetParent(null, worldPositionStays: true);

        if (duration <= 0f)
        {
            cachedTransform.SetPositionAndRotation(worldTarget, worldRotation);
            cachedTransform.SetParent(lineRoot, worldPositionStays: true);
            onComplete?.Invoke();
            return;
        }

        DOTween.Sequence()
            .Join(cachedTransform.DOMove(worldTarget, duration).SetEase(Ease.OutQuad))
            .Join(cachedTransform.DORotateQuaternion(worldRotation, duration).SetEase(Ease.OutQuad))
            .OnComplete(() =>
            {
                cachedTransform.SetPositionAndRotation(worldTarget, worldRotation);
                cachedTransform.SetParent(lineRoot, worldPositionStays: true);
                onComplete?.Invoke();
            });
    }
}
