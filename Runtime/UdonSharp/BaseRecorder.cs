#if UDONSHARP
using System;
using System.Globalization;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;

namespace DrSakuu.Humr
{
    public class BaseRecorder : UdonSharpBehaviour
    {
        [SerializeField] [Tooltip("Target name for recording.")]
        protected string targetName = "Target";

        [SerializeField] [Tooltip("Frames per second for recording.")]
        protected float recordFramerate = 30;

        [SerializeField] [Tooltip("Start recording immediately on scene load.")]
        protected bool recordOnStart = true;

        [SerializeField] [Tooltip("Start recording button, connect onClick to StartRecording custom event.")]
        private Button startRecordButton;

        [SerializeField] [Tooltip("Stop recording button, connect onClick to StartRecording custom event.")]
        private Button stopRecordButton;

        [SerializeField] [Tooltip("Target mesh to that changes material to indicate recording state.")]
        private Renderer indicatorRenderer;

        [SerializeField] [Tooltip("The material to set the indicator when HUMR is recording.")]
        private Material recordingMaterial;

        protected object[] RecordingObjects;
        protected TargetType TargetType = TargetType.Object;
        protected bool IsRecording;
        protected bool RecordIsReady = true;

        private Material _indicatorDefaultMaterial;
        private float _nextRecordTime;
        private VRCPickup _pickup;
        private float _recordInterval;
        private float _recordTime;
        private long _takeTimestamp;

        public virtual void Start()
        {
            if (recordOnStart) StartRecording();

            _pickup = GetComponent<VRCPickup>();
            if (_pickup != null) _pickup.UseText = "Record";

            if (indicatorRenderer != null && recordingMaterial != null)
                _indicatorDefaultMaterial = indicatorRenderer.material;
        }

        private void Update()
        {
            if (!IsRecording) return;
            
            if (!RecordIsReady)
            {
                StopRecording();
                return;
            }

            _recordTime += Time.deltaTime;
            if (_recordTime < _nextRecordTime) return;
            _nextRecordTime = _recordTime + _recordInterval;

            OnRecordTick();
        }

        private void OnDestroy()
        {
            if (IsRecording) StopRecording();
        }

        public virtual void StartRecording()
        {
            if (!RecordIsReady) return;
            
            _recordTime = 0f;
            _nextRecordTime = _recordTime;
            _recordInterval = recordFramerate <= 0 ? Mathf.Infinity : 1f / recordFramerate;
            _takeTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            IsRecording = true;
            RecordObjects();
            UpdateUI();
        }

        public virtual void StopRecording()
        {
            RecordObjects();
            IsRecording = false;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (startRecordButton != null) startRecordButton.gameObject.SetActive(!IsRecording);
            if (stopRecordButton != null) stopRecordButton.gameObject.SetActive(IsRecording);
            if (indicatorRenderer != null && recordingMaterial != null)
                indicatorRenderer.material = IsRecording ? recordingMaterial : _indicatorDefaultMaterial;
        }

        private void OnRecordTick()
        {
            RecordObjects();
        }

        protected virtual void UpdateRecordingObjects()
        {
        }

        public override void Interact()
        {
            if (_pickup != null) return;

            ToggleRecording();
        }

        public override void OnPickupUseDown()
        {
            ToggleRecording();
        }

        private void ToggleRecording()
        {
            if (IsRecording) StopRecording();
            else StartRecording();
        }

        private void RecordObjects()
        {
            var outputString = HumrLogger.InitializeFrame(TargetType, targetName, _takeTimestamp, _recordTime);
            UpdateRecordingObjects();
            foreach (var recObj in RecordingObjects) outputString = HumrLogger.AppendObject(outputString, recObj);
            HumrLogger.Log(outputString);
        }
    }
}
#endif