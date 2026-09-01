#if UDONSHARP
using UnityEngine;
using VRC.SDKBase;

namespace DrSakuu.Humr
{
    public class PlayerRecorder : BaseRecorder
    {
        private VRCPlayerApi _player;

        public override void Start()
        {
            _player = Networking.LocalPlayer;
            TargetType = TargetType.BoneRotations;
            // TODO: Hide targetName in inspector
            targetName = _player.displayName;
            RecordingObjects = new object[1 + (int)HumanBodyBones.LastBone];

            RecordIsReady = false;
            base.Start();
        }

        public override void OnAvatarChanged(VRCPlayerApi player)
        {
            if (!player.isLocal || RecordIsReady) return;

            RecordIsReady = true;
            if (recordOnStart && !IsRecording) StartRecording();
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
#endif