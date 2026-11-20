#if UDONSHARP
using UnityEngine;
using VRC.SDKBase;

namespace DrSakuu.Humr
{
    public class PlayerRecorder : BaseRecorder
    {
        private VRCPlayerApi _player;
        private int _lastTakeStartFrame = -1;

        public override void Start()
        {
            _player = Networking.LocalPlayer;
            TargetType = TargetType.BoneRotations;
            // TODO: Hide targetName in inspector
            targetName = _player.displayName;
            RecordingObjects = new object[1 + HumanTrait.BoneCount];

            RecordIsReady = false;
            base.Start();
        }

        public override void OnAvatarChanged(VRCPlayerApi player)
        {
            if (!player.isLocal) return;

            if (RecordIsReady)
            {
                RestartTake();
                return;
            }

            RecordIsReady = true;
            if (recordOnStart && !IsRecording) StartRecording();
        }

        public override void OnAvatarEyeHeightChanged(VRCPlayerApi player, float prevEyeHeightAsMeters)
        {
            if (!player.isLocal) return;

            RestartTake();
        }

        public override void StartRecording()
        {
            if (!RecordIsReady) return;

            _lastTakeStartFrame = Time.frameCount;
            base.StartRecording();
        }

        private void RestartTake()
        {
            if (!IsRecording || !RecordIsReady || _lastTakeStartFrame == Time.frameCount) return;

            // Start directly so the changed avatar's pose is not appended to the old take.
            StartRecording();
            HumrLogger.Log($"PlayerRecorder: restarted take after avatar or height change frame={Time.frameCount}");
        }

        protected override void CaptureRecordingStart()
        {
            RecordingStartPosition = _player.GetPosition();
            var heading = Quaternion.Euler(0f, _player.GetRotation().eulerAngles.y, 0f);
            RecordingStartInverseRotation = Quaternion.Inverse(heading);
        }

        protected override void UpdateRecordingObjects()
        {
            var hipsPosition = _player.GetBonePosition(HumanBodyBones.Hips);
            var inverseRotation = Quaternion.identity;
            switch (recordingSpace)
            {
                case RecordingSpace.Local:
                    hipsPosition = transform.InverseTransformPoint(hipsPosition);
                    inverseRotation = Quaternion.Inverse(transform.rotation);
                    break;
                case RecordingSpace.RecordingStart:
                    hipsPosition = RecordingStartInverseRotation * (hipsPosition - RecordingStartPosition);
                    inverseRotation = RecordingStartInverseRotation;
                    break;
            }
            RecordingObjects[0] = hipsPosition;
            for (var i = 0; i < HumanTrait.BoneCount; i++)
            {
                var boneRotation = _player.GetBoneRotation((HumanBodyBones)i);
                RecordingObjects[i + 1] = inverseRotation * boneRotation;
            }
        }
    }
}
#endif
