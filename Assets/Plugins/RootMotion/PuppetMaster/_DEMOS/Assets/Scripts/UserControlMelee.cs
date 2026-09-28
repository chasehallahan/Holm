using System.Collections;
using UnityEngine;


#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace RootMotion.Demos {
	
	/// <summary>
	/// User input for a third person melee character controller.
	/// </summary>
	public class UserControlMelee : UserControlThirdPerson {

		public KeyCode hitKey;

		protected override void Update () {
			base.Update();

#if ENABLE_LEGACY_INPUT_MANAGER
			bool input = Input.GetKey(hitKey);
#else
			Keyboard kb = Keyboard.current;
			bool input = kb != null && kb.eKey.isPressed;
#endif
			state.actionIndex = input? 1: 0;
		}
	}
}
