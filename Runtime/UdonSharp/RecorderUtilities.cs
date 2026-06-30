using System.Globalization;
using System.Text;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace HUMR
{
    public enum RecordingType
    {
        Object,
        Humanoid
    }
    
    public class RecorderUtilities : UdonSharpBehaviour
    {
        private const string VariableDelimiter = ";";
        private const string ComponentDelimiter = ",";

        public static void RecordObjectTransform(Transform transform, string name, float time)
        {
            var timeStr = time.ToString(CultureInfo.InvariantCulture);
    
            var positionStr = FormatVector3Components(transform.position);
            var rotationStr = FormatQuaternionComponents(transform.rotation);
            var scaleStr = FormatVector3Components(transform.localScale);

            var outputString = string.Join(VariableDelimiter, name, timeStr, positionStr, rotationStr, scaleStr);
    
            HumrLog(outputString);
        }

        public static void RecordPlayerBoneRotations(VRCPlayerApi player, float time)
        {
            // TODO: Extend LogObjectTransform
            var builder = new StringBuilder(4096);
            const string partDelimiter = ";";
            const string itemDelimiter = ",";
            var displayName = player.displayName; 
            
            builder.Append(displayName);
            builder.Append(partDelimiter);
            builder.Append(time.ToString(CultureInfo.InvariantCulture));
            
            var hipsPosition = player.GetBonePosition(HumanBodyBones.Hips);
            
            builder.Append(partDelimiter);
            builder.Append(hipsPosition.x.ToString("F7", CultureInfo.InvariantCulture));
            builder.Append(itemDelimiter);
            builder.Append(hipsPosition.y.ToString("F7", CultureInfo.InvariantCulture));
            builder.Append(itemDelimiter);
            builder.Append(hipsPosition.z.ToString("F7", CultureInfo.InvariantCulture));
            
            for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var rotation = player.GetBoneRotation((HumanBodyBones)i);
            
                builder.Append(partDelimiter);
                builder.Append(rotation.x.ToString("F7", CultureInfo.InvariantCulture));
                builder.Append(itemDelimiter);
                builder.Append(rotation.y.ToString("F7", CultureInfo.InvariantCulture));
                builder.Append(itemDelimiter);
                builder.Append(rotation.z.ToString("F7", CultureInfo.InvariantCulture));
                builder.Append(itemDelimiter);
                builder.Append(rotation.w.ToString("F7", CultureInfo.InvariantCulture));
            }
            
            HumrLog(builder.ToString());
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
        
        public static void StartRecording(RecordingType recordingType, string recordingName)
        {
            HumrLog(string.Join(VariableDelimiter, "STOP RECORDING", recordingType, recordingName));
        }
        
        public static void StopRecording(RecordingType recordingType, string recordingName)
        {
            HumrLog(string.Join(VariableDelimiter, "START RECORDING", recordingType, recordingName));
        }
        
        public static void HumrLog(object message)
        {
            Debug.Log($"[HUMR] {message}");
        }
    }
}
