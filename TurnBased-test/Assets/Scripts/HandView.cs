using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

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

    [Header("Template")]
    [SerializeField] private VisualTreeAsset? cardTemplate;

    [Header("Hand Layout")]
    [Tooltip("Negative margin (px) between cards. More negative = more overlap.")]
    [SerializeField] private float cardOverlapMargin = -20f;

    [Tooltip("Maximum rotation (degrees) at the outermost cards. Center card stays upright (0).")]
    [SerializeField] private float maxFanAngle = 5f;

    [Header("Hand Background")]
    [Tooltip("Sprite used as the background of the hand container.")]
    [SerializeField] private Sprite? handBackgroundSprite;

    [Header("Card Background")]
    [Tooltip("Base sprite used as the card background.")]
    [SerializeField] private Sprite? cardBackgroundSprite;

    [Header("Card Overlay")]
    [Tooltip("Overlay sprite drawn on top of the card background.")]
    [SerializeField] private Sprite? cardOverlaySprite;

    [Header("Card Corners")]
    [Tooltip("Top-left corner sprite")]
    [SerializeField] private Sprite? cornerTopLeftSprite;

    [Tooltip("Bottom-right corner sprite")]
    [SerializeField] private Sprite? cornerBottomRightSprite;

    [Header("Light Cost Sprites")]
    [SerializeField] private List<Sprite> lightCostSprites = new();

    [Header("Demo Data (replace with your real deck/hand)")]
    [SerializeField] private List<CardData> demoHand = new()
    {
        new CardData("Rip Space", 3, "On Hit: ..."),
        new CardData("Slash", 1, "Deal 2 damage."),
        new CardData("Guard", 1, "Gain 1 Block."),
    };

    private VisualElement? _hand;

    private void OnEnable()
    {

        var root = GetComponent<UIDocument>().rootVisualElement;
        _hand = root.Q<VisualElement>(HandElementName);

        // Apply hand background (if assigned).
        SetSpriteAsBackground(_hand, handBackgroundSprite);

        Rebuild(demoHand);
    }

    //Clears and rebuilds the UI from the provided card list.
    public void Rebuild(IReadOnlyList<CardData> cards)
    {
        if (_hand == null) return;
        if (cardTemplate == null) return;

        _hand.Clear();

        var count = cards.Count;

        for (var i = 0; i < count; i++)
        {
            var data = cards[i];

            // Clone the template and add it to the hand container.
            var instance = cardTemplate.CloneTree();

            // --- Overlap: negative left margin (skip first card) ---
            if (i > 0)
                instance.style.marginLeft = new StyleLength(cardOverlapMargin);

            // --- Uniform rotation: all cards tilt the same direction ---
            instance.style.transformOrigin = new StyleTransformOrigin(
                new TransformOrigin(
                    new Length(50, LengthUnit.Percent),
                    new Length(100, LengthUnit.Percent)));
            instance.style.rotate = new StyleRotate(new Rotate(new Angle(maxFanAngle, AngleUnit.Degree)));

            _hand.Add(instance);

            var cardRoot = instance.Q<VisualElement>(CardElementName) ?? instance;
            cardRoot.pickingMode = PickingMode.Position;

            var cardBg = instance.Q<Image>(CardBgElementName);
            SetImageSprite(cardBg, cardBackgroundSprite);
            if (cardBg != null) cardBg.pickingMode = PickingMode.Ignore;

            var cardOverlay = instance.Q<Image>(CardOverlayElementName);
            SetImageSprite(cardOverlay, cardOverlaySprite);
            if (cardOverlay != null) cardOverlay.pickingMode = PickingMode.Ignore;

            var cornerTl = instance.Q<Image>(CornerTopLeftElementName);
            SetImageSprite(cornerTl, cornerTopLeftSprite);
            if (cornerTl != null) cornerTl.pickingMode = PickingMode.Ignore;

            var cornerBr = instance.Q<Image>(CornerBottomRightElementName);
            SetImageSprite(cornerBr, cornerBottomRightSprite);
            if (cornerBr != null) cornerBr.pickingMode = PickingMode.Ignore;

            var title = instance.Q<Label>(TitleElementName);
            if (title != null)
            {
                title.text = data.Title;
                title.pickingMode = PickingMode.Ignore;
            }

            var costIcon = instance.Q<Image>(CostIconElementName);
            ApplyLightCost(costIcon, data.Cost);
            if (costIcon != null) costIcon.pickingMode = PickingMode.Ignore;

            // Interaction: make focusable for keyboard/gamepad navigation.
            cardRoot.focusable = true;
            cardRoot.tabIndex = 0;
        }
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

        var index = lightCost == 0 ? 9 : lightCost - 1;
        return lightCostSprites[index];
    }

    // Sets a Sprite as a VisualElement background.
    // Uses Background. FromSprite(Sprite) when available (atlas/sliced-safe), otherwise falls back to sprite.texture.
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

    // Sets a Sprite on a UI Toolkit Image element.
    // Uses Image.sprite when available; otherwise falls back to Image.image (Texture2D).
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

