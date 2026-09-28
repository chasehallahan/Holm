using UnityEngine;

public class DebugCamToggle : MonoBehaviour
{
    [SerializeField] private Camera firstPerson;
    [SerializeField] private Camera debugThirdPerson;
    private PlayerInputReader _input;


    void Awake()
    {
        if (_input is null) _input = GetComponentInParent<PlayerInputReader>();
    }

    void Update()
    {
        if (_input.CrouchPressed)
        {
            bool fp = firstPerson.enabled;
            firstPerson.enabled = !fp;
            debugThirdPerson.enabled = fp;
        }
    }
}
