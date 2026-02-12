using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Renders a hand/grid of cards by cloning `CardTemplate.uxml` into the `Hand` container.
/// This version uses the simple flex-wrap layout (no fan/overlap).
/// </summary>
[RequireComponent(typeof(UIDocument))]
public sealed class HandView : MonoBehaviour
{
    private const string HandElementName = "Hand";
    private const string CardElementName = "Card";
    private const string CardBgElementName = "CardBg";
    private const string CardOverlayElementName = "CardOverlay";
    private const string CornerTopLeftElementName = "CornerTL";
    private const string CornerBottomRightElementName = "CornerBR";
    private const string TitleElementName = "Title";
    private const string CostIconElementName = "CostIcon";

    private const string HoverClass = "card--hover";
    private const string SelectedClass = "card--selected";

    [Header("Template")]
    [SerializeField] private VisualTreeAsset? cardTemplate;

    [Header("Selection")]
    [SerializeField] private bool singleSelect = true;

    [Header("Hand Background")]
    [Tooltip("Sprite used as the background of the hand container (VisualElement named 'Hand').")]
    [SerializeField] private Sprite? handBackgroundSprite;

    [Header("Card Background")]
    [Tooltip("Base sprite used as the card background (Image named 'CardBg').")]
    [SerializeField] private Sprite? cardBackgroundSprite;

    [Header("Card Overlay")]
    [Tooltip("Overlay sprite drawn on top of the card background (Image named 'CardOverlay').")]
    [SerializeField] private Sprite? cardOverlaySprite;

    [Header("Card Corners")]
    [Tooltip("Top-left corner sprite (assign DiceCard_White_Merge (2)_10).")]
    [SerializeField] private Sprite? cornerTopLeftSprite;

    [Tooltip("Bottom-right corner sprite (assign DiceCard_White_Merge (2)_14).")]
    [SerializeField] private Sprite? cornerBottomRightSprite;

    [Header("Light Cost Sprites")]
    [Tooltip("Assign 10 sprites in this exact order:\n" +
             "Index 0 = CardCostFont (1)_0 (Light Cost 1)\n" +
             "Index 1 = CardCostFont (1)_1 (Light Cost 2)\n" +
             "...\n" +
             "Index 8 = CardCostFont (1)_8 (Light Cost 9)\n" +
             "Index 9 = CardCostFont (1)_9 (Light Cost 0)")]
    [SerializeField] private List<Sprite> lightCostSprites = new();

    [Header("Demo Data (replace with your real deck/hand)")]
    [SerializeField] private List<CardData> demoHand = new()
    {
        new CardData("Rip Space", 3, "On Hit: ..."),
        new CardData("Slash", 1, "Deal 2 damage."),
        new CardData("Guard", 1, "Gain 1 Block."),
    };

    private VisualElement? _hand;
    private readonly List<VisualElement> _cards = new();

    private void OnEnable()
    {
        if (cardTemplate == null)
        {
            Debug.LogError($"`{nameof(HandView)}` is missing a card template. Assign `Assets/UI/CardTemplate.uxml`.");
            return;
        }

        var root = GetComponent<UIDocument>().rootVisualElement;
        _hand = root.Q<VisualElement>(HandElementName);
        if (_hand == null)
        {
            Debug.LogError($"`{nameof(HandView)}` could not find a VisualElement named '{HandElementName}'. " +
                           "Make sure your UIDocument Source Asset is `Assets/UI/HandScreen.uxml` (or that your container is named correctly).");
            return;
        }

        // Apply hand background (if assigned).
        SetSpriteAsBackground(_hand, handBackgroundSprite);

        Rebuild(demoHand);
    }

    /// <summary>Clears and rebuilds the UI from the provided card list.</summary>
    public void Rebuild(IReadOnlyList<CardData> cards)
    {
        if (_hand == null) return;
        if (cardTemplate == null) return;

        _hand.Clear();
        _cards.Clear();

        for (var i = 0; i < cards.Count; i++)
        {
            var data = cards[i];

            // Clone the template and add it to the hand container.
            var instance = cardTemplate.CloneTree();
            _hand.Add(instance);

            // Find the actual card root element (named "Card") inside the clone.
            var cardRoot = instance.Q<VisualElement>(CardElementName) ?? instance;
            _cards.Add(cardRoot);

            // Background layers.
            SetImageSprite(instance.Q<Image>(CardBgElementName), cardBackgroundSprite);
            SetImageSprite(instance.Q<Image>(CardOverlayElementName), cardOverlaySprite);

            // Corner decorations.
            SetImageSprite(instance.Q<Image>(CornerTopLeftElementName), cornerTopLeftSprite);
            SetImageSprite(instance.Q<Image>(CornerBottomRightElementName), cornerBottomRightSprite);

            // Text / cost.
            var title = instance.Q<Label>(TitleElementName);
            if (title != null) title.text = data.Title;

            ApplyLightCost(instance.Q<Image>(CostIconElementName), data.Cost);

            // Interaction: make focusable for keyboard/gamepad navigation.
            cardRoot.focusable = true;
            cardRoot.tabIndex = 0;

            // Store selection state per-card.
            cardRoot.userData = false; // selected?

            // Click / hover.
            cardRoot.AddManipulator(new Clickable(() => OnCardClicked(cardRoot)));
            cardRoot.RegisterCallback<PointerEnterEvent>(_ => cardRoot.AddToClassList(HoverClass));
            cardRoot.RegisterCallback<PointerLeaveEvent>(_ => cardRoot.RemoveFromClassList(HoverClass));
        }
    }

    private void OnCardClicked(VisualElement card)
    {
        if (singleSelect)
        {
            for (var i = 0; i < _cards.Count; i++)
            {
                var other = _cards[i];
                if (other == card) continue;

                other.userData = false;
                other.RemoveFromClassList(SelectedClass);
            }
        }

        var selected = card.userData is bool b && b;
        selected = !selected;
        card.userData = selected;

        if (selected) card.AddToClassList(SelectedClass);
        else card.RemoveFromClassList(SelectedClass);
    }

    private void ApplyLightCost(Image? costIcon, int lightCost)
    {
        SetImageSprite(costIcon, GetLightCostSprite(lightCost));
    }

    private Sprite? GetLightCostSprite(int lightCost)
    {
        // Valid costs are 0-9 based on user mapping.
        if (lightCost < 0 || lightCost > 9) return null;
        if (lightCostSprites == null || lightCostSprites.Count < 10) return null;

        // Mapping:
        // 1 -> _0, 2 -> _1, ... 9 -> _8, 0 -> _9
        var index = lightCost == 0 ? 9 : lightCost - 1;
        return lightCostSprites[index];
    }

    /// <summary>
    /// Sets a Sprite as a VisualElement background.
    /// Uses Background.FromSprite(Sprite) when available (atlas/sliced-safe), otherwise falls back to sprite.texture.
    /// </summary>
    private static void SetSpriteAsBackground(VisualElement target, Sprite? sprite)
    {
        if (sprite == null)
        {
            target.style.backgroundImage = StyleKeyword.None;
            return;
        }

        var fromSprite = typeof(Background).GetMethod(
            "FromSprite",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(Sprite) },
            modifiers: null);

        if (fromSprite != null)
        {
            var bgObj = fromSprite.Invoke(null, new object[] { sprite });
            if (bgObj is Background bg)
            {
                target.style.backgroundImage = new StyleBackground(bg);
                return;
            }
        }

        // Fallback: uses the whole texture (not correct for atlased sprites).
        target.style.backgroundImage = new StyleBackground(sprite.texture);
    }

    /// <summary>
    /// Sets a Sprite on a UI Toolkit Image element.
    /// Uses Image.sprite when available; otherwise falls back to Image.image (Texture2D).
    /// </summary>
    private static void SetImageSprite(Image? image, Sprite? sprite)
    {
        if (image == null) return;

        if (sprite == null)
        {
            image.style.display = DisplayStyle.None;
            return;
        }

        image.style.display = DisplayStyle.Flex;

        var spriteProp = typeof(Image).GetProperty("sprite", BindingFlags.Public | BindingFlags.Instance);
        if (spriteProp != null && spriteProp.PropertyType == typeof(Sprite) && spriteProp.CanWrite)
            spriteProp.SetValue(image, sprite);
        else
            image.image = sprite.texture;

        image.scaleMode = ScaleMode.ScaleToFit;
    }

    [System.Serializable]
    public sealed class CardData
    {
        public string Title;
        public int Cost;
        [TextArea] public string Rules;

        public CardData(string title, int cost, string rules)
        {
            Title = title;
            Cost = cost;
            Rules = rules;
        }
    }
}

