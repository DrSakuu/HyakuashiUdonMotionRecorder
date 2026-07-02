namespace HUMR
{
    public class ObjectRecorder : BaseRecorder
    {
        public override void Start()
        {
            base.Start();
            FrameType = FrameType.Object;
            TargetName = gameObject.name;
            RecordingObjects = new object[3];
        }

        protected override void UpdateRecordingObjects()
        {
            RecordingObjects[0] = transform.position;
            RecordingObjects[1] = transform.rotation;
            RecordingObjects[2] = transform.localScale;
        }
    }
}