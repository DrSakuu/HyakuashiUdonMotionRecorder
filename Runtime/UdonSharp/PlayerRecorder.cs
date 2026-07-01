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
            RecordType = RecordingType.Player;
            ObjectName = _player.displayName;
        }

        public override void StartRecording()
        {
            base.StartRecording();
            RecordPlayerBones(_player, RecordTime);
        }

        protected override void OnRecordTick()
        {
            if (!Utilities.IsValid(_player)) return;
            
            RecordPlayerBones(_player, RecordTime);
        }

        public override void OnAvatarChanged(VRCPlayerApi player)
        {
            if (!player.isLocal || _avatarLoaded) return;
            
            _avatarLoaded = true;
            if (recordOnStart) StartRecording();
        }
    }
}