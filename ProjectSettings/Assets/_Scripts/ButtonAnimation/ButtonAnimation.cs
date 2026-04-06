using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonAnimation : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Animation Settings")]
    [SerializeField] private float pressedScale = 0.9f;
    [SerializeField] private float duration = 0.1f;

    private Vector3 originalScale;
    private Coroutine scaleCoroutine;

    void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleOverTime(originalScale * pressedScale));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleOverTime(originalScale));
    }

    private IEnumerator ScaleOverTime(Vector3 targetScale)
    {
        Vector3 initialScale = transform.localScale;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            // Simple linear interpolation
            transform.localScale = Vector3.Lerp(initialScale, targetScale, t);
            yield return null;
        }

        transform.localScale = targetScale;
    }
}
