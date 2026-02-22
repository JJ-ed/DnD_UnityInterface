using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class SpeedDiceView : MonoBehaviour
{
    // UXML element names
    private const string SpeedDiceStackElementName = "SpeedDiceStack";
    private const string SpeedDiceElementName = "SpeedDice";
    private const string SpeedDiceRowPrefix = "SpeedDiceRow";

    private const string DiceBackgroundElementName = "DiceBackground";
    private const string DiceElementName = "Dice";
    private const string DiceLinesElementName = "DiceLines";
    private const string DiceRouletteElementName = "DiceRoulette";

    private const string SpeedTensElementName = "SpeedTens";
    private const string SpeedOnesElementName = "SpeedOnes";

    // USS classes 
    private const string SpeedClassPrefix = "speed--";
    private const string OnesClassPrefix = "ones--";
    private const string TensClassPrefix = "tens--";
    private const string StackedClass = "speedDice--stacked";

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

    [Header("Stack")]
    [SerializeField, Range(1, 28)] private int diceCount = 1;

    [Header("Stack Offsets")]
    [SerializeField] private Vector2 offsetFor1Layer = new(8.4f, -12.4f);
    [Tooltip("Panel-space offset applied to the whole stack when 2 rows are visible.")]
    [SerializeField] private Vector2 offsetFor2Layers = new(8.4f, -7.55f);
    [Tooltip("Panel-space offset applied to the whole stack when 3 rows are visible.")]
    [SerializeField] private Vector2 offsetFor3Layers = new(8.4f, -3.43f);
    [Tooltip("Panel-space offset applied to the whole stack when 4 rows are visible.")]
    [SerializeField] private Vector2 offsetFor4Layers = new(8.4f, -0.27f);
    [Tooltip("Panel-space offset applied to the whole stack when 5 rows are visible.")]
    [SerializeField] private Vector2 offsetFor5Layers = new(8.4f, 2.75f);

    [SerializeField] private System.Collections.Generic.List<Sprite> speedDigitSprites = new();

    [Header("Roulette Animation")]
    [SerializeField] private float fallDuration = 1.5f;

    private const bool HideWhenBehindCamera = true;
    private const int BindRetryFrames = 10;
    private const int MaxDice = 28;
    private const int RowCount = 5;
    private static readonly int[] RowPattern = { 6, 5, 6, 5, 6 };

    private sealed class DiceUI
    {
        public VisualElement Root = null!;
        public Image? DiceBackground;
        public Image? Dice;
        public Image? DiceLines;
        public Image? DiceRoulette;
        public Image? SpeedTens;
        public Image? SpeedOnes;

        public int CurrentSpeed = -1;
        public string? LastSpeedClass;
        public string? LastOnesClass;
        public string? LastTensClass;
    }

    private readonly List<DiceUI> _dice = new();
    private VisualElement? _stackRoot;
    private readonly List<VisualElement> _rows = new();
    private int _activeLayerCount = 1;
    private float _fallTimer;

    private VisualElement? _uiRoot;
    private int _remainingBindRetries;
    private bool _bound;
    private bool _speedsInitialized;

    private void OnEnable()
    {
        _remainingBindRetries = BindRetryFrames;
        _bound = false;
        _speedsInitialized = false;

        var root = GetComponent<UIDocument>().rootVisualElement;
        _uiRoot = root;

        // Stretch the root to full screen so hit testing can reach absolute children.
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.pickingMode = PickingMode.Ignore;

        // Speeds are initialized after we bind (when dice instances exist).
        TryBindOrRetry();
    }

    private void OnValidate()
    {
        // Two-digit display (0-99).
        minSpeed = Mathf.Clamp(minSpeed, 0, 99);
        maxSpeed = Mathf.Clamp(maxSpeed, 0, 99);
        diceCount = Mathf.Clamp(diceCount, 1, MaxDice);
    }

    private void Update()
    {
        if (!_bound || _stackRoot == null) return;

        var cam = Camera.main;
        if (cam == null || target == null)
        {
            _stackRoot.style.display = DisplayStyle.None;
            return;
        }

        var worldPos = target.position + worldOffset;
        var screenPos = cam.WorldToScreenPoint(worldPos);

        if (HideWhenBehindCamera && screenPos.z < 0f)
        {
            _stackRoot.style.display = DisplayStyle.None;
            return;
        }

        if (_stackRoot.panel == null)
            return;

        _stackRoot.style.display = DisplayStyle.Flex;

        // Keep sprites in sync (supports live tweaking in Inspector).
        foreach (var die in _dice)
        {
            SetImageSprite(die.DiceBackground, diceBackgroundSprite);
            SetImageSprite(die.Dice, diceSprite);
            SetImageSprite(die.DiceLines, diceLinesSprite);
            SetImageSpriteOnly(die.DiceRoulette, diceRouletteSprite);
        }

        // Animate roulette falling: top goes from -100% to +100%, then resets (applies to all dice).
        if (diceRouletteSprite != null)
        {
            _fallTimer += Time.deltaTime / Mathf.Max(fallDuration, 0.01f);
            _fallTimer %= 1f;

            var topPercent = Mathf.Lerp(-100f, 100f, _fallTimer);
            foreach (var die in _dice)
            {
                if (die.DiceRoulette != null)
                    die.DiceRoulette.style.top = new Length(topPercent, LengthUnit.Percent);
            }
        }

        // Scale the dice based on camera zoom
        if (scaleWithCameraZoom && cam.orthographic)
        {
            // Bigger ortho size = more zoomed out = dice should be smaller.
            var safeRef = Mathf.Max(referenceOrthographicSize, 0.001f);
            var safeOrtho = Mathf.Max(cam.orthographicSize, 0.001f);
            var scale = safeRef / safeOrtho;
            scale = Mathf.Clamp(scale, zoomScaleClamp.x, zoomScaleClamp.y);
            _stackRoot.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));
        }
        else
        {
            _stackRoot.style.scale = new StyleScale(new Scale(Vector3.one));
        }

        // Convert Unity screen coords into panel coords.
        var panelPos = RuntimePanelUtils.ScreenToPanel(_stackRoot.panel, new Vector2(screenPos.x, screenPos.y));
        var offset = GetOffsetForLayers(_activeLayerCount);

        // Place the element centered horizontally, sitting above the point.
        _stackRoot.style.left = panelPos.x + offset.x;
        _stackRoot.style.top = panelPos.y + offset.y;
        _stackRoot.style.translate = new Translate(
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
        _stackRoot = root.Q<VisualElement>(SpeedDiceStackElementName);
        if (_stackRoot == null) return false;

        _stackRoot.pickingMode = PickingMode.Ignore;

        // Bind row containers (fixed 5 rows).
        _rows.Clear();
        for (var i = 0; i < RowCount; i++)
        {
            var row = _stackRoot.Q<VisualElement>(SpeedDiceRowPrefix + i);
            if (row == null) return false;
            row.pickingMode = PickingMode.Ignore;
            _rows.Add(row);
        }

        // Prototype die exists in row 0.
        var prototype = _rows[0].Q<VisualElement>(SpeedDiceElementName);
        if (prototype == null) return false;

        _dice.Clear();
        _dice.Add(BindDie(prototype));

        EnsureDiceCount();
        EnsureSpeedsInitialized();
        ApplyAllSpeedsToUi();
        return true;
    }

    //Rolls all dice in the stack and updates their UI
    public void RollAllSpeeds()
    {
        if (_dice.Count == 0) return;
        var min = Mathf.Min(minSpeed, maxSpeed);
        var max = Mathf.Max(minSpeed, maxSpeed);

        // Generate then sort descending so the stack reads high-to-low
        // in display order (top->bottom, left->right).
        var speeds = new List<int>(_dice.Count);
        for (var i = 0; i < _dice.Count; i++)
        {
            var rolled = UnityEngine.Random.Range(min, max + 1);
            speeds.Add(Mathf.Clamp(rolled, 0, 99));
        }

        speeds.Sort((a, b) => b.CompareTo(a)); // descending
        for (var i = 0; i < _dice.Count; i++)
            _dice[i].CurrentSpeed = speeds[i];

        ApplyAllSpeedsToUi();
    }

    private void EnsureSpeedsInitialized()
    {
        if (_speedsInitialized) return;
        if (_dice.Count == 0) return;

        var min = Mathf.Min(minSpeed, maxSpeed);
        var max = Mathf.Max(minSpeed, maxSpeed);

        // Initialize all dice once, then sort descending to match display order.
        var speeds = new List<int>(_dice.Count);
        for (var i = 0; i < _dice.Count; i++)
        {
            var rolled = UnityEngine.Random.Range(min, max + 1);
            speeds.Add(Mathf.Clamp(rolled, 0, 99));
        }

        speeds.Sort((a, b) => b.CompareTo(a)); // descending
        for (var i = 0; i < _dice.Count; i++)
            _dice[i].CurrentSpeed = speeds[i];

        _speedsInitialized = true;
    }

    private void EnsureDiceCount()
    {
        if (_stackRoot == null) return;
        if (_rows.Count != RowCount) return;
        var desired = Mathf.Clamp(diceCount, 1, MaxDice);

        // Remove extras.
        for (var i = _dice.Count - 1; i >= desired; i--)
        {
            var die = _dice[i];
            die.Root.RemoveFromHierarchy();
            _dice.RemoveAt(i);
        }

        // Add missing.
        while (_dice.Count < desired)
        {
            var dieRoot = CreateDieElement();
            // Added to a row below (after we clear + distribute).
            _dice.Add(BindDie(dieRoot));
        }

        // Rebuild row contents deterministically (6/5/6/5/6).
        for (var r = 0; r < _rows.Count; r++)
            _rows[r].Clear();

        var dieIndex = 0;
        _activeLayerCount = 0;
        for (var rowIndex = 0; rowIndex < RowCount && dieIndex < desired; rowIndex++)
        {
            var rowCap = RowPattern[rowIndex];
            var countInRow = Mathf.Min(rowCap, desired - dieIndex);

            _rows[rowIndex].style.display = countInRow > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (countInRow > 0) _activeLayerCount = rowIndex + 1;

            for (var j = 0; j < countInRow; j++)
            {
                var die = _dice[dieIndex++];
                _rows[rowIndex].Add(die.Root);

                // Overlap all but the first die in the row.
                if (j == 0) die.Root.RemoveFromClassList(StackedClass);
                else die.Root.AddToClassList(StackedClass);
            }
        }

        // Hide any remaining empty rows after the last used row.
        for (var rowIndex = _activeLayerCount; rowIndex < RowCount; rowIndex++)
            _rows[rowIndex].style.display = DisplayStyle.None;

        _activeLayerCount = Mathf.Clamp(_activeLayerCount, 1, RowCount);
    }

    private Vector2 GetOffsetForLayers(int layers)
    {
        return layers switch
        {
            5 => offsetFor5Layers,
            4 => offsetFor4Layers,
            3 => offsetFor3Layers,
            2 => offsetFor2Layers,
            _ => offsetFor1Layer
        };
    }

    private static VisualElement CreateDieElement()
    {
        var dieRoot = new VisualElement { name = SpeedDiceElementName };
        dieRoot.AddToClassList("speedDice");

        var diceBackground = new Image { name = DiceBackgroundElementName };
        diceBackground.AddToClassList("speedDice__img");

        var dice = new Image { name = DiceElementName };
        dice.AddToClassList("speedDice__img");

        var rouletteClip = new VisualElement { name = "DiceRouletteClip" };
        rouletteClip.AddToClassList("speedDice__rouletteClip");

        var roulette = new Image { name = DiceRouletteElementName };
        roulette.AddToClassList("speedDice__roulette");
        rouletteClip.Add(roulette);

        var speedTens = new Image { name = SpeedTensElementName };
        speedTens.AddToClassList("speedDice__digit");

        var speedOnes = new Image { name = SpeedOnesElementName };
        speedOnes.AddToClassList("speedDice__digit");

        var diceLines = new Image { name = DiceLinesElementName };
        diceLines.AddToClassList("speedDice__img");

        dieRoot.Add(diceBackground);
        dieRoot.Add(dice);
        dieRoot.Add(rouletteClip);
        dieRoot.Add(speedTens);
        dieRoot.Add(speedOnes);
        dieRoot.Add(diceLines);

        return dieRoot;
    }

    private DiceUI BindDie(VisualElement dieRoot)
    {
        var die = new DiceUI { Root = dieRoot };
        dieRoot.pickingMode = PickingMode.Position;

        die.DiceBackground = dieRoot.Q<Image>(DiceBackgroundElementName);
        die.Dice = dieRoot.Q<Image>(DiceElementName);
        die.DiceLines = dieRoot.Q<Image>(DiceLinesElementName);
        die.DiceRoulette = dieRoot.Q<Image>(DiceRouletteElementName);
        die.SpeedTens = dieRoot.Q<Image>(SpeedTensElementName);
        die.SpeedOnes = dieRoot.Q<Image>(SpeedOnesElementName);

        if (die.DiceBackground != null) die.DiceBackground.pickingMode = PickingMode.Ignore;
        if (die.Dice != null) die.Dice.pickingMode = PickingMode.Ignore;
        if (die.DiceLines != null) die.DiceLines.pickingMode = PickingMode.Ignore;
        if (die.DiceRoulette != null) die.DiceRoulette.pickingMode = PickingMode.Ignore;
        if (die.SpeedTens != null) die.SpeedTens.pickingMode = PickingMode.Ignore;
        if (die.SpeedOnes != null) die.SpeedOnes.pickingMode = PickingMode.Ignore;

        return die;
    }

    private void ApplyAllSpeedsToUi()
    {
        for (var i = 0; i < _dice.Count; i++)
        {
            SetDieSpeed(_dice[i], Mathf.Clamp(_dice[i].CurrentSpeed, 0, 99));
        }
    }

    private void SetDieSpeed(DiceUI die, int speed)
    {
        die.CurrentSpeed = speed;

        // Digits.
        if (die.SpeedOnes == null) return;
        var tens = speed / 10;
        var ones = speed % 10;

        ApplyDigitToImage(die.SpeedOnes, ones);
        SyncDigitClass(die.SpeedOnes, OnesClassPrefix, ones, ref die.LastOnesClass);

        if (die.SpeedTens != null)
        {
            if (speed >= 10)
            {
                ApplyDigitToImage(die.SpeedTens, tens);
                SyncDigitClass(die.SpeedTens, TensClassPrefix, tens, ref die.LastTensClass);
            }
            else
            {
                SetImageSprite(die.SpeedTens, null);
                SyncDigitClass(die.SpeedTens, TensClassPrefix, null, ref die.LastTensClass);
            }
        }

        // speed--NN class on the die root for pair-specific tweaks (e.g. speed--10).
        if (!string.IsNullOrEmpty(die.LastSpeedClass))
            die.Root.RemoveFromClassList(die.LastSpeedClass);
        die.LastSpeedClass = SpeedClassPrefix + speed;
        die.Root.AddToClassList(die.LastSpeedClass);

        EnforceDieLayerOrder(die);
    }

    private static void EnforceDieLayerOrder(DiceUI die)
    {
        // Desired order (back -> front) within each die: roulette, lines, digits
        if (die.DiceRoulette != null) die.DiceRoulette.SendToBack();
        if (die.DiceLines != null && die.SpeedTens != null) die.DiceLines.PlaceBehind(die.SpeedTens);
        if (die.DiceLines != null && die.SpeedOnes != null) die.DiceLines.PlaceBehind(die.SpeedOnes);

        if (die.SpeedTens != null) die.SpeedTens.BringToFront();
        if (die.SpeedOnes != null) die.SpeedOnes.BringToFront();
    }

    private static void SyncDigitClass(Image image, string prefix, int? digit, ref string? lastClass)
    {
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
