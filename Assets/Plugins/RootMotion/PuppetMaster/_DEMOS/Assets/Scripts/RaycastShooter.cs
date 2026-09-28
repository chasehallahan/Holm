using UnityEngine;
using System.Collections;
using RootMotion.Dynamics;

#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace RootMotion.Demos {

	public class RaycastShooter : MonoBehaviour {

		public LayerMask layers;
		public float unpin = 10f;
		public float force = 10f;
		public ParticleSystem blood;

        // Update is called once per frame
        void Update () {
#if ENABLE_LEGACY_INPUT_MANAGER
            bool clicked = Input.GetMouseButtonDown(0);
            Vector2 mousePos = Input.mousePosition;
#else
            Mouse mouse = Mouse.current;
            bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
            Vector2 mousePos = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#endif

			if (clicked) {
				Ray ray = Camera.main.ScreenPointToRay(mousePos);

				// Raycast to find a ragdoll collider
				RaycastHit hit = new RaycastHit();
				if (Physics.Raycast(ray, out hit, 100f, layers)) {
					var broadcaster = hit.collider.attachedRigidbody.GetComponent<MuscleCollisionBroadcaster>();

					if (broadcaster != null) {
						broadcaster.Hit(unpin, ray.direction * force, hit.point);
                        

						blood.transform.position = hit.point;
						blood.transform.rotation = Quaternion.LookRotation(-ray.direction);
						blood.Emit(5);
					}
				}
			}
		}
	}
}
