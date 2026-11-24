using System;
using UnityEngine;

namespace TacticalGame
{
    // Moves the template rig's legs with ground displacement while keeping its upper-body pose.
    public sealed class GuardLocomotion : MonoBehaviour
    {
        Transform leftThigh, rightThigh, leftShin, rightShin;
        Quaternion leftThighRest, rightThighRest, leftShinRest, rightShinRest;
        Vector3 previousPosition;
        float phase, weight;
        bool capturedPose;
        public bool HasLegRig => leftThigh != null && rightThigh != null && leftShin != null && rightShin != null;
        public bool HasStride => weight > .01f;

        void Awake()
        {
            var bones = GetComponentsInChildren<Transform>(true);
            leftThigh = Array.Find(bones, bone => bone.name == "Left_UpperLeg");
            rightThigh = Array.Find(bones, bone => bone.name == "Right_UpperLeg");
            leftShin = Array.Find(bones, bone => bone.name == "Left_LowerLeg");
            rightShin = Array.Find(bones, bone => bone.name == "Right_LowerLeg");
            previousPosition = transform.position;
        }

        void LateUpdate()
        {
            Vector3 step = transform.position - previousPosition;
            previousPosition = transform.position;
            if (!HasLegRig || Time.deltaTime <= 0f) return;
            if (!capturedPose)
            {
                leftThighRest = leftThigh.localRotation;
                rightThighRest = rightThigh.localRotation;
                leftShinRest = leftShin.localRotation;
                rightShinRest = rightShin.localRotation;
                capturedPose = true;
            }

            step.y = 0f;
            float distance = step.magnitude;
            bool walking = distance < 1f && distance > .001f;
            weight = Mathf.MoveTowards(weight, walking ? 1f : 0f, Time.deltaTime * 12f);
            if (walking) phase = Mathf.Repeat(phase + distance * (2f * Mathf.PI / 1.7f), 2f * Mathf.PI);
            float swing = Mathf.Sin(phase);
            SetPitch(leftThigh, leftThighRest, 26f * swing);
            SetPitch(rightThigh, rightThighRest, -26f * swing);
            SetPitch(leftShin, leftShinRest, 32f * Mathf.Max(0f, Mathf.Cos(phase)));
            SetPitch(rightShin, rightShinRest, 32f * Mathf.Max(0f, -Mathf.Cos(phase)));
        }

        void SetPitch(Transform bone, Quaternion rest, float degrees)
        {
            Vector3 localRight = bone.parent.InverseTransformDirection(transform.right);
            bone.localRotation = Quaternion.AngleAxis(degrees * weight, localRight) * rest;
        }
    }
}
