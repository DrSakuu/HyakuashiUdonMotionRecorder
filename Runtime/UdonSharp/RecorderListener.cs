#if UDONSHARP
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace DrSakuu.Humr
{
    public class RecorderListener : UdonSharpBehaviour
    {
        [SerializeField] [Tooltip("Recorder to listen to.")]
        private BaseRecorder recorder;
        
        protected virtual void Start()
        {
            if (!Utilities.IsValid(recorder))
            {
                HumrLogger.Error($"{name} is missing a Recorder to listen to!");
                return;
            }
        
            recorder.AddListener(this);
        }

        public virtual void AfterRecordingStateChanged(bool isRecording, bool recordIsReady)
        {
        }
    }
}
#endif