using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws the translucent beer surface inside the brain-shaped UI mask.</summary>
public sealed class FatherAlcoholIndicator : MaskableGraphic
{
    [SerializeField] private FatherFridgeInteractable fridge;

    private static readonly Color32 Amber = new Color32(219, 133, 33, 151);
    private const int StripCount = 48;
    private const float RiseTime = .42f;
    private const float RestingWave = .015f;

    private float fill;
    private float target;
    private float riseFrom;
    private float timeSinceChange;
    private float waveTime;

    private void Update()
    {
        float newTarget = fridge != null ? Mathf.Min(fridge.BeersDrunk, 3) * .3f : 0f;
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

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (fill <= 0f) return;

        Rect rect = GetPixelAdjustedRect();
        float amplitude = RestingWave + .032f * Mathf.Exp(-timeSinceChange * 3.5f);
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
    }

    private float Surface(Rect rect, float x, float amplitude)
    {
        float wave = Mathf.Sin(x * 9f - waveTime * 3.2f) * amplitude;
        return Mathf.Lerp(rect.yMin, rect.yMax, Mathf.Clamp01(fill + wave));
    }
}
