using System.Globalization;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace HUMR
{
    public enum RecordingType
    {
        Unknown,
        Legacy,
        BoneRotations,
        Object
    }

    public class BaseRecorder : UdonSharpBehaviour
    {
        private const string VariableDelimiter = ";";

        [SerializeField] [Tooltip("Start recording button, connect to StartRecording event.")]
        private Button startRecordButton;

        [SerializeField] [Tooltip("Stop recording button, connect to StartRecording event.")]
        private Button stopRecordButton;

        [SerializeField] [Tooltip("Start recording immediately on scene load.")]
        protected bool recordOnStart = true;

        [SerializeField] [Tooltip("Frames per second for recording.")]
        protected float recordFramerate = 30;

        protected RecordingType RecordingType = RecordingType.Object;
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
            var typeStr = RecordingTypeToString(RecordingType);
            var outputString = string.Join(VariableDelimiter, "RECORDING", TargetName, _takeNumber, typeStr, timeStr);

            UpdateRecordingObjects();
            foreach (var recObj in RecordingObjects)
            {
                if (recObj == null) continue;

                switch (recObj.GetType().Name)
                {
                    case "Vector3":
                    {
                        var vector3Str = FormatVector3Components((Vector3)recObj);
                        outputString = string.Join(VariableDelimiter, outputString, vector3Str);
                        break;
                    }
                    case "Quaternion":
                    {
                        var quaternionStr = FormatQuaternionComponents((Quaternion)recObj);
                        outputString = string.Join(VariableDelimiter, outputString, quaternionStr);
                        break;
                    }
                    default:
                        outputString = string.Join(VariableDelimiter, recObj.ToString());
                        break;
                }
            }

            HumrLogger.Log(outputString);
        }

        private static string FormatVector3Components(Vector3 vector3)
        {
            var trimmedVector3 = vector3.ToString().Trim('(', ')');
            return trimmedVector3.Replace(" ", "");
        }

        private static string FormatQuaternionComponents(Quaternion quaternion)
        {
            var trimmedQuaternion = quaternion.ToString().Trim('(', ')');
            return trimmedQuaternion.Replace(" ", "");
        }

        private static string RecordingTypeToString(RecordingType recordingType)
        {
            switch (recordingType)
            {
                case RecordingType.Legacy:
                    return "Legacy";
                case RecordingType.BoneRotations:
                    return "BoneRotations";
                case RecordingType.Object:
                    return "Object";
                case RecordingType.Unknown:
                default:
                    return "Unknown";
            }
        }
    }
}