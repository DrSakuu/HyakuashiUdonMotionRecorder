/*******
 * Recorder.cs
 * 
 * UdonSharp用。VRCSDK3-WorldとUdonSharpを導入すること
 * 
 * プレイヤーの座標とボーンの回転値をログに出力している
 * 
 * *****/

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;


namespace HUMR
{
    public class Recorder : UdonSharpBehaviour
    {
        [SerializeField, Tooltip("チェックを入れるとワールド内の全ての人のモーションが記録されます（周知を推奨）")]
        private bool recordAllPlayers;

        [SerializeField, Tooltip("記録毎に最低何秒間の間隔を空けるかの設定")] 
        private float recordFramerate = 30f;

        private VRCPlayerApi[] _players;
        private VRCPlayerApi _localPlayer;
        private float _recordTime;
        private float _recordInterval;
        private float _nextRecordTime;

        private void Start()
        {
            _localPlayer = Networking.LocalPlayer;
            _recordTime = 0f;
            _nextRecordTime = _recordTime;
            _recordInterval = 1f / recordFramerate;
            _players = new VRCPlayerApi[10];
            RefreshPlayerList();
        }

        private void Update()
        {
            _recordTime += Time.deltaTime;
            if (_recordTime < _nextRecordTime) return;
            _nextRecordTime = _recordTime + _recordInterval;

            if (recordAllPlayers)
            {
                foreach (var player in _players)
                {
                    if (!Utilities.IsValid(player)) continue;
                    UdonUtilities.LogPlayerBoneRotations(player, _recordTime);
                }
            }
            else
            {
                UdonUtilities.LogPlayerBoneRotations(_localPlayer, _recordTime);
            }
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            RefreshPlayerList();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            RefreshPlayerList();
        }

        private void RefreshPlayerList()
        {
            EnsurePlayerCapacity(VRCPlayerApi.GetPlayerCount());
            VRCPlayerApi.GetPlayers(_players);
        }

        private void EnsurePlayerCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= _players.Length) return;

            var newCapacity = _players.Length;
            while (newCapacity < requiredCapacity)
            {
                newCapacity *= 2;
            }

            _players = new VRCPlayerApi[newCapacity];
        }
    }
}