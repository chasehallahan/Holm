using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    [Header("References (exposed to equipped weapons)")]
    [SerializeField] private PlayerInputReader input;
    [SerializeField] private Transform aimPivot;
    [SerializeField] private Transform rightHandTarget;
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform cheekAnchor;

    [Header("Prototype")]
    [Tooltip("Scene weapon toggled with Interact until the pickup slice exists.")]
    [SerializeField] private Weapon prototypeWeapon;

    public Weapon Current { get; private set; }
    public PlayerInputReader Input => input;
    public Transform AimPivot => aimPivot;
    public Transform RightHandTarget => rightHandTarget;
    public Transform LeftHandTarget => leftHandTarget;
    public Transform CheekAnchor => cheekAnchor;

    void Awake()
    {
        if (input == null) input = GetComponent<PlayerInputReader>();
        if (aimPivot == null) aimPivot = transform.Find("CameraRig/CameraPivot");
    }

    void Update()
    {
        if (input.InteractPressed && prototypeWeapon != null)
        {
            if (Current == null) Equip(prototypeWeapon);
            else Unequip();
        }
    }

    public void Equip(Weapon w)
    {
        if (Current != null) Unequip();
        Current = w;
        w.OnEquip(this);
        Debug.Log($"[Equip] {w.name}");
    }

    public void Unequip()
    {
        if (Current == null) return;
        Debug.Log($"[Equip] unequip {Current.name}");
        Current.OnUnequip();
        Current = null;
    }
}
