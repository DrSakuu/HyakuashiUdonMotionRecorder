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
        }

        public override void StartRecording()
        {
            base.StartRecording();
            RecordPlayerBones(_player);
        }

        protected override void OnRecordTick()
        {
            if (!Utilities.IsValid(_player)) return;

            RecordPlayerBones(_player);
        }

        public override void OnAvatarChanged(VRCPlayerApi player)
        {
            if (!player.isLocal || _avatarLoaded) return;

            _avatarLoaded = true;
            if (recordOnStart) StartRecording();
        }

        private void RecordPlayerBones(VRCPlayerApi player)
        {
            const int totalElements = 1 + (int)HumanBodyBones.LastBone;
            var objectList = new object[totalElements];
            var hipsPosition = player.GetBonePosition(HumanBodyBones.Hips);
            objectList[0] = hipsPosition;
            for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var boneRotation = player.GetBoneRotation((HumanBodyBones)i);
                objectList[i + 1] = boneRotation;
            }

            RecordObjects(objectList);
        }
    }
}