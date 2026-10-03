#if UDONSHARP
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

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

        [SerializeField] [Tooltip("Record position relative to world origin, otherwise local position relative to parent.")]
        protected bool worldAbsolutePosition;

        protected object[] RecordingObjects;
        protected TargetType TargetType = TargetType.Object;
        protected bool IsRecording;
        protected bool RecordIsReady = true;

        private RecorderListener[] _listeners = new RecorderListener[0];
        private float _nextRecordTime;
        private VRCPickup _pickup;
        private float _recordInterval;
        private float _recordTime;
        private long _takeTimestamp;

        public virtual void Start()
        {
            _pickup = GetComponent<VRCPickup>();
            if (_pickup != null) _pickup.UseText = "Record";

            if (recordOnStart) StartRecording();
            else SendRecordingState();
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

            if (_recordInterval == 0)
            {
                RecordObjects();
                return;
            }

            if (_recordTime >= _nextRecordTime)
            {
                RecordObjects();
            }

            while (_recordTime >= _nextRecordTime)
            {
                _nextRecordTime += _recordInterval;
            }
        }

        private void OnDestroy()
        {
            if (IsRecording) StopRecording();
        }

        public virtual void StartRecording()
        {
            if (!RecordIsReady) return;

            _recordTime = 0f;
            _recordInterval = float.IsInfinity(recordFramerate)
                ? 0f
                : (recordFramerate <= 0 ? Mathf.Infinity : 1f / recordFramerate);
            _nextRecordTime = _recordInterval;
            _takeTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            IsRecording = true;
            RecordObjects();
            SendRecordingState();
        }

        public virtual void StopRecording()
        {
            if (RecordIsReady) RecordObjects();
            IsRecording = false;
            SendRecordingState();
        }

        public RecorderListener[] EventListeners
        {
            get => _listeners;
            set => _listeners = value;
        }

        public void AddListener(RecorderListener listener)
        {
            if (!Utilities.IsValid(listener) || Array.IndexOf(_listeners, listener) >= 0) return;
            
            _listeners = _listeners.Add(listener);
        }

        private void SendRecordingState()
        {
            var len = _listeners.Length;
            for (var i = 0; i < len; i++)
            {
                var listener = _listeners[i];
                if (Utilities.IsValid(listener))
                    listener.AfterRecordingStateChanged(IsRecording, RecordIsReady);
            }
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