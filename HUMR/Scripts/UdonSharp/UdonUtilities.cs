using System.Globalization;
using System.Text;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace HUMR
{
    public class UdonUtilities : UdonSharpBehaviour
    {

        public static void LogPlayerBoneRotations(VRCPlayerApi player, float time)
        {
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

            for (var i = 0; i < HumanTrait.BoneName.Length; i++)
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
        
        public static void HumrLog(object message)
        {
            Debug.Log($"[HUMR] {message}");
        }
    }
}
