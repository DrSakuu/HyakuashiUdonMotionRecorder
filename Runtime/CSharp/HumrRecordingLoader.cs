using UnityEngine;

namespace Humr
{
    [RequireComponent(typeof(Animator))]
    public class HumrRecordingLoader : MonoBehaviour
    {
        public Animator Animator => GetComponent<Animator>();
        public int fileIndex;
        public int targetIndex;
        public bool exportHumanFbx = true;
        public bool exportGenericAnim; 
        public bool showAdvanced;
    }
}