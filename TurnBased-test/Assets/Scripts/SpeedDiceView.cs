using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class SpeedDiceView : MonoBehaviour
{
    // UXML element names
    private const string SpeedDiceElementName = "SpeedDice";
    private const string DiceBackgroundElementName = "DiceBackground";
    private const string DiceElementName = "Dice";
    private const string DiceLinesElementName = "DiceLines";

    [Header("World Target")]
    [Tooltip("World-space Transform to track (typically the Player).")]
    [SerializeField] private Transform? target;

    [Tooltip("World offset added to the target position")]
    [SerializeField] private Vector3 worldOffset = new(0f, 1.8f, 0f);

    [Header("Sprites")]
    [Tooltip("Dice background")]
    [SerializeField] private Sprite? diceBackgroundSprite;

    [Tooltip("Dice")]
    [SerializeField] private Sprite? diceSprite;

    [Tooltip("Dice lines")]
    [SerializeField] private Sprite? diceLinesSprite;

    private const bool HideWhenBehindCamera = true;
    private const int BindRetryFrames = 10;

    private VisualElement? _diceRoot;
    private Image? _diceBackground;
    private Image? _dice;
    private Image? _diceLines;

    private VisualElement? _uiRoot;
    private int _remainingBindRetries;
    private bool _bound;

    private void OnEnable()
    {
        _remainingBindRetries = BindRetryFrames;
        _bound = false;

        var root = GetComponent<UIDocument>().rootVisualElement;
        _uiRoot = root;

        // Stretch the root to full screen so hit testing can reach absolute children.
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.pickingMode = PickingMode.Ignore;

        TryBindOrRetry();
    }

    private void Update()
    {
        if (!_bound || _diceRoot == null) return;

        // Keep sprites in sync (supports live tweaking in Inspector).
        SetImageSprite(_diceBackground, diceBackgroundSprite);
        SetImageSprite(_dice, diceSprite);
        SetImageSprite(_diceLines, diceLinesSprite);

        var cam = Camera.main;
        if (cam == null || target == null)
        {
            _diceRoot.style.display = DisplayStyle.None;
            return;
        }

        var worldPos = target.position + worldOffset;
        var screenPos = cam.WorldToScreenPoint(worldPos);

        if (HideWhenBehindCamera && screenPos.z < 0f)
        {
            _diceRoot.style.display = DisplayStyle.None;
            return;
        }

        if (_diceRoot.panel == null)
            return; // panel not ready yet; TryBindOrRetry will keep attempting.

        _diceRoot.style.display = DisplayStyle.Flex;

        // Convert Unity screen coords (origin bottom-left) into panel coords.
        var panelPos = RuntimePanelUtils.ScreenToPanel(_diceRoot.panel, new Vector2(screenPos.x, screenPos.y));

        // Place the element centered horizontally, sitting above the point.
        _diceRoot.style.left = panelPos.x;
        _diceRoot.style.top = panelPos.y;
        _diceRoot.style.translate = new Translate(
            new Length(-50f, LengthUnit.Percent),
            new Length(-100f, LengthUnit.Percent),
            0f);
    }

    private void TryBindOrRetry()
    {
        if (_bound) return;
        if (_uiRoot == null) return;

        if (TryBind(_uiRoot))
        {
            _bound = true;
            return;
        }

        if (_remainingBindRetries-- > 0)
        {
            // Retry next frame; useful if the panel initializes after OnEnable.
            _uiRoot.schedule.Execute(TryBindOrRetry).StartingIn(0);
        }
    }

    private bool TryBind(VisualElement root)
    {
        // The UIDocument loads SpeedDice.uxml directly, so the element is already in the tree.
        _diceRoot = root.Q<VisualElement>(SpeedDiceElementName);
        if (_diceRoot == null) return false;

        _diceBackground = _diceRoot.Q<Image>(DiceBackgroundElementName);
        _dice = _diceRoot.Q<Image>(DiceElementName);
        _diceLines = _diceRoot.Q<Image>(DiceLinesElementName);

        // Allow hover on the dice root; children pass events up to it.
        _diceRoot.pickingMode = PickingMode.Position;
        if (_diceBackground != null) _diceBackground.pickingMode = PickingMode.Ignore;
        if (_dice != null) _dice.pickingMode = PickingMode.Ignore;
        if (_diceLines != null) _diceLines.pickingMode = PickingMode.Ignore;

        // Set sprites once at bind time (Update keeps them in sync for Inspector tweaking).
        SetImageSprite(_diceBackground, diceBackgroundSprite);
        SetImageSprite(_dice, diceSprite);
        SetImageSprite(_diceLines, diceLinesSprite);

        return true;
    }

    // Sets a Sprite on a UI Toolkit Image element.
    private static void SetImageSprite(Image? image, Sprite? sprite)
    {
        if (image == null) return;

        if (sprite == null)
        {   
            image.style.display = DisplayStyle.None;
            return;
        }

        image.style.display = DisplayStyle.Flex;

        // Unity versions differ on whether UI Toolkit Image has a `sprite` property.
        var spriteProp = typeof(Image).GetProperty("sprite", BindingFlags.Public | BindingFlags.Instance);
        if (spriteProp != null && spriteProp.PropertyType == typeof(Sprite) && spriteProp.CanWrite)
            spriteProp.SetValue(image, sprite);
        else
            image.image = sprite.texture;

        // Fit inside the element bounds (prevents cropping).
        image.scaleMode = ScaleMode.ScaleToFit;
    }
}
