using UnityEngine;

namespace DrSakuu.Humr
{
    [RequireComponent(typeof(Animator))]
    public class HumrRecordingLoader : MonoBehaviour
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