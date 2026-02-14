using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;


// - The card is a VisualElement and a USS class for hover/selected.
[RequireComponent(typeof(UIDocument))]
public sealed class CardSelectView : MonoBehaviour
{
    // UXML element names
    private const string CardElementName = "Card";
    private const string CardBgElementName = "CardBg";
    private const string CardOverlayElementName = "CardOverlay";
    private const string CornerTopLeftElementName = "CornerTL";
    private const string CornerBottomRightElementName = "CornerBR";
    private const string TitleElementName = "Title";
    private const string CostIconElementName = "CostIcon";

    // USS classes toggled by this component
    private const string HoverClass = "card--hover";
    private const string SelectedClass = "card--selected";

    [Header("Behavior")]
    [SerializeField] private bool toggleSelectedOnClick = true;

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

    [Header("Binding")]
    [Tooltip("How many frames to retry binding if the Card element isn't present yet.")]
    [SerializeField] private int bindRetryFrames = 10;
    [Tooltip("If a `HandView` is on the same GameObject, CardSelectView will not bind (hand is managed by HandView).")]
    [SerializeField] private bool disableWhenHandViewPresent = true;
    [Tooltip("If the UIDocument looks like the hand screen (has 'Hand' but no 'Card'), CardSelectView will no-op.")]
    [SerializeField] private bool disableWhenHandRootPresent = true;

    private VisualElement? _card;
    private Image? _cardBg;
    private Image? _cardOverlay;
    private Image? _cornerTl;
    private Image? _cornerBr;
    private Label? _title;
    private Image? _costIcon;

    public bool Selected { get; private set; }

    /// <summary>Raised when the card is clicked (after selection toggle, if enabled).</summary>
    public event Action<CardSelectView>? Clicked;

    private int _remainingBindRetries;
    private bool _bound;
    private bool _loggedMissingOnce;

    private void OnEnable()
    {
        if (disableWhenHandViewPresent && TryGetComponent<HandView>(out _))
        {
            // HandView manages multiple cards; this view is for the single-card UXML.
            return;
        }

        var uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        // If we're on a screen that contains the hand container, don't try to bind to a single card.
        if (disableWhenHandRootPresent && root.Q<VisualElement>("Hand") != null && root.Q<VisualElement>(CardElementName) == null)
            return;

        _remainingBindRetries = Mathf.Max(0, bindRetryFrames);
        _bound = false;
        _loggedMissingOnce = false;

        TryBindOrRetry(root);
    }

    private void OnDisable()
    {
        // NOTE: We don't strictly need to unregister callbacks because the VisualElement tree is owned by the UIDocument,
        // but unregistering is safer if you ever swap UXML at runtime.
        if (_card == null) return;

        _card.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
        _card.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
        _card.UnregisterCallback<KeyDownEvent>(OnKeyDown);

        _bound = false;
    }

    public void SetSelected(bool selected)
    {
        Selected = selected;
        SyncStateClasses();
    }

    public void SetTitle(string title)
    {
        if (_title != null) _title.text = title;
    }

    public void SetCost(int cost)
    {
        ApplyLightCost(cost);
    }

    private void TryBindOrRetry(VisualElement root)
    {
        if (_bound) return;

        if (TryBind(root))
        {
            _bound = true;
            return;
        }

        if (_remainingBindRetries-- > 0)
        {
            // Retry next frame; useful if another component (e.g. HandView) populates the UI after OnEnable.
            root.schedule.Execute(() => TryBindOrRetry(root)).StartingIn(0);
            return;
        }

        // No-op if we couldn't bind. (This component is optional on many screens.)
    }

    private bool TryBind(VisualElement root)
    {
        // Bind ONLY to an explicitly named Card element (single-card UXML).
        _card = root.Q<VisualElement>(CardElementName);

        if (_card == null)
            return false;

        // Query elements relative to the card root (important when there are multiple cards).
        _title = _card.Q<Label>(TitleElementName);
        _costIcon = _card.Q<Image>(CostIconElementName);
        _cardBg = _card.Q<Image>(CardBgElementName);
        _cardOverlay = _card.Q<Image>(CardOverlayElementName);
        _cornerTl = _card.Q<Image>(CornerTopLeftElementName);
        _cornerBr = _card.Q<Image>(CornerBottomRightElementName);

        // Make it keyboard-focusable if you navigate UI with keyboard/gamepad.
        _card.focusable = true;
        _card.tabIndex = 0;

        // Click handling (mouse / touch).
        _card.AddManipulator(new Clickable(OnCardClicked));

        // Hover styling.
        _card.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
        _card.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);

        // Basic keyboard activate (Enter/Space).
        _card.RegisterCallback<KeyDownEvent>(OnKeyDown);

        // Ensure classes reflect current state.
        SyncStateClasses();

        // Apply sprites (if assigned).
        SetImageSprite(_cardBg, cardBackgroundSprite);
        SetImageSprite(_cardOverlay, cardOverlaySprite);
        SetImageSprite(_cornerTl, cornerTopLeftSprite);
        SetImageSprite(_cornerBr, cornerBottomRightSprite);

        return true;
    }

    private void OnCardClicked()
    {
        if (toggleSelectedOnClick) SetSelected(!Selected);
        Clicked?.Invoke(this);
    }

    private void OnPointerEnter(PointerEnterEvent _)
    {
        _card?.AddToClassList(HoverClass);
    }

    private void OnPointerLeave(PointerLeaveEvent _)
    {
        _card?.RemoveFromClassList(HoverClass);
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space)
            return;

        OnCardClicked();
        evt.StopPropagation();
    }

    private void SyncStateClasses()
    {
        if (_card == null) return;

        if (Selected) _card.AddToClassList(SelectedClass);
        else _card.RemoveFromClassList(SelectedClass);
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
        {
            spriteProp.SetValue(image, sprite);
        }
        else
        {
            image.image = sprite.texture;
        }

        // Don't crop the sprite; fit it inside the card bounds.
        image.scaleMode = ScaleMode.ScaleToFit;
    }

    private void ApplyLightCost(int lightCost)
    {
        var sprite = GetLightCostSprite(lightCost);
        SetImageSprite(_costIcon, sprite);
    }

    private Sprite? GetLightCostSprite(int lightCost)
    {
        if (lightCost < 0 || lightCost > 9) return null;
        if (lightCostSprites == null || lightCostSprites.Count < 10) return null;

        var index = lightCost == 0 ? 9 : lightCost - 1;
        return lightCostSprites[index];
    }
}

