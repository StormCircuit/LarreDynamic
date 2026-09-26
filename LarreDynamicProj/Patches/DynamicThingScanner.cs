using System;
using Assets.Scripts.Objects;
using UnityEngine;

namespace LarreDynamic.Patches
{
  internal static class DynamicThingScanner
  {
    private const int MaxColliders = 64;
    private static readonly Collider[] ColliderBuffer = new Collider[MaxColliders];

    public static bool TryFindNearestWithSlots(Vector3 origin, float radius, out DynamicThing result)
    {
      result = null;
      if (radius <= 0f)
      {
        return false;
      }

      int colliderCount = Physics.OverlapSphereNonAlloc(
        origin,
        radius,
        ColliderBuffer,
        ~0,
        QueryTriggerInteraction.Collide);
      float nearestDistanceSquared = radius * radius;

      try
      {
        for (int index = 0; index < colliderCount; index++)
        {
          Collider collider = ColliderBuffer[index];
          DynamicThing candidate = collider != null
            ? collider.GetComponentInParent<DynamicThing>()
            : null;
          if (candidate == null || candidate.BeingDestroyed || candidate.Slots == null || candidate.Slots.Count == 0)
          {
            continue;
          }

          Vector3 closestPoint = collider.ClosestPoint(origin);
          float distanceSquared = (closestPoint - origin).sqrMagnitude;
          if (distanceSquared <= nearestDistanceSquared)
          {
            nearestDistanceSquared = distanceSquared;
            result = candidate;
          }
        }
      }
      finally
      {
        Array.Clear(ColliderBuffer, 0, colliderCount);
      }

      return result != null;
    }
  }
}