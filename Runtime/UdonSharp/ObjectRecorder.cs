using UnityEngine;

#if UDONSHARP
namespace DrSakuu.Humr
{
    public class ObjectRecorder : BaseRecorder
    {
        public override void Start()
        {
            TargetType = TargetType.Object;
            RecordingObjects = new object[3];
            base.Start();
        }

        protected override void UpdateRecordingObjects()
        {
            switch (recordingSpace)
            {
                case RecordingSpace.Local:
                    RecordingObjects[0] = transform.localPosition;
                    RecordingObjects[1] = transform.localRotation;
                    break;
                case RecordingSpace.RecordingStart:
                    RecordingObjects[0] = RecordingStartInverseRotation * (transform.position - RecordingStartPosition);
                    RecordingObjects[1] = RecordingStartInverseRotation * transform.rotation;
                    break;
                default:
                    RecordingObjects[0] = transform.position;
                    RecordingObjects[1] = transform.rotation;
                    break;
            }
            RecordingObjects[2] = transform.localScale;
        }
    }
}
#endif
