// BaseRecorder.cs

using System.Globalization;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace HUMR
{
    
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
    
            CSharpUtilities.HumrLog(outputString);
        }

        protected static void RecordPlayerBones(VRCPlayerApi player, float time)
        {
            var timeStr = time.ToString(CultureInfo.InvariantCulture);
            
            var hipsPosition = player.GetBonePosition(HumanBodyBones.Hips);
            var hipsPositionStr = FormatVector3Components(hipsPosition);
            
            var outputString = string.Join(VariableDelimiter, player.displayName, timeStr, hipsPositionStr);
            
            for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var rotation = player.GetBoneRotation((HumanBodyBones)i);
                var rotationStr = FormatQuaternionComponents(rotation);
                outputString = string.Join(VariableDelimiter, outputString, rotationStr);
            }
            
            CSharpUtilities.HumrLog(outputString);
        }

        private static string FormatVector3Components(Vector3 vector3)
        {
            var trimmedVector3 = vector3.ToString().Trim('(',')');
            return trimmedVector3.Replace(" ", "");
        }

        private static string FormatQuaternionComponents(Quaternion quaternion)
        {
            var trimmedQuaternion = quaternion.ToString().Trim('(',')');
            return trimmedQuaternion.Replace(" ", "");
        }

        protected static void RecordStart(RecordingType recType, string recName)
        {
            CSharpUtilities.HumrLog(string.Join(VariableDelimiter, "START RECORDING", CSharpUtilities.RecTypeToString(recType), recName));
        }

        protected static void RecordStop(RecordingType recType, string recName)
        {
            CSharpUtilities.HumrLog(string.Join(VariableDelimiter, "STOP RECORDING", CSharpUtilities.RecTypeToString(recType), recName));
        }
    }
}