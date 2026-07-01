using System.Collections.Generic;
using UnityEngine;

namespace HUMR
{
    internal class AvatarPoseSnapshot
    {
        private Vector3 _savedRootPosition;
        private Quaternion _savedRootRotation;
        private readonly List<BoneSnapshot> _avatarSnapshot = new List<BoneSnapshot>();

        public void Take(Transform rootTransform, Animator animator)
        {
            if (animator == null) return;

            _savedRootPosition = rootTransform.position;
            _savedRootRotation = rootTransform.rotation;
            _avatarSnapshot.Clear();
            
            for (var i = 0; i < HumanTrait.BoneName.Length; i++)
            {
                var boneTransform = animator.GetBoneTransform((HumanBodyBones)i);
                if (boneTransform == null) continue;
            
                _avatarSnapshot.Add(new BoneSnapshot
                {
                    Transform = boneTransform,
                    LocalPosition = boneTransform.localPosition,
                    LocalRotation = boneTransform.localRotation
                });
            }
        }

        public void Restore(Transform rootTransform)
        {
            rootTransform.position = _savedRootPosition;
            rootTransform.rotation = _savedRootRotation;
            
            foreach (var snapshot in _avatarSnapshot)
            {
                if (snapshot.Transform == null) continue;
                snapshot.Transform.localPosition = snapshot.LocalPosition;
                snapshot.Transform.localRotation = snapshot.LocalRotation;
            }
        }
    }
}