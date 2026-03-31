using DG.Tweening;
using UnityEngine;

public class ShootersPlate : MonoBehaviour
{
    private Transform cachedTransform;

    private void Awake()
    {
        cachedTransform = transform;
    }

    public void AttachToShooter(
        Transform anchor,
        Vector3 localOffset,
        Vector3 localRotationEuler,
        float duration)
    {
        if (cachedTransform == null)
        {
            cachedTransform = transform;
        }

        if (anchor == null)
        {
            return;
        }

        DOTween.Kill(cachedTransform);
        cachedTransform.SetParent(anchor, true);

        Quaternion targetRotation = Quaternion.Euler(localRotationEuler);

        if (duration <= 0f)
        {
            cachedTransform.localPosition = localOffset;
            cachedTransform.localRotation = targetRotation;
            return;
        }

        Sequence seq = DOTween.Sequence();
        seq.Join(cachedTransform.DOLocalMove(localOffset, duration).SetEase(Ease.OutQuad));
        seq.Join(cachedTransform.DOLocalRotateQuaternion(targetRotation, duration).SetEase(Ease.OutQuad));
    }

    public void MoveToSplineStart(
        Vector3 worldTarget,
        Quaternion worldRotation,
        float duration,
        Vector3 spinEuler)
    {
        if (cachedTransform == null)
        {
            cachedTransform = transform;
        }

        DOTween.Kill(cachedTransform);
        cachedTransform.SetParent(null, true);

        if (duration <= 0f)
        {
            cachedTransform.position = worldTarget;
            cachedTransform.rotation = worldRotation;
            return;
        }

        Vector3 targetEuler = worldRotation.eulerAngles + spinEuler;

        Sequence seq = DOTween.Sequence();
        seq.Join(cachedTransform.DOMove(worldTarget, duration).SetEase(Ease.OutQuad));
        seq.Join(cachedTransform.DORotate(targetEuler, duration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
        seq.OnComplete(() =>
        {
            cachedTransform.position = worldTarget;
            cachedTransform.rotation = worldRotation;
        });
    }

    public void MoveToLine(
        Transform lineRoot,
        Vector3 worldTarget,
        Quaternion worldRotation,
        float duration,
        Vector3 spinEuler,
        System.Action onComplete)
    {
        if (cachedTransform == null)
        {
            cachedTransform = transform;
        }

        DOTween.Kill(cachedTransform);
        cachedTransform.SetParent(null, true);

        if (duration <= 0f)
        {
            cachedTransform.position = worldTarget;
            cachedTransform.rotation = worldRotation;

            if (lineRoot != null)
            {
                cachedTransform.SetParent(lineRoot, true);
            }

            onComplete?.Invoke();
            return;
        }

        Vector3 targetEuler = worldRotation.eulerAngles + spinEuler;

        Sequence seq = DOTween.Sequence();
        seq.Join(cachedTransform.DOMove(worldTarget, duration).SetEase(Ease.OutQuad));
        seq.Join(cachedTransform.DORotate(targetEuler, duration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
        seq.OnComplete(() =>
        {
            cachedTransform.position = worldTarget;
            cachedTransform.rotation = worldRotation;

            if (lineRoot != null)
            {
                cachedTransform.SetParent(lineRoot, true);
            }

            onComplete?.Invoke();
        });
    }
}