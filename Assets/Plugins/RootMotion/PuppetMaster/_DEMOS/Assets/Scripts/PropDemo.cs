using UnityEngine;
using System.Collections;
using RootMotion.Dynamics;

#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace RootMotion.Demos {

	// Code example for picking up/dropping props.
	public class PropDemo : MonoBehaviour {

		[Tooltip("The Prop you wish to pick up.")] 
		public PuppetMasterProp prop;

		[Tooltip("The Prop Muscle of the left hand.")] 
		public PropMuscle propMuscleLeft;

		[Tooltip("The Prop Muscle of the right hand.")] 
		public PropMuscle propMuscleRight;

		[Tooltip("If true, the prop will be picked up when PuppetMaster initiates")]
		public bool pickUpOnAwake;

		private bool right = true;

		void Start() {
            if (pickUpOnAwake) connectTo.currentProp = prop;
		}

		void Update () {
#if ENABLE_LEGACY_INPUT_MANAGER
			bool pPressed = Input.GetKeyDown(KeyCode.P);
			bool xPressed = Input.GetKeyDown(KeyCode.X);
            bool sPressed = Input.GetKeyDown(KeyCode.S);
#else
			Keyboard kb = Keyboard.current;
			bool pPressed = kb != null && kb.pKey.wasPressedThisFrame;
			bool xPressed = kb != null && kb.xKey.wasPressedThisFrame;
            bool sPressed = kb != null && kb.sKey.wasPressedThisFrame;
#endif

			// Picking up
			if (pPressed) {
				// Makes the prop root drop any existing props and pick up the newly assigned one.
				connectTo.currentProp = prop;
			}

			// Dropping
			if (xPressed) {
				// By setting the prop root's currentProp to null, the prop connected to it will be dropped.
				connectTo.currentProp = null;
			}

			// Switching prop roots.
			if (sPressed) {
				// Switch hands
				right = !right;

				// Assign the prop to the other hand
				connectTo.currentProp = prop;
			}
		}

		private PropMuscle connectTo {
			get {
				return right? propMuscleRight: propMuscleLeft;
			}
		}
	}
}
