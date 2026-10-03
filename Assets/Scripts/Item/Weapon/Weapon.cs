using UnityEngine;

public abstract class Weapon : MonoBehaviour
{
    public abstract void OnEquip(PlayerEquipment owner);
    public abstract void OnUnequip();
    
    
}
