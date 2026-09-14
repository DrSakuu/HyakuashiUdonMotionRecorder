using UnityEngine;

namespace DrSakuu.Humr
{
#if VRC_SDK_VRCSDK3
    using VRC.SDKBase;
    public class HumrRecordingLoader : MonoBehaviour, IEditorOnly
#else
    public class HumrRecordingLoader : MonoBehaviour
#endif
    {
        public string logPath;
        public int fileIndex;
        public int targetIndex;
        public bool showAdvanced;
        public bool blenderHipFix = true;
        public Animator Animator => GetComponent<Animator>();
    }
}