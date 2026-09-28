using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.Dynamics;


#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace RootMotion.Demos
{

    // Simple snowboard controller template.
    public class BoardController : MonoBehaviour
    {

        public int groundLayer = 4;
        public Transform rotationTarget;
        public float torque = 1f;
        public float skidDrag = 0.5f;
        public float turnSensitivity = 15f;

        private Rigidbody r;
        private bool isGrounded;

        void Awake()
        {
            r = GetComponent<Rigidbody>();
        }

        void Update()
        {
            // Turning the board
            float turn = GetInputVector().x;
            rotationTarget.rotation = Quaternion.AngleAxis(turn * turnSensitivity * Mathf.Min(r.linearVelocity.sqrMagnitude * 0.2f, 1f) * Time.deltaTime, Vector3.up) * rotationTarget.rotation;
        }

        private void FixedUpdate()
        {
            // Add torque to the Rigidbody to make it follow the rotation target
            Vector3 angularAcc = PhysXTools.GetAngularAcceleration(r.rotation, rotationTarget.rotation);
            r.AddTorque(angularAcc * torque);

            // Add snowboard-like skid drag
            if (isGrounded)
            {
                Vector3 velocity = r.linearVelocity;
                Vector3 skid = V3Tools.ExtractHorizontal(velocity, r.rotation * Vector3.up, 1f);
                skid = Vector3.Project(velocity, r.rotation * Vector3.right);

                r.linearVelocity = velocity - Vector3.ClampMagnitude(skid * skidDrag * Time.deltaTime, skid.magnitude);
            }
        }

        // Check if the board is grounded
        void OnCollisionEnter(Collision c)
        {
            if (c.collider.gameObject.layer != groundLayer) return;
            isGrounded = true;
        }

        void OnCollisionStay(Collision c)
        {
            if (c.collider.gameObject.layer != groundLayer) return;
            isGrounded = true;
        }

        void OnCollisionExit(Collision c)
        {
            if (c.collider.gameObject.layer != groundLayer) return;
            isGrounded = false;
        }

        // Reads the Input to get the movement direction.
		private Vector3 GetInputVector() {
#if ENABLE_LEGACY_INPUT_MANAGER
			Vector3 d = new Vector3(
				Input.GetAxis("Horizontal"),
				0f,
				Input.GetAxis("Vertical")
			);
#else
			Vector3 d = ReadMovementAxes(smooth: true);
#endif
			d.z += Mathf.Abs(d.x) * 0.05f;
			d.x -= Mathf.Abs(d.z) * 0.05f;
			return d;
		}
 
		private Vector3 GetInputVectorRaw() {
#if ENABLE_LEGACY_INPUT_MANAGER
			return new Vector3(
				Input.GetAxisRaw("Horizontal"),
				0f,
				Input.GetAxisRaw("Vertical")
			);
#else
			return ReadMovementAxes(smooth: false);
#endif
		}
 
#if !ENABLE_LEGACY_INPUT_MANAGER
		// Reads WASD / arrow keys from the new Input System.
		// smooth=true mimics GetAxis (clamped float), smooth=false mimics GetAxisRaw (-1/0/1).
		private Vector3 smoothInput, smoothInputV;

		private Vector3 ReadMovementAxes(bool smooth) {
			Keyboard kb = Keyboard.current;
			if (kb == null) return Vector3.zero;

			float h = (kb.dKey.isPressed || kb.rightArrowKey.isPressed) ? 1f :
				          (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  ? -1f : 0f;
			float v = (kb.wKey.isPressed || kb.upArrowKey.isPressed)    ? 1f :
				          (kb.sKey.isPressed || kb.downArrowKey.isPressed)  ? -1f : 0f;

			Vector3 raw = new Vector3(h, 0f, v);
			if (!smooth) return raw;


			//smoothInput = Vector3.MoveTowards(smoothInput, raw, Time.deltaTime);
			smoothInput = Vector3.SmoothDamp(smoothInput, raw, ref smoothInputV, 0.2f);
			return smoothInput;
		}
#endif
    }
}
