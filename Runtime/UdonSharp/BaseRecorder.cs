// BaseRecorder.cs

using System.Globalization;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace HUMR
{
    public enum RecordingType
    {
        Object,
        Player
    }
    
    public class BaseRecorder : UdonSharpBehaviour
    {
        [SerializeField, Tooltip("Start recording immediately on scene load.")]
        protected bool recordOnStart = true;
        [SerializeField, Tooltip("Frames per second for recording.")]
        protected float recordFramerate = 30;
        
        protected float RecordTime;

        private bool _isRecording;
        private float _recordInterval;
        private float _nextRecordTime;
        
        private const string VariableDelimiter = ";";
        private const string ComponentDelimiter = ",";

        public virtual void Start()
        {
            if (recordOnStart) StartRecording();
        }

        private void Update()
        {
            if (!_isRecording) return;
            
            RecordTime += Time.deltaTime;
            if (RecordTime < _nextRecordTime) return;
            _nextRecordTime = RecordTime + _recordInterval;

            OnRecordTick();
        }

        public virtual void StartRecording()
        {
            RecordTime = 0f;
            _nextRecordTime = RecordTime;
            _recordInterval = 1f / recordFramerate;
            _isRecording = true;
        }
        
        public virtual void StopRecording()
        {
            _isRecording = false;
        }

        protected virtual void OnRecordTick() { }

        private void OnDestroy()
        {
            if (_isRecording) StopRecording();
        }

        public override void Interact()
        {
            if (_isRecording) StopRecording();
            else StartRecording();
        }

        protected static void RecordObjectTransform(Transform transform, string name, float time)
        {
            var timeStr = time.ToString(CultureInfo.InvariantCulture);
    
            var positionStr = FormatVector3Components(transform.position);
            var rotationStr = FormatQuaternionComponents(transform.rotation);
            var scaleStr = FormatVector3Components(transform.localScale);

            var outputString = string.Join(VariableDelimiter, name, timeStr, positionStr, rotationStr, scaleStr);
    
            HumrLog(outputString);
        }

        protected static void RecordPlayerBones(VRCPlayerApi player, float time)
        {
            var timeStr = time.ToString(CultureInfo.InvariantCulture);
            
            var hipsPosition = player.GetBonePosition(HumanBodyBones.Hips);
            var hipPositionStr = FormatVector3Components(hipsPosition);
            
            var outputString = string.Join(VariableDelimiter, player.displayName, timeStr, hipPositionStr);
            
            for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var rotation = player.GetBoneRotation((HumanBodyBones)i);
                outputString = string.Join(ComponentDelimiter, outputString, rotation);
            }
            
            HumrLog(outputString);
        }

        private static string FormatVector3Components(Vector3 vector3)
        {
            return string.Join(ComponentDelimiter,
                vector3.x.ToString("F6", CultureInfo.InvariantCulture),
                vector3.y.ToString("F6", CultureInfo.InvariantCulture),
                vector3.z.ToString("F6", CultureInfo.InvariantCulture));
        }

        private static string FormatQuaternionComponents(Quaternion quaternion)
        {
            return string.Join(ComponentDelimiter,
                quaternion.x.ToString("F6", CultureInfo.InvariantCulture),
                quaternion.y.ToString("F6", CultureInfo.InvariantCulture),
                quaternion.z.ToString("F6", CultureInfo.InvariantCulture),
                quaternion.w.ToString("F6", CultureInfo.InvariantCulture));
        }

        protected static void RecordStart(RecordingType recordingType, string recordingName)
        {
            HumrLog(string.Join(VariableDelimiter, "START RECORDING", recordingType.ToString(), recordingName));
        }

        protected static void RecordStop(RecordingType recordingType, string recordingName)
        {
            HumrLog(string.Join(VariableDelimiter, "STOP RECORDING", recordingType.ToString(), recordingName));
        }

        private static void HumrLog(object message)
        {
            Debug.Log($"[HUMR] {message}");
        }
    }
}