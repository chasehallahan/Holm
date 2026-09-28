using UnityEngine;
using System.Collections;
using RootMotion.Dynamics;


#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif


namespace RootMotion.Demos {

	/// <summary>
	/// Switching PuppetMaster.state between Alive, Dead and Frozen
	/// </summary>
	public class Killing : MonoBehaviour {

		[Tooltip("Reference to the PuppetMaster component.")]
		public PuppetMaster puppetMaster;

		[Tooltip("Settings for killing and freezing the puppet.")]
		public PuppetMaster.StateSettings stateSettings = PuppetMaster.StateSettings.Default;

		void Update () {
#if ENABLE_LEGACY_INPUT_MANAGER
			bool rPressed = Input.GetKeyDown(KeyCode.R);
			bool kPressed = Input.GetKeyDown(KeyCode.K);
            bool fPressed = Input.GetKeyDown(KeyCode.F);
#else
			Keyboard kb = Keyboard.current;
			bool rPressed = kb != null && kb.rKey.wasPressedThisFrame;
			bool kPressed = kb != null && kb.kKey.wasPressedThisFrame;
            bool fPressed = kb != null && kb.fKey.wasPressedThisFrame;
#endif

			// Using the state settings defined above
			if (kPressed) puppetMaster.Kill(stateSettings);
			if (fPressed) puppetMaster.Freeze(stateSettings);
			if (rPressed) puppetMaster.Resurrect();

			// Using whatever the current state settings of the puppetMaster instance
			/*
			if (kPressed) puppetMaster.state = PuppetMaster.State.Dead;
			if (fPressed) puppetMaster.state = PuppetMaster.State.Frozen;
			if (rPressed) puppetMaster.state = PuppetMaster.State.Alive;
			*/

			// Using default state settings
			/*
			if (kPressed) puppetMaster.Kill(PuppetMaster.StateSettings.Default);
			if (fPressed) puppetMaster.Freeze(PuppetMaster.StateSettings.Default);
			if (rPressed) puppetMaster.Resurrect();
			*/
		}
	}
}
