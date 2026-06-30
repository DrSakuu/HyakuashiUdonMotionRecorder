// ObjectRecorder.cs
using UnityEngine;

namespace HUMR
{
    public class ObjectRecorder : BaseRecorder
    {
        [SerializeField, Tooltip("Identifier for the object to record.")]
        private string objectName = "Object";

        public override void StartRecording()
        {
            base.StartRecording();
            RecordStart(RecordingType.Object, objectName);
            RecordObjectTransform(transform, objectName, RecordTime);
        }

        protected override void OnRecordTick()
        {
            RecordObjectTransform(transform, objectName, RecordTime);
        }

        public override void StopRecording()
        {
            RecordObjectTransform(transform, objectName, RecordTime);
            RecordStop(RecordingType.Object, objectName);
            base.StopRecording();
        }
    }
}