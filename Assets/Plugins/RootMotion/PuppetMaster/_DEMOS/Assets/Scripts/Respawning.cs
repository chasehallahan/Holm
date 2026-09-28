using UnityEngine;
using System.Collections;
using RootMotion.Dynamics;

#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace RootMotion.Demos {

	// Respawning BehaviourPuppet at a random position/rotation
	public class Respawning : MonoBehaviour {

		[Tooltip("Pooled characters will be parented to this deactivated GameObject.")] public Transform pool;
		[Tooltip("Reference to the BehaviourPuppet component of the character you wish to respawn.")] public BehaviourPuppet puppet;
		[Tooltip("The animation to play on respawn.")] public string idleAnimation;

		private bool isPooled { get { return puppet.transform.root == pool; }}
		private Transform puppetRoot;

		void Start() {
			// Store the root Transform of the puppet
			puppetRoot = puppet.transform.root;

			// Deactivate the pool so anyhting parented to it would be deactivated too
			pool.gameObject.SetActive(false);
		}

		void Update () {
#if ENABLE_LEGACY_INPUT_MANAGER
			bool a1Pressed = Input.GetKeyDown(KeyCode.Alpha1);
			bool a2Pressed = Input.GetKeyDown(KeyCode.Alpha2);
            bool a3Pressed = Input.GetKeyDown(KeyCode.Alpha3);
			bool pPressed = Input.GetKeyDown(KeyCode.P);
			bool rPressed = Input.GetKeyDown(KeyCode.R);
#else
			Keyboard kb = Keyboard.current;
			bool a1Pressed = kb != null && kb.digit1Key.wasPressedThisFrame;
			bool a2Pressed = kb != null && kb.digit2Key.wasPressedThisFrame;
            bool a3Pressed = kb != null && kb.digit3Key.wasPressedThisFrame;
			bool pPressed = kb != null && kb.pKey.wasPressedThisFrame;
			bool rPressed = kb != null && kb.rKey.wasPressedThisFrame;
#endif

			if (a1Pressed) puppet.puppetMaster.state = PuppetMaster.State.Alive;
			if (a2Pressed) puppet.puppetMaster.state = PuppetMaster.State.Dead;
			if (a3Pressed) puppet.puppetMaster.state = PuppetMaster.State.Frozen;

			if (pPressed && !isPooled) {
				Pool();
			}

			// Pool/Respawn from the pool
			if (rPressed) {
                // Respawn in random position/rotation
                Vector2 rndCircle = UnityEngine.Random.insideUnitCircle * 2f;
                
                Respawn(new Vector3(rndCircle.x, 0f, rndCircle.y), Quaternion.LookRotation(new Vector3(-rndCircle.x, 0f, -rndCircle.y)));
			}
		}

		private void Pool() {
            puppetRoot.parent = pool;
		}

		private void Respawn(Vector3 position, Quaternion rotation) {
			puppet.puppetMaster.state = PuppetMaster.State.Alive;
            if (puppet.puppetMaster.targetAnimator.gameObject.activeInHierarchy) puppet.puppetMaster.targetAnimator.Play(idleAnimation, 0, 0f);
            puppet.SetState(BehaviourPuppet.State.Puppet);
			puppet.puppetMaster.Teleport(position, rotation, true);

            puppetRoot.parent = null;
		}
	}
}
