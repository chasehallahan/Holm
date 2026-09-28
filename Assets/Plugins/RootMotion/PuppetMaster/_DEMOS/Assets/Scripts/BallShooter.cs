using UnityEngine;
using System.Collections;

#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace RootMotion.Demos {

	// Just shooting objects from the camera towards the mouse position.
	public class BallShooter : MonoBehaviour {

		public GameObject ball;
		public Vector3 spawnOffset = new Vector3(0f, 0.5f, 0f);
		public Vector3 force = new Vector3(0f, 0f, 7f);
		public float mass = 3f;

		void Update () {
#if ENABLE_LEGACY_INPUT_MANAGER
			bool clicked = Input.GetMouseButtonDown(0);
			Vector3 mousePos = Input.mousePosition;
#else
            Mouse mouse = Mouse.current;
            bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
            Vector2 mousePos = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#endif

			if (clicked) {
				GameObject b = (GameObject)GameObject.Instantiate(ball, transform.position + transform.rotation * spawnOffset, transform.rotation);
				var r = b.GetComponent<Rigidbody>();

				if (r != null) {
					r.mass = mass;

					Ray ray = Camera.main.ScreenPointToRay(mousePos);
					r.AddForce(Quaternion.LookRotation(ray.direction) * force, ForceMode.VelocityChange);
				}
			}
		}
	}
}
