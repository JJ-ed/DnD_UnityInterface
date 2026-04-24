using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pages;

public class CardDisplay : MonoBehaviour
{
    public Card cardData;
    public Image cardImage;
    public TextMeshProUGUI cardName;
    public Image[] range;
    public Image[] lightCost;

    void Start() {
        UpdateCardDisplay();
    }

    public void UpdateCardDisplay() {
        cardName.text = cardData.cardName;
        
        for (int i = 0; i < range.Length; i++) {
            range[i].enabled = i == (int)cardData.range;
        }       
        
        for (int i = 0; i < lightCost.Length; i++) {
            lightCost[i].enabled = i == cardData.lightCost;
        }
    }
}

