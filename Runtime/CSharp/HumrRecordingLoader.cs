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
        public int fileIndex;
        public int targetIndex;
        public Animator Animator => GetComponent<Animator>();
    }
}