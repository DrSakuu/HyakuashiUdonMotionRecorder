using HarmonyLib;
using UnityEngine;
using VRC.SDKBase;

namespace HUMR
{
    public class PlayerRecorder : BaseRecorder
    {
        private bool _avatarLoaded;
        private VRCPlayerApi _player;

        public override void Start()
        {
            _player = Networking.LocalPlayer;
            RecordingType = RecordingType.BoneRotations;
            TargetName = _player.displayName;
            RecordingObjects = new object[1+(int)HumanBodyBones.LastBone];
        }

        public override void OnAvatarChanged(VRCPlayerApi player)
        {
            if (!player.isLocal || _avatarLoaded) return;

            _avatarLoaded = true;
            if (recordOnStart) StartRecording();
        }

        protected override void UpdateRecordingObjects()
        {
            var hipsPosition = _player.GetBonePosition(HumanBodyBones.Hips);
            RecordingObjects[0] = hipsPosition;
            for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var boneRotation = _player.GetBoneRotation((HumanBodyBones)i);
                RecordingObjects[i + 1] = boneRotation;
            }
        }
    }
}