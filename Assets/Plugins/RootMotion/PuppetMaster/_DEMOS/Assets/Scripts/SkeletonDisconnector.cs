using UnityEngine;
using System.Collections;
using RootMotion.Dynamics;

#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace RootMotion.Demos
{

    public class SkeletonDisconnector : MonoBehaviour
    {

        public BehaviourPuppet puppet;
        public Skeleton skeleton;
        public MuscleDisconnectMode disconnectMuscleMode;
        public LayerMask layers;
        public float unpin = 10f;
        public float force = 10f;
        public ParticleSystem particles;

        public PropMuscle propMuscle;
        public PuppetMasterProp prop;

        // Update is called once per frame
        void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
			bool rPressed = Input.GetKeyDown(KeyCode.R);
			bool dPressed = Input.GetKeyDown(KeyCode.D);
            bool pPressed = Input.GetKeyDown(KeyCode.P);
            bool mPressed = Input.GetKeyDown(KeyCode.M);

            bool clicked = Input.GetMouseButtonDown(0);
            Vector2 mousePos = Input.mousePosition;
#else
			Keyboard kb = Keyboard.current;
			bool rPressed = kb != null && kb.rKey.wasPressedThisFrame;
			bool dPressed = kb != null && kb.dKey.wasPressedThisFrame;
            bool pPressed = kb != null && kb.pKey.wasPressedThisFrame;
			bool mPressed = kb != null && kb.mKey.wasPressedThisFrame;

            Mouse mouse = Mouse.current;
            bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
            Vector2 mousePos = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#endif

            // Switching modes
            if (mPressed)
            {
                if (disconnectMuscleMode == MuscleDisconnectMode.Sever) disconnectMuscleMode = MuscleDisconnectMode.Explode;
                else disconnectMuscleMode = MuscleDisconnectMode.Sever;
            }

            // Pick up prop
            if (pPressed)
            {
                propMuscle.currentProp = prop;

                // If skeleton is dead, need to resurrect it, as attaching prop also reconnects all parent muscles
                if (puppet.puppetMaster.muscles[0].state.isDisconnected) skeleton.OnRebuild();
            }

            // Drop prop
            if (dPressed)
            {
                propMuscle.currentProp = null;
            }

            // Shooting
            if (clicked)
            {
                Ray ray = Camera.main.ScreenPointToRay(mousePos);

                // Raycast to find a ragdoll collider
                RaycastHit hit = new RaycastHit();
                if (Physics.Raycast(ray, out hit, 100f, layers))
                {
                    var broadcaster = hit.collider.attachedRigidbody.GetComponent<MuscleCollisionBroadcaster>();

                    // If is a muscle...
                    if (broadcaster != null)
                    {
                        broadcaster.Hit(unpin, ray.direction * force, hit.point);

                        // Remove the muscle and its children
                        broadcaster.puppetMaster.DisconnectMuscleRecursive(broadcaster.muscleIndex, disconnectMuscleMode);
                    }
                    else
                    {
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
            if (rPressed)
            {
                puppet.puppetMaster.ReconnectMuscleRecursive(0);
                skeleton.OnRebuild();
            }
        }
    }
}