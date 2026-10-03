using UnityEngine;

public class DebugStick : Weapon // ponytail: B2 scaffold, delete when Bow.cs exists
{
    public override void OnEquip(PlayerEquipment owner) => Debug.Log("[DebugStick] equipped");
    public override void OnUnequip() => Debug.Log("[DebugStick] unequipped");
}