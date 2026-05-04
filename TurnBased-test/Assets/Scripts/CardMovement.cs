using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardMovement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float selectScale = 1.30f;
    [SerializeField] private float hoverOffset = 50f;
    [SerializeField] private float hoverSpeed = 10f;
    [SerializeField] private float tilt = 5f;
    
    private int currentState = 0;
    private int originalSiblingIndex;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    
    private Vector3 targetPos;
    private Quaternion targetRot;
    private Vector3 targetScale;

    void Awake() {
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
        targetPos = basePosition;
        targetRot = baseRotation;
        targetScale = Vector3.one;
    }
    
    void Update() {
        float speed = Time.deltaTime * hoverSpeed;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, speed);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRot, speed);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, speed);
        switch (currentState) {
            case 1: 
                targetPos = basePosition + new Vector3(0f, hoverOffset, 0f);
                targetRot = Quaternion.Euler(0f, 0f, tilt);
                targetScale = Vector3.one * selectScale;
                break;
            default: 
                targetPos = basePosition;
                targetRot = baseRotation;
                targetScale = Vector3.one;
                break;
        }
    }

    public void SetBasePosition(Vector3 position, Quaternion rotation) {
        basePosition = position;
        baseRotation = rotation;
    }

    public void OnPointerEnter(PointerEventData eventData) {
        Debug.Log($"[CardMovement] OnPointerEnter on {name}, currentState={currentState}", this);

        if (currentState == 0) {
            currentState = 1;
            originalSiblingIndex = transform.GetSiblingIndex();
            transform.SetAsLastSibling();
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        if (currentState == 1) {
            currentState = 0;
            transform.SetSiblingIndex(originalSiblingIndex);
        }
    }
}
