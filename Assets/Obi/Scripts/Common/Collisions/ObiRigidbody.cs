using UnityEngine;
using System;
using System.Collections;

namespace Obi
{

    /**
	 * Small helper class that lets you specify Obi-only properties for rigidbodies.
	 */

    [ExecuteInEditMode]
    [RequireComponent(typeof(Rigidbody))]
    public class ObiRigidbody : ObiRigidbodyBase
    {
        public Rigidbody unityRigidbody { get; private set; }

        public Vector3 position => unityRigidbody.position;
        public Quaternion rotation => unityRigidbody.rotation;

        public Vector3 linearVelocity { get; protected set; }
        public Vector3 angularVelocity { get; protected set; }

        private Quaternion prevRotation;
        private Vector3 prevPosition;

        public override void OnEnable()
        {
            unityRigidbody = GetComponent<Rigidbody>();
            prevPosition = unityRigidbody.position;
            prevRotation = unityRigidbody.rotation;

            linearVelocity = unityRigidbody.velocity;
            angularVelocity = unityRigidbody.angularVelocity;
            base.OnEnable();
        }

        private void UpdateVelocities(float stepTime)
        {
            // differentiate positions/orientations to get our own velocites for kinematic objects.
            // when calling Physics.Simulate, MovePosition/Rotation do not work correctly. Also useful for animations.
            if (unityRigidbody.isKinematic)
            {
                // differentiate positions to obtain linear velocity:
                linearVelocity = (unityRigidbody.position - prevPosition) / stepTime;

                // differentiate rotations to obtain angular velocity:
                Quaternion delta = unityRigidbody.rotation * Quaternion.Inverse(prevRotation);
                angularVelocity = new Vector3(delta.x, delta.y, delta.z) * 2.0f / stepTime;
            }
            else
            {
                // if the object is non-kinematic, just copy velocities.
                linearVelocity = unityRigidbody.velocity;
                angularVelocity = unityRigidbody.angularVelocity;
            }

            prevPosition = unityRigidbody.position;
            prevRotation = unityRigidbody.rotation;
        }

        public override void UpdateIfNeeded(float stepTime)
        {
            UpdateVelocities(stepTime);
            var world = ObiColliderWorld.GetInstance();

            var rb = world.rigidbodies[handle.index];
            rb.FromRigidbody(this);
            world.rigidbodies[handle.index] = rb;
        }

        /**
		 * Reads velocities back from the solver.
		 */
        public override void UpdateVelocities(Vector3 linearDelta, Vector3 angularDelta)
        {
            // kinematic rigidbodies are passed to Obi with zero velocity, so we must ignore the new velocities calculated by the solver:
            if (Application.isPlaying && !(unityRigidbody.isKinematic || kinematicForParticles))
            {
                if (Vector3.SqrMagnitude(linearDelta) > 0.00001f || Vector3.SqrMagnitude(angularDelta) > 0.00001f)
                {
                    unityRigidbody.velocity += linearDelta;
                    unityRigidbody.angularVelocity += angularDelta;
                }
            }
        }
    }
}

