namespace HUMR
{
    public class ObjectRecorder : BaseRecorder
    {
        public override void Start()
        {
            base.Start();
            RecordType = RecordingType.Object;
            ObjectName = gameObject.name;
        }

        public override void StartRecording()
        {
            base.StartRecording();
            RecordObjectTransform(transform, ObjectName, RecordTime);
        }

        protected override void OnRecordTick()
        {
            RecordObjectTransform(transform, ObjectName, RecordTime);
        }

        public override void StopRecording()
        {
            RecordObjectTransform(transform, ObjectName, RecordTime);
            base.StopRecording();
        }
    }
}