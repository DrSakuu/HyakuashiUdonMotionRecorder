// HumanoidRecorder.cs
using UnityEngine;
using VRC.SDKBase;

namespace HUMR
{
    public class PlayerRecorder : BaseRecorder
    {
        private VRCPlayerApi _player;
        private bool _avatarLoaded;

        public override void Start()
        {
            _player = Networking.LocalPlayer;
        }

        public override void StartRecording()
        {
            base.StartRecording();
            RecordStart(RecordingType.Player, _player.displayName); 
            RecordPlayerBones(_player, RecordTime);
        }

        protected override void OnRecordTick()
        {
            if (!Utilities.IsValid(_player)) return;
            
            RecordPlayerBones(_player, RecordTime);
        }

        public override void StopRecording()
        {
            RecordStop(RecordingType.Player, _player.displayName);
            base.StopRecording();
        }

        public override void OnAvatarChanged(VRCPlayerApi player)
        {
            if (!player.isLocal || _avatarLoaded || !recordOnStart) return;
            
            StartRecording();
            _avatarLoaded = true;
        }
    }
}