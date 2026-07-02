namespace HUMR
{
    public class ObjectRecorder : BaseRecorder
    {
        public override void Start()
        {
            base.Start();
            RecordingType = RecordingType.Object;
            TargetName = gameObject.name;
        }

        public override void StartRecording()
        {
            base.StartRecording();
            RecordObjectTransform();
        }

        protected override void OnRecordTick()
        {
            RecordObjectTransform();
        }

        public override void StopRecording()
        {
            RecordObjectTransform();
            base.StopRecording();
        }

        private void RecordObjectTransform()
        {
            RecordObjects(new object[]{transform.position, transform.rotation, transform.localScale});
        }
    }
}