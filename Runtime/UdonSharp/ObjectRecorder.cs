namespace HUMR
{
    public class ObjectRecorder : BaseRecorder
    {
        public override void Start()
        {
            base.Start();
            RecordType = RecordingType.Object;
            TargetName = gameObject.name;
        }

        public override void StartRecording()
        {
            base.StartRecording();
            RecordObjectTransform(transform, TargetName, RecordTime);
        }

        protected override void OnRecordTick()
        {
            RecordObjectTransform(transform, TargetName, RecordTime);
        }

        public override void StopRecording()
        {
            RecordObjectTransform(transform, TargetName, RecordTime);
            base.StopRecording();
        }
    }
}