using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pages;

public class DeckManager : MonoBehaviour
{
   public List<Card> allCards = new List<Card>();

   private int currentCardIndex = 0;


   void Start() {
    //Load all card assets from the Resources folder
    Card[] cards = Resources.LoadAll<Card>("Cards");

    // Add the loaded cards to the allCards list
    allCards.AddRange(cards);   
   }

   public void DrawCard(HandManager handManager) {
    if (allCards.Count == 0) 
    return;
    
    Card nextCard = allCards[currentCardIndex];
    handManager.AddCardToHand(nextCard);
    currentCardIndex = (currentCardIndex + 1) % allCards.Count;
   }
}
