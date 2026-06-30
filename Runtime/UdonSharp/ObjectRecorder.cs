using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace HUMR
{
    public class ObjectRecorder : UdonSharpBehaviour
    {
        [SerializeField, Tooltip("Identifier for the object to record.")]
        private string objectName = "Object";
        [SerializeField, Tooltip("Start recording immediately on scene load.")]
        private bool recordOnStart = true;
        [SerializeField, Tooltip("Frames per second for recording.")]
        private float recordFramerate = 30;
        
        private bool _isRecording;
        
        private float _recordTime;
        private float _recordInterval;
        private float _nextRecordTime;

        private void Start()
        {
            if (recordOnStart) StartRecording();
        }

        private void Update()
        {
            if (!_isRecording) return;
            
            _recordTime += Time.deltaTime;
            if (_recordTime < _nextRecordTime) return;
            _nextRecordTime = _recordTime + _recordInterval;

            UdonUtilities.RecordObjectTransform(transform, objectName, _recordTime);
        }

        public void StartRecording()
        {
            _recordTime = 0f;
            _nextRecordTime = _recordTime;
            _recordInterval = 1f / recordFramerate;

            UdonUtilities.StartRecording(RecordingType.Object, objectName);
            UdonUtilities.RecordObjectTransform(transform, objectName, _recordTime);
            _isRecording = true;
        }


        public void StopRecording()
        {
            UdonUtilities.RecordObjectTransform(transform, objectName, _recordTime);
            UdonUtilities.StopRecording(RecordingType.Object, objectName);
        }

        private void OnDestroy()
        {
            if (_isRecording) StopRecording();
        }
    }
    
}