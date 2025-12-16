using UnityEngine;

public class DebugCamToggle : MonoBehaviour
{
    [SerializeField] private Camera firstPerson;
    [SerializeField] private Camera debugThirdPerson;
    [SerializeField] private PlayerInputReader _input;

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
