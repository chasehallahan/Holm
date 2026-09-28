using UnityEngine;
using System.Collections;
using RootMotion.Dynamics;

#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif


namespace RootMotion.Demos {
	
	public class SkeletonShooter : MonoBehaviour {

		public PuppetMaster puppetMaster;
		public Skeleton skeleton;
		public MuscleRemoveMode removeMuscleMode;
		public LayerMask layers;
		public float unpin = 10f;
		public float force = 10f;
		public ParticleSystem particles;

        // Update is called once per frame
		void Update () {
            #if ENABLE_LEGACY_INPUT_MANAGER
			bool rPressed = Input.GetKeyDown(KeyCode.R);

            bool clicked = Input.GetMouseButtonDown(0);
            Vector2 mousePos = Input.mousePosition;
#else
			Keyboard kb = Keyboard.current;
			bool rPressed = kb != null && kb.rKey.wasPressedThisFrame;

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

					// If is a muscle...
					if (broadcaster != null) {
						broadcaster.Hit(unpin, ray.direction * force, hit.point);

                        // Remove the muscle and its children
                        broadcaster.puppetMaster.RemoveMuscleRecursive(broadcaster.puppetMaster.muscles[broadcaster.muscleIndex].joint, true, true, removeMuscleMode);
                    } else {
						// Not a muscle (any more)
						//var joint = hit.collider.attachedRigidbody.GetComponent<ConfigurableJoint>();
						//if (joint != null) Destroy(joint);

						// Add force
						hit.collider.attachedRigidbody.AddForceAtPosition(ray.direction * force, hit.point);
					}

					// Particle FX
					particles.transform.position = hit.point;
					particles.transform.rotation = Quaternion.LookRotation(-ray.direction);
					particles.Emit(5);
				}
			}

			// Reattach all the missing muscles
			if (rPressed) {
				puppetMaster.Rebuild();
				skeleton.OnRebuild();
			}
		}
	}
}