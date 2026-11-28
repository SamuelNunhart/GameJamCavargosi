using UnityEngine;

/// <summary>The ray under the aiming dot, limited by reach and the first solid surface.</summary>
internal readonly struct InteractionAim
{
    private static RaycastHit[] solidHits = new RaycastHit[8];
    private readonly float reach;
    private readonly RaycastHit obstruction;
    internal Ray Ray { get; }

#if UNITY_EDITOR // DEBUG-ONLY: remove before release; see docs/debug-skips.md
    internal RaycastHit DebugSolidHit => obstruction;
    internal RaycastHit DebugFindDistantSolid(Transform playerRoot, float distance) =>
        FindFirstSolid(Ray, distance, playerRoot);
#endif

    private InteractionAim(Camera camera, Transform playerRoot, float reach)
    {
        // Measure reach from the player's eyes, including the space before the near clip plane.
        Ray = new Ray(camera.transform.position, camera.ViewportPointToRay(new Vector3(.5f, .5f)).direction);
        this.reach = reach;
        obstruction = FindFirstSolid(Ray, reach, playerRoot);
    }

    internal static bool TryCreate(StoryPlayerRig player, float reach, out InteractionAim aim)
    {
        Camera camera = player != null ? player.ActiveCamera : null;
        aim = camera != null ? new InteractionAim(camera, player.transform, reach) : default;
        return camera != null;
    }

    private static RaycastHit FindFirstSolid(Ray ray, float distance, Transform playerRoot)
    {
        if (!Physics.Raycast(ray, out RaycastHit first, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return default;
        if (!first.collider.transform.IsChildOf(playerRoot)) return first;

        // Looking down can hit the player's capsule before the surface under the dot.
        int count = Physics.RaycastNonAlloc(ray, solidHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        while (count == solidHits.Length)
        {
            // A full buffer may omit closer hits.
            System.Array.Resize(ref solidHits, solidHits.Length * 2);
            count = Physics.RaycastNonAlloc(ray, solidHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        RaycastHit nearest = default;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = solidHits[i];
            if (hit.distance >= nearestDistance || hit.collider.transform.IsChildOf(playerRoot)) continue;
            nearest = hit;
            nearestDistance = hit.distance;
        }
        return nearest;
    }

    internal bool Sees(Transform surface) => surface != null && obstruction.collider != null &&
        obstruction.collider.transform.IsChildOf(surface);

    internal bool Reaches(float distance, Transform backing = null)
    {
        if (distance < 0f || distance > reach) return false;
        if (obstruction.collider == null || distance <= obstruction.distance) return true;
        // The collision surface behind a mounted note can protrude slightly from its visual surface.
        return Sees(backing) && distance - obstruction.distance <= .035f;
    }

    internal bool HitsTrigger(Collider target)
    {
        if (target == null || !target.enabled || !target.gameObject.activeInHierarchy) return false;
        if (target.Raycast(Ray, out RaycastHit hit, reach)) return Reaches(hit.distance);

        // Unity's collider raycast misses when the eye is already inside the thin door trigger.
        return target.ClosestPoint(Ray.origin) == Ray.origin &&
            Vector3.Dot(target.bounds.center - Ray.origin, Ray.direction) > 0f;
    }
}
