using System.Collections;
using System.Collections.Generic;
using Pages;
using UnityEngine;

public class HandManager : MonoBehaviour
{
    public DeckManager deckManager;
    public GameObject CardPrefab;
    public Transform handTransform;
    public float fanSpread = 0f;
    public float cardSpacing = 145f;
    public float tilt = 5f;
    private List<GameObject> cardObjects = new List<GameObject>();
    
    public void AddCardToHand(Card cardData) {
        
        //Initiate the cards
        GameObject newCard = Instantiate(CardPrefab, handTransform.position, Quaternion.identity, handTransform);
        cardObjects.Add(newCard);

        //Set the CardData of the initiated card
        newCard.GetComponent<CardDisplay>().cardData = cardData;

        UpdateCardPositions();
    }

    public void OnDrawCardButton() {
        deckManager.DrawCard(this);
    }

    private void UpdateCardPositions() {
        int cardCount = cardObjects.Count;

        if (cardCount == 0) return;

        for (int i = 0; i < cardCount; i++) {
            float angle = (fanSpread * (i - (cardCount - 1) / 2f)) + tilt;
            float horizontalOffset = (cardSpacing * (i - (cardCount - 1) / 2f));

            float normalizedPosition = (2f * i / (cardCount - 1f)) - 1f; //Normalize card position between -1 and 1

            Vector3 targetPos = new Vector3(horizontalOffset, 0f, 0f);
            Quaternion targetRot = Quaternion.Euler(0f, 0f, angle);

            // Let CardMovement handle positioning if present, otherwise set directly.
            CardMovement movement = cardObjects[i].GetComponent<CardMovement>();
            if (movement != null) {
                movement.SetBasePosition(targetPos, targetRot);
            } else {
                cardObjects[i].transform.localPosition = targetPos;
                cardObjects[i].transform.localRotation = targetRot;
            }
        }
    }
}
