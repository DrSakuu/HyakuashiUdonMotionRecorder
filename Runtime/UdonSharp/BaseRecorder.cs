using System.Globalization;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace HUMR
{
    public enum FrameType
    {
        Unknown,
        Legacy,
        BoneRotations,
        Object
    }

    public class BaseRecorder : UdonSharpBehaviour
    {
        [SerializeField] [Tooltip("Start recording button, connect to StartRecording event.")]
        private Button startRecordButton;

        [SerializeField] [Tooltip("Stop recording button, connect to StartRecording event.")]
        private Button stopRecordButton;

        [SerializeField] [Tooltip("Start recording immediately on scene load.")]
        protected bool recordOnStart = true;

        [SerializeField] [Tooltip("Frames per second for recording.")]
        protected float recordFramerate = 30;

        protected FrameType FrameType = FrameType.Object;
        protected string TargetName = "Target";
        protected object[] RecordingObjects;

        private bool _isRecording;
        private float _recordTime;
        private float _nextRecordTime;
        private float _recordInterval;
        private int _takeNumber = -1;

        public virtual void Start()
        {
            if (recordOnStart) StartRecording();
        }

        private void Update()
        {
            if (!_isRecording) return;

            _recordTime += Time.deltaTime;
            if (_recordTime < _nextRecordTime) return;
            _nextRecordTime = _recordTime + _recordInterval;

            OnRecordTick();
        }

        private void OnDestroy()
        {
            if (_isRecording) StopRecording();
        }

        public virtual void StartRecording()
        {
            _recordTime = 0f;
            _nextRecordTime = _recordTime;
            _recordInterval = 1f / recordFramerate;
            _takeNumber++;
            _isRecording = true;
            RecordObjects();
            UpdateUI();
        }

        public virtual void StopRecording()
        {
            RecordObjects();
            _isRecording = false;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (startRecordButton != null) startRecordButton.gameObject.SetActive(!_isRecording);
            if (stopRecordButton != null) stopRecordButton.gameObject.SetActive(_isRecording);
        }

        private void OnRecordTick()
        {
            RecordObjects();
        }

        protected virtual void UpdateRecordingObjects()
        {
        }

        public override void Interact()
        {
            if (_isRecording) StopRecording();
            else StartRecording();
        }

        private void RecordObjects()
        {
            var timeStr = _recordTime.ToString(CultureInfo.InvariantCulture);
            var typeStr = HumrLogger.RecordingTypeToString(FrameType);
            var outputString = string.Join(HumrLogger.VariableDelimiter, HumrLogger.RecordingTag, TargetName, _takeNumber, typeStr, timeStr);

            UpdateRecordingObjects();
            foreach (var recObj in RecordingObjects)
            {
                if (recObj == null) continue;

                switch (recObj.GetType().Name)
                {
                    case "Vector3":
                    {
                        var vector3Str = HumrLogger.FormatVector3Components((Vector3)recObj);
                        outputString = string.Join(HumrLogger.VariableDelimiter, outputString, vector3Str);
                        break;
                    }
                    case "Quaternion":
                    {
                        var quaternionStr = HumrLogger.FormatQuaternionComponents((Quaternion)recObj);
                        outputString = string.Join(HumrLogger.VariableDelimiter, outputString, quaternionStr);
                        break;
                    }
                    default:
                        outputString = string.Join(HumrLogger.VariableDelimiter, recObj.ToString());
                        break;
                }
            }

            HumrLogger.Log(outputString);
        }
    }
}