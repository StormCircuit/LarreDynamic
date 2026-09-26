using System.Reflection;
using System;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Objects.Pipes;
using HarmonyLib;
using Objects.RoboticArm;
using UnityEngine;

namespace LarreDynamic.Patches
{
  [HarmonyPatch]
  internal static class RoboticArmDockCargoPatch
  {
    private const float DynamicThingScanRadius = 1.5f;
    private const int MaxDeviceColliders = 64;

    private static readonly FieldInfo ArmField = AccessTools.Field(typeof(RoboticArmDock), "_arm");
    private static readonly FieldInfo CurrentSlotIndexField = AccessTools.Field(typeof(RoboticArmDockCargo), "_currentSlotIndex");
    private static readonly MethodInfo GetArmInteractionCellMethod = AccessTools.Method(typeof(RoboticArmDock), "GetArmInteractionCell");
    private static readonly MethodInfo SetTargetLogicableMethod = AccessTools.PropertySetter(typeof(RoboticArmDockCargo), "TargetLogicable");
    private static readonly Collider[] DeviceColliderBuffer = new Collider[MaxDeviceColliders];
    [HarmonyPatch(typeof(RoboticArmDockCargo), "DoContextualAction")]
    [HarmonyPostfix]
    private static void Postfix(RoboticArmDockCargo __instance)
    {
      SmallCell interactionCell = (SmallCell)GetArmInteractionCellMethod.Invoke(__instance, null);
      if (interactionCell?.Device == null && TryFindFiltrationDevice(__instance, out FiltrationMachineBase filtrationMachine))
      {
        int filtrationSlotIndex = (int)CurrentSlotIndexField.GetValue(__instance);
        if (filtrationSlotIndex >= 0 && filtrationSlotIndex <= 1 && TryGetValidSlot(filtrationMachine, filtrationSlotIndex, out Slot filtrationSlot) && filtrationSlot.Type == Slot.Class.GasFilter && IsAccessibleFiltrationSlot(filtrationSlot))
        {
          TransferSlot(__instance, filtrationMachine, filtrationSlot);
        }
        return;
      }

      if (interactionCell?.Device != null)
      {
        return;
      }

      if (!TryFindDynamicTarget(__instance, out DynamicThing target))
      {
        return;
      }

      int slotIndex = (int)CurrentSlotIndexField.GetValue(__instance);
      if (!TryGetValidSlot(target, slotIndex, out Slot targetSlot))
      {
        return;
      }

      if (!CanAccess(target, slotIndex, targetSlot))
      {
        return;
      }

      TransferSlot(__instance, target, targetSlot);
    }

    [HarmonyPatch(typeof(RoboticArmDockCargo), "CanAccessSlot")]
    [HarmonyPostfix]
    private static void FiltrationAccessPostfix(Slot slot, ref bool __result)
    {
      if (!__result && IsAccessibleFiltrationSlot(slot))
      {
        __result = true;
      }
    }

    [HarmonyPatch(typeof(RoboticArmDockCargo), "SetTargetSmallGrid")]
    [HarmonyPostfix]
    private static void SetTargetSmallGridPostfix(RoboticArmDockCargo __instance)
    {
      if (GetArmInteractionCellMethod.Invoke(__instance, null) is SmallCell { Device: not null })
      {
        return;
      }

      int slotIndex = (int)CurrentSlotIndexField.GetValue(__instance);
      if (SetTargetLogicableMethod != null && TryFindFiltrationDevice(__instance, out FiltrationMachineBase filtrationMachine) && TryGetValidSlot(filtrationMachine, slotIndex, out _))
      {
        SetTargetLogicableMethod.Invoke(__instance, new object[] { filtrationMachine });
        return;
      }

      if (SetTargetLogicableMethod != null && TryFindDynamicTarget(__instance, out DynamicThing target) && TryGetValidSlot(target, slotIndex, out _) && target is ILogicable logicable)
      {
        SetTargetLogicableMethod.Invoke(__instance, new object[] { logicable });
      }
    }

    private static bool TryFindDynamicTarget(RoboticArmDockCargo instance, out DynamicThing target)
    {
      target = null;
      RoboticArm arm = (RoboticArm)ArmField.GetValue(instance);
      if (arm == null || arm.Transform == null)
      {
        return false;
      }

      Vector3 scanOrigin = arm.Transform.position - arm.Transform.up * 0.25f;
      DynamicThing excluded = instance.Slots[0].Get<DynamicThing>();
      if (DynamicThingScanner.TryFindNearestWithSlots(scanOrigin, DynamicThingScanRadius, IsSupportedRobot, excluded, out target))
      {
        return true;
      }

      return DynamicThingScanner.TryFindNearestWithSlots(scanOrigin, DynamicThingScanRadius, IsGenericTarget, excluded, out target);
    }

    private static bool IsSupportedRobot(DynamicThing target)
    {
      return target is RobotMining || string.Equals(target.PrefabName, "robotDiRCI", StringComparison.Ordinal);
    }

    private static bool IsGenericTarget(DynamicThing target)
    {
      return true;
    }

    private static bool IsAccessibleFiltrationSlot(Slot slot)
    {
      return slot?.Parent is FiltrationMachineBase && slot.SlotIndex >= 0 && slot.SlotIndex <= 1 && slot.Type == Slot.Class.GasFilter && slot.IsInteractable && !slot.IsLocked;
    }

    private static bool TryFindFiltrationDevice(RoboticArmDockCargo instance, out FiltrationMachineBase result)
    {
      result = null;
      RoboticArm arm = (RoboticArm)ArmField.GetValue(instance);
      if (arm == null || arm.Transform == null)
      {
        return false;
      }

      Vector3 scanOrigin = arm.Transform.position - arm.Transform.up * 0.25f;
      int colliderCount = Physics.OverlapSphereNonAlloc(scanOrigin, DynamicThingScanRadius, DeviceColliderBuffer, ~0, QueryTriggerInteraction.Collide);
      float nearestDistanceSquared = DynamicThingScanRadius * DynamicThingScanRadius;

      try
      {
        for (int index = 0; index < colliderCount; index++)
        {
          Collider collider = DeviceColliderBuffer[index];
          FiltrationMachineBase candidate = collider != null ? collider.GetComponentInParent<FiltrationMachineBase>() : null;
          if (candidate == null || candidate == instance || candidate.BeingDestroyed)
          {
            continue;
          }

          float distanceSquared = (collider.ClosestPoint(scanOrigin) - scanOrigin).sqrMagnitude;
          if (distanceSquared <= nearestDistanceSquared)
          {
            nearestDistanceSquared = distanceSquared;
            result = candidate;
          }
        }
      }
      finally
      {
        Array.Clear(DeviceColliderBuffer, 0, colliderCount);
      }

      return result != null;
    }

    private static void TransferSlot(RoboticArmDockCargo instance, Thing target, Slot targetSlot)
    {
      try
      {
        Slot handSlot = instance.Slots[0];
        if (handSlot.Contains<DynamicThing>(out DynamicThing heldThing))
        {
          if (targetSlot.IsAllowedType(heldThing))
          {
            if (targetSlot.IsEmpty())
            {
              OnServer.MoveToSlot(heldThing, targetSlot);
            }
            else if (targetSlot.IsSwappable)
            {
              OnServer.SwapSlots(target.ReferenceId, instance.ReferenceId, targetSlot.SlotIndex, handSlot.SlotIndex);
            }
          }
        }
        else if (!targetSlot.IsEmpty())
        {
          OnServer.MoveToSlot(targetSlot.Get(), handSlot);
        }
      }
      catch (ArgumentOutOfRangeException)
      {
      }
    }

    private static bool TryGetValidSlot(Thing target, int slotIndex, out Slot slot)
    {
      slot = null;
      if (target == null || target.BeingDestroyed || target.Slots == null || slotIndex < 0 || slotIndex >= target.Slots.Count)
      {
        return false;
      }

      try
      {
        slot = target.Slots[slotIndex];
        return slot != null && slot.Parent == target && slot.SlotIndex == slotIndex;
      }
      catch (ArgumentOutOfRangeException)
      {
        slot = null;
        return false;
      }
    }

    private static bool CanAccess(DynamicThing target, int slotIndex, Slot slot)
    {
      if (slot == null || !slot.IsInteractable || slot.IsLocked)
      {
        return false;
      }

      if (slot.Type == Slot.Class.Plant)
      {
        return false;
      }

      if (IsSupportedRobot(target))
      {
        return slotIndex >= 0 && slotIndex <= 1;
      }

      return true;
    }

  }
}