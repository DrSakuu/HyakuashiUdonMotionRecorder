using UnityEngine;

namespace DrSakuu.Humr
{
#if VRC_SDK_VRCSDK3
    using VRC.SDKBase;
    [RequireComponent(typeof(Animator))]
    public class HumrRecordingLoader : MonoBehaviour, IEditorOnly
#else
    [RequireComponent(typeof(Animator))]
    public class HumrRecordingLoader : MonoBehaviour
#endif
    {
        public string logPath;
        public int fileIndex;
        public int targetIndex;
        public bool exportFbx = true;
        public bool exportAnim;
        public bool showAdvanced;
        public bool blenderHipFix = true;
        public Animator Animator => GetComponent<Animator>();
    }
}