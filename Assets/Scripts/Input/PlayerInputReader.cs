using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerInputReader : MonoBehaviour
{
    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; }
    public bool AttackHeld { get; private set; }
    public bool AttackPressed { get; private set; }
    public bool AttackReleased { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool CrouchHeld{ get; private set; }
    public bool CrouchPressed { get; private set; }
    public bool CrouchReleased { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool SprintPressed { get; private set; }
    public bool SprintReleased { get; private set; }

    private PlayerInput _playerInput;

    void OnEnable()
    {
        _playerInput = GetComponent<PlayerInput>();
        _playerInput.onActionTriggered += OnAction;
    }

    void OnDisable()
    {
        _playerInput.onActionTriggered -= OnAction;
    }

    void LateUpdate()
    {
        AttackPressed = false;
        AttackReleased = false;
        InteractPressed = false;
        CrouchPressed = false;
        CrouchReleased = false;
        JumpPressed = false;
        SprintPressed = false;
        SprintReleased = false;
    }

    void OnAction(InputAction.CallbackContext ctx)
    {
        if (ctx.action.actionMap.name != "Player") return;

        switch (ctx.action.name)
        {
            case "Move":
                Move = ctx.ReadValue<Vector2>();
                if (ctx.canceled) Move = Vector2.zero;
                break;

            case "Look":
                Look = ctx.ReadValue<Vector2>();
                break;

            case "Attack":
                if (ctx.started)
                {
                    AttackHeld = true;
                    AttackPressed = true;
                }
                if (ctx.canceled)
                {
                    AttackHeld = false;
                    AttackReleased = true;
                }
                break;

            case "Interact":
                if (ctx.started) InteractPressed = true;
                break;

            case "Crouch":
                if (ctx.started)
                {
                    CrouchHeld = true;
                    CrouchPressed = true;
                }
                if (ctx.canceled)
                {
                    CrouchHeld = false;
                    CrouchReleased= true;
                }
                break;

            case "Jump":
                if (ctx.started) JumpPressed = true;
                break;

            case "Sprint":
                if (ctx.started)
                {
                    SprintHeld = true;
                    SprintPressed = true;
                }
                if (ctx.canceled)
                {
                    SprintHeld = false;
                    SprintReleased = true;
                }
                break;
        }
    }
}
