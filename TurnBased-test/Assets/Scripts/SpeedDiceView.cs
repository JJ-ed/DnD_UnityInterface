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
    private const string DiceRouletteElementName = "DiceRoulette";

    private const string SpeedTensElementName = "SpeedTens";
    private const string SpeedOnesElementName = "SpeedOnes";

    // USS classes (for per-speed styling in SpeedDice.uss)
    private const string SpeedClassPrefix = "speed--";
    private const string OnesClassPrefix = "ones--";
    private const string TensClassPrefix = "tens--";

    [Header("World Target")]
    [Tooltip("World-space Transform to track (typically the Player).")]
    [SerializeField] private Transform? target;

    [Tooltip("World offset added to the target position")]
    [SerializeField] private Vector3 worldOffset = new(0f, 1.8f, 0f);

    [Header("Camera Scaling")]
    [Tooltip("If enabled, scales the dice based on the camera zoom")]
    [SerializeField] private bool scaleWithCameraZoom = true;

    [Tooltip("Orthographic reference size where the dice scale is 1.0.")]
    [SerializeField] private float referenceOrthographicSize = 13.5f;

    [Tooltip("Clamp the zoom-driven scale")]
    [SerializeField] private Vector2 zoomScaleClamp = new(0.6f, 1.6f);

    [Header("Sprites")]
    [Tooltip("Dice background")]
    [SerializeField] private Sprite? diceBackgroundSprite;

    [Tooltip("Dice")]
    [SerializeField] private Sprite? diceSprite;

    [Tooltip("Dice lines")]
    [SerializeField] private Sprite? diceLinesSprite;

    [Tooltip("Falling roulette sprite")]
    [SerializeField] private Sprite? diceRouletteSprite;

    [Header("Speed")]
    [Tooltip("Minimum speed (inclusive).")]
    [SerializeField] private int minSpeed = 1;

    [Tooltip("Maximum speed (inclusive).")]
    [SerializeField] private int maxSpeed = 9;

    [SerializeField] private System.Collections.Generic.List<Sprite> speedDigitSprites = new();

    [Header("Roulette Animation")]
    [SerializeField] private float fallDuration = 1.5f;

    private const bool HideWhenBehindCamera = true;
    private const int BindRetryFrames = 10;

    private VisualElement? _diceRoot;
    private Image? _diceBackground;
    private Image? _dice;
    private Image? _diceLines;
    
    private Image? _diceRoulette;
    private Image? _speedTens;
    private Image? _speedOnes;
    private float _fallTimer;

    private VisualElement? _uiRoot;
    private int _remainingBindRetries;
    private bool _bound;
    private int _currentSpeed = -1;
    private string? _lastSpeedClass;
    private string? _lastOnesClass;
    private string? _lastTensClass;

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

        // Default to a rolled speed on enable. If some other system sets speed later, it can call SetSpeed/RollSpeed again.
        // Could be VERY helpful too in future functions or debugging.
        RollSpeed();
        TryBindOrRetry();
    }

    private void OnValidate()
    {
        // Two-digit display (0-99).
        minSpeed = Mathf.Clamp(minSpeed, 0, 99);
        maxSpeed = Mathf.Clamp(maxSpeed, 0, 99);
    }

    private void Update()
    {
        if (!_bound || _diceRoot == null) return;

        // Keep sprites in sync (supports live tweaking in Inspector).
        SetImageSprite(_diceBackground, diceBackgroundSprite);
        SetImageSprite(_dice, diceSprite);
        SetImageSprite(_diceLines, diceLinesSprite);

        // Animate roulette falling: top goes from -100% to +100%, then resets.
        if (_diceRoulette != null && diceRouletteSprite != null)
        {
            _fallTimer += Time.deltaTime / Mathf.Max(fallDuration, 0.01f);
            _fallTimer %= 1f;

            // Lerp from -100% (above clip) to +100% (below clip).
            var topPercent = Mathf.Lerp(-100f, 100f, _fallTimer);
            _diceRoulette.style.top = new Length(topPercent, LengthUnit.Percent);
        }

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
            return;

        _diceRoot.style.display = DisplayStyle.Flex;

        // Scale the dice based on camera zoom
        if (scaleWithCameraZoom && cam.orthographic)
        {
            // Bigger ortho size = more zoomed out = dice should be smaller.
            var safeRef = Mathf.Max(referenceOrthographicSize, 0.001f);
            var safeOrtho = Mathf.Max(cam.orthographicSize, 0.001f);
            var scale = safeRef / safeOrtho;
            scale = Mathf.Clamp(scale, zoomScaleClamp.x, zoomScaleClamp.y);
            _diceRoot.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));
        }
        else
        {
            _diceRoot.style.scale = new StyleScale(new Scale(Vector3.one));
        }

        // Convert Unity screen coords into panel coords.
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
        _diceRoulette = _diceRoot.Q<Image>(DiceRouletteElementName);

        _speedTens = _diceRoot.Q<Image>(SpeedTensElementName);
        _speedOnes = _diceRoot.Q<Image>(SpeedOnesElementName);

        // Allow hover on the dice root; children pass events up to it.
        _diceRoot.pickingMode = PickingMode.Position;
        if (_diceBackground != null) _diceBackground.pickingMode = PickingMode.Ignore;
        if (_dice != null) _dice.pickingMode = PickingMode.Ignore;
        if (_diceLines != null) _diceLines.pickingMode = PickingMode.Ignore;

        if (_diceRoulette != null) _diceRoulette.pickingMode = PickingMode.Ignore;

        if (_speedTens != null) _speedTens.pickingMode = PickingMode.Ignore;
        if (_speedOnes != null) _speedOnes.pickingMode = PickingMode.Ignore;

        // Set sprites once at bind time (Update keeps them in sync for Inspector tweaking).
        SetImageSprite(_diceBackground, diceBackgroundSprite);
        SetImageSprite(_dice, diceSprite);
        SetImageSprite(_diceLines, diceLinesSprite);
        SetImageSpriteOnly(_diceRoulette, diceRouletteSprite);
        SyncSpeedDigits();
        SyncSpeedClass();
        EnforceLayerOrder();

        return true;
    }

    //Set the current speed explicitly (clamped to 0-99 for 2-digit display).
    public void SetSpeed(int speed)
    {
        _currentSpeed = Mathf.Clamp(speed, 0, 99);
        SyncSpeedDigits();
        SyncSpeedClass();
        EnforceLayerOrder();
    }

    //Roll speed using min/max (inclusive) and update the icon.
    public int RollSpeed()
    {
        var min = Mathf.Min(minSpeed, maxSpeed);
        var max = Mathf.Max(minSpeed, maxSpeed);
        var rolled = UnityEngine.Random.Range(min, max + 1); // inclusive upper bound for ints
        SetSpeed(rolled);
        return _currentSpeed;
    }

    private void SyncSpeedDigits()
    {
        if (_speedOnes == null) return;

        var speed = Mathf.Clamp(_currentSpeed, 0, 99);
        var tens = speed / 10;
        var ones = speed % 10;

        // Ones always shown.
        ApplyDigitToImage(_speedOnes, ones);
        SyncDigitClass(_speedOnes, OnesClassPrefix, ones, ref _lastOnesClass);

        // Tens only for 10+.
        if (_speedTens != null)
        {
            if (speed >= 10)
            {
                ApplyDigitToImage(_speedTens, tens);
                SyncDigitClass(_speedTens, TensClassPrefix, tens, ref _lastTensClass);
            }
            else
            {
                SetImageSprite(_speedTens, null);
                SyncDigitClass(_speedTens, TensClassPrefix, null, ref _lastTensClass);
            }
        }
    }

    private static void SyncDigitClass(Image image, string prefix, int? digit, ref string? lastClass)
    {
        // Remove old digit class.
        if (!string.IsNullOrEmpty(lastClass))
            image.RemoveFromClassList(lastClass);

        if (!digit.HasValue)
        {
            lastClass = null;
            return;
        }

        var d = digit.Value;
        if (d < 0 || d > 9)
        {
            lastClass = null;
            return;
        }

        lastClass = prefix + d;
        image.AddToClassList(lastClass);
    }

    // Ensure DiceLines stays visually above the speed icon, regardless of USS quirks.
    private void EnforceLayerOrder()
    {
        if (_diceRoot == null) return;

        // Desired order (back -> front):
        // background/dice/roulette, dice lines, speed digits
        if (_diceRoulette != null) _diceRoulette.SendToBack();

        if (_diceLines != null && _speedTens != null)
            _diceLines.PlaceBehind(_speedTens);

        if (_diceLines != null && _speedOnes != null)
            _diceLines.PlaceBehind(_speedOnes);

        // Ensure digits render above lines 
        if (_speedTens != null) _speedTens.BringToFront();
        if (_speedOnes != null) _speedOnes.BringToFront();
    }

    private void SyncSpeedClass()
    {
        if (_diceRoot == null) return;

        // Remove old speed class.
        if (!string.IsNullOrEmpty(_lastSpeedClass))
            _diceRoot.RemoveFromClassList(_lastSpeedClass);

        // Add current speed class (speed--0 .. speed--99).
        if (_currentSpeed >= 0 && _currentSpeed <= 99)
        {
            _lastSpeedClass = SpeedClassPrefix + _currentSpeed;
            _diceRoot.AddToClassList(_lastSpeedClass);
        }
        else
        {
            _lastSpeedClass = null;
        }
    }

    private Sprite? GetSpeedDigitSprite(int speed)
    {
        if (speed < 0 || speed > 9) return null;
        if (speedDigitSprites == null || speedDigitSprites.Count < 10) return null;
        return speedDigitSprites[speed];
    }

    private void ApplyDigitToImage(Image image, int digit)
    {
        var sprite = GetSpeedDigitSprite(digit);
        if (sprite == null)
        {
            SetImageSprite(image, null);
            return;
        }

        SetImageSprite(image, sprite);
    }

    // Sets a Sprite without changing display style (for USS-controlled visibility).
    private static void SetImageSpriteOnly(Image? image, Sprite? sprite)
    {
        if (image == null || sprite == null) return;

        var spriteProp = typeof(Image).GetProperty("sprite", BindingFlags.Public | BindingFlags.Instance);
        if (spriteProp != null && spriteProp.PropertyType == typeof(Sprite) && spriteProp.CanWrite)
            spriteProp.SetValue(image, sprite);
        else
            image.image = sprite.texture;

        image.scaleMode = ScaleMode.ScaleToFit;
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
