#if UDONSHARP
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace DrSakuu.Humr
{
    public class RecorderUIController : RecorderListener
    {
        [SerializeField] [Tooltip("Start recording button, connect onClick to StartRecording custom event on the recorder.")]
        private Button startRecordButton;

        [SerializeField] [Tooltip("Stop recording button, connect onClick to StopRecording custom event on the recorder.")]
        private Button stopRecordButton;

        [SerializeField] [Tooltip("Renderer that changes material to indicate recording state.")]
        private Renderer indicatorRenderer;

        [SerializeField] [Tooltip("The material to set the indicator when HUMR is recording.")]
        private Material recordingMaterial;
        private Material _indicatorDefaultMaterial;

        protected override void Start()
        {
            base.Start();
            
            if (Utilities.IsValid(indicatorRenderer) && Utilities.IsValid(recordingMaterial))
                _indicatorDefaultMaterial = indicatorRenderer.material;
        }

        public override void AfterRecordingStateChanged(bool isRecording, bool recordIsReady)
        {
            if (Utilities.IsValid(startRecordButton))
                startRecordButton.gameObject.SetActive(!isRecording);

            if (Utilities.IsValid(stopRecordButton))
                stopRecordButton.gameObject.SetActive(isRecording);

            if (Utilities.IsValid(indicatorRenderer) && Utilities.IsValid(recordingMaterial))
                indicatorRenderer.material = isRecording ? recordingMaterial : _indicatorDefaultMaterial;
        }
    }
}
#endif
