#if UDONSHARP
using UnityEditor;
using UdonSharpEditor;

namespace DrSakuu.Humr.Editor
{
    [CustomEditor(typeof(PlayerRecorder))]
    [CanEditMultipleObjects]
    public class PlayerRecorderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(targets)) return;

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "targetName");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
