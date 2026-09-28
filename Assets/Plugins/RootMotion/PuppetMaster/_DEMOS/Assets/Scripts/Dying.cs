using UnityEngine;
using System.Collections;
using RootMotion.Dynamics;

#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace RootMotion.Demos {

	// Blending animation with physics to get death procedures that will not penetrate colliders in the scene.
	public class Dying : MonoBehaviour {

		[Tooltip("Reference to the PuppetMaster component.")]
		public PuppetMaster puppetMaster;

		[Tooltip("The speed of fading out PuppetMaster.pinWeight.")]
		public float fadeOutPinWeightSpeed = 5f;

		[Tooltip("The speed of fading out PuppetMaster.muscleWeight.")]
		public float fadeOutMuscleWeightSpeed = 5f;

		[Tooltip("The muscle weight to fade out to.")]
		public float deadMuscleWeight = 0.3f;

		private Animator animator;
		private Vector3 defaultPosition;
		private Quaternion defaultRotation = Quaternion.identity;
		private bool isDead;

		void Start() {
			animator = GetComponent<Animator>();

			// Store the default pos/rot of the character
			defaultPosition = transform.position;
			defaultRotation = transform.rotation;
		}

		void Update () {
#if ENABLE_LEGACY_INPUT_MANAGER
			bool rPressed = Input.GetKeyDown(KeyCode.R);
			bool dPressed = Input.GetKeyDown(KeyCode.D);
#else
			Keyboard kb = Keyboard.current;
			bool rPressed = kb != null && kb.rKey.wasPressedThisFrame;
			bool dPressed = kb != null && kb.dKey.wasPressedThisFrame;
#endif

			// Starting the death procedure
			if (dPressed && !isDead) {
				// Play the death animation
				animator.CrossFadeInFixedTime("Die Backwards", 0.2f);

				// Start fading out PM pin and muscle weights
				if (puppetMaster != null) {
					StopAllCoroutines();
					StartCoroutine(FadeOutPinWeight());
					StartCoroutine(FadeOutMuscleWeight());
				}

				// Just making sure we don't kill the puppet twice
				isDead = true;
			}

			// Resetting the character and PuppetMaster weights
			if (rPressed && isDead) {
				transform.position = defaultPosition;
				transform.rotation = defaultRotation;

				animator.Play("Idle", 0, 0f);

				if (puppetMaster != null) {
					StopAllCoroutines();
					puppetMaster.pinWeight = 1f;
					puppetMaster.muscleWeight = 1f;
				}

				isDead = false;
			}
		}

		// Fading out puppetMaster.pinWeight to zero
		private IEnumerator FadeOutPinWeight() {
			while (puppetMaster.pinWeight > 0f) {
				puppetMaster.pinWeight = Mathf.MoveTowards(puppetMaster.pinWeight, 0f, Time.deltaTime * fadeOutPinWeightSpeed);
				yield return null;
			}
		}

		// Fading out puppetMaster.muscleWeight to deadMuscleWeight
		private IEnumerator FadeOutMuscleWeight() {
			while (puppetMaster.muscleWeight > 0f) {
				puppetMaster.muscleWeight = Mathf.MoveTowards(puppetMaster.muscleWeight, deadMuscleWeight, Time.deltaTime * fadeOutMuscleWeightSpeed);
				yield return null;
			}
		}
	}
}
