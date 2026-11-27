using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws the translucent beer surface inside the brain-shaped UI mask.</summary>
public sealed class FatherAlcoholIndicator : MaskableGraphic
{
    [SerializeField] private FatherFridgeInteractable fridge;

    private static readonly Color32 Amber = new Color32(219, 133, 33, 151);
    private static readonly Color32 BubbleColor = new Color32(255, 236, 182, 235);
    private const int StripCount = 48;
    private const int BubbleCount = 8;
    private const int BubbleSegments = 10;
    private const float RiseTime = .42f;
    private const float RestingWave = .015f;
    private const float PulseDuration = .85f;

    private float fill;
    private float target;
    private float riseFrom;
    private float timeSinceChange;
    private float waveTime;
    private bool bubbling;
    private Transform pulsingIcon;
    private Vector3 iconScale;
    private float pulseTime;

    internal void EmphasizeDrink()
    {
        if (pulsingIcon == null)
        {
            pulsingIcon = transform.parent;
            if (pulsingIcon == null) return;
            iconScale = pulsingIcon.localScale;
        }
        pulseTime = 0f;
    }

    protected override void OnDisable()
    {
        RestoreIconScale();
        base.OnDisable();
    }

    private void Update()
    {
        UpdatePulse();
        int beers = fridge != null ? fridge.BeersDrunk : 0;
        bubbling = beers >= FatherBlackout.BlackoutBeerCount;
        float newTarget = bubbling ? 1f : Mathf.Min(beers, 3) * .3f;
        if (!Mathf.Approximately(newTarget, target))
        {
            riseFrom = fill;
            target = newTarget;
            timeSinceChange = 0f;
        }

        if (fill == 0f && target == 0f) return;
        timeSinceChange += Time.unscaledDeltaTime;
        waveTime += Time.unscaledDeltaTime;
        fill = Mathf.SmoothStep(riseFrom, target, timeSinceChange / RiseTime);
        SetVerticesDirty();
    }

    private void UpdatePulse()
    {
        if (pulsingIcon == null || AudioSettingsOverlay.IsOpen) return;
        pulseTime += Time.unscaledDeltaTime;
        if (pulseTime >= PulseDuration)
        {
            RestoreIconScale();
            return;
        }

        float bump = Mathf.Sin(Mathf.PI * pulseTime / PulseDuration);
        pulsingIcon.localScale = iconScale * (1f + .22f * bump);
    }

    private void RestoreIconScale()
    {
        if (pulsingIcon != null) pulsingIcon.localScale = iconScale;
        pulsingIcon = null;
    }

#if UNITY_EDITOR // DEBUG-ONLY: remove before release; see docs/debug-skips.md
    internal void DebugResetAfterOpening()
    {
        RestoreIconScale();
        fill = target = riseFrom = 0f;
        bubbling = false;
        SetVerticesDirty();
    }
#endif

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (fill <= 0f) return;

        Rect rect = GetPixelAdjustedRect();
        float amplitude = RestingWave + .032f * Mathf.Exp(-timeSinceChange * 3.5f);
        if (bubbling) amplitude *= 1f - fill;
        for (int i = 0; i <= StripCount; i++)
        {
            float x = (float)i / StripCount;
            float pixelX = rect.xMin + x * rect.width;
            mesh.AddVert(new Vector3(pixelX, rect.yMin), Amber, Vector2.zero);
            mesh.AddVert(new Vector3(pixelX, Surface(rect, x, amplitude)), Amber, Vector2.zero);
            if (i == 0) continue;

            int left = 2 * (i - 1);
            mesh.AddTriangle(left, left + 2, left + 3);
            mesh.AddTriangle(left, left + 3, left + 1);
        }

        if (bubbling) AddBubbles(mesh, rect);
    }

    private void AddBubbles(VertexHelper mesh, Rect rect)
    {
        for (int i = 0; i < BubbleCount; i++)
        {
            float age = Mathf.Repeat(timeSinceChange * (.8f + .1f * (i % 3)) + .141f * i, 1f);
            float lane = .21f + .57f * ((i * 3 % BubbleCount) / (float)(BubbleCount - 1));
            float x = Mathf.Lerp(rect.xMin, rect.xMax, lane + Mathf.Sin(age * 6f + i) * .035f);
            float height = Mathf.Lerp(.16f, fill - .06f, age);
            float y = Mathf.Lerp(rect.yMin, rect.yMax, height);
            float radius = Mathf.Lerp(2f, 5f, age) * (.8f + .16f * (i % 3));
            var tint = BubbleColor;
            tint.a = (byte)(tint.a * Mathf.Clamp01((1f - age) * 8f));
            AddBubbleRing(mesh, new Vector2(x, y), radius, tint);
        }
    }

    private static void AddBubbleRing(VertexHelper mesh, Vector2 center, float radius, Color32 tint)
    {
        int start = mesh.currentVertCount;
        for (int side = 0; side <= BubbleSegments; side++)
        {
            float angle = 2f * Mathf.PI * side / BubbleSegments;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            mesh.AddVert(center + direction * radius, tint, Vector2.zero);
            mesh.AddVert(center + direction * radius * .65f, tint, Vector2.zero);
            if (side == 0) continue;

            int left = start + (side - 1) * 2;
            mesh.AddTriangle(left, left + 2, left + 3);
            mesh.AddTriangle(left, left + 3, left + 1);
        }
    }

    private float Surface(Rect rect, float x, float amplitude)
    {
        float wave = Mathf.Sin(x * 9f - waveTime * 3.2f) * amplitude;
        return Mathf.Lerp(rect.yMin, rect.yMax, Mathf.Clamp01(fill + wave));
    }
}
