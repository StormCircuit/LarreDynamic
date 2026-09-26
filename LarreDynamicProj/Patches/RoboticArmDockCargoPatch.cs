using System.Reflection;
using System;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Objects;
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

    private static readonly FieldInfo ArmField = AccessTools.Field(typeof(RoboticArmDock), "_arm");
    private static readonly FieldInfo CurrentSlotIndexField = AccessTools.Field(typeof(RoboticArmDockCargo), "_currentSlotIndex");
    private static readonly MethodInfo GetArmInteractionCellMethod = AccessTools.Method(typeof(RoboticArmDock), "GetArmInteractionCell");
    private static readonly MethodInfo SetTargetLogicableMethod = AccessTools.PropertySetter(typeof(RoboticArmDockCargo), "TargetLogicable");

    [HarmonyPatch(typeof(RoboticArmDockCargo), "DoContextualAction")]
    [HarmonyPostfix]
    private static void Postfix(RoboticArmDockCargo __instance)
    {
      SmallCell interactionCell = (SmallCell)GetArmInteractionCellMethod.Invoke(__instance, null);
      if (interactionCell?.Device != null)
      {
        return;
      }

      if (!TryFindDynamicTarget(__instance, out DynamicThing target))
      {
        return;
      }

      int slotIndex = (int)CurrentSlotIndexField.GetValue(__instance);
      if (slotIndex < 0 || slotIndex >= target.Slots.Count)
      {
        return;
      }

      Slot targetSlot = target.Slots[slotIndex];
      if (!CanAccess(target, slotIndex, targetSlot))
      {
        return;
      }

      Slot handSlot = __instance.Slots[0];
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
            OnServer.SwapSlots(target.ReferenceId, __instance.ReferenceId, targetSlot.SlotIndex, handSlot.SlotIndex);
          }
        }
      }
      else if (!targetSlot.IsEmpty())
      {
        OnServer.MoveToSlot(targetSlot.Get(), handSlot);
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

      if (SetTargetLogicableMethod != null && TryFindDynamicTarget(__instance, out DynamicThing target) && target is ILogicable logicable)
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

    private static bool CanAccess(DynamicThing target, int slotIndex, Slot slot)
    {
      if (slot == null || !slot.IsInteractable || slot.IsLocked)
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