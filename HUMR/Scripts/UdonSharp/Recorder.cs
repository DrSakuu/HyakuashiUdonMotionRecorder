
/*******
 * Recorder.cs
 * 
 * UdonSharp用。VRCSDK3-WorldとUdonSharpを導入すること
 * 
 * プレイヤーの座標とボーンの回転値をログに出力している
 * 
 * *****/

using System.Globalization;
using System.Text;
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
        private float _startTime;
        private float _recordInterval;
        private float _nextRecordTime;

        private void Start()
        {
            _localPlayer = Networking.LocalPlayer;
            _startTime = Time.time;
            _recordInterval = 1f / recordFramerate;
            _players = new VRCPlayerApi[10];
            RefreshPlayerList();
        }

        private void Update()
        {
            var time = Time.time - _startTime;
            if (time < _nextRecordTime) return;

            _nextRecordTime = Time.time + _recordInterval;

            if (recordAllPlayers)
            {
                foreach (var player in _players)
                {
                    if (!Utilities.IsValid(player)) continue;
                    LogPlayer(player, time);
                }
            }
            else
            {
                LogPlayer(_localPlayer, time);
            }
            
            var playerCount = VRCPlayerApi.GetPlayerCount();
            for (var i = 0; i < playerCount; i++)
            {
                if (!recordAllPlayers && i > 0) break;
                
                var player = _players[i];
                if (!Utilities.IsValid(player)) continue;

                LogPlayer(player, time);
            }
        }

        private static void LogPlayer(VRCPlayerApi player, float time)
        {
            var builder = new StringBuilder(4096);

            builder.Append("HUMR:");
            builder.Append(player.displayName);
            builder.Append(';');
            builder.Append(time.ToString(CultureInfo.InvariantCulture));

            var hipsPosition = player.GetBonePosition(HumanBodyBones.Hips);

            builder.Append(';');
            builder.Append(hipsPosition.x.ToString("F7", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(hipsPosition.y.ToString("F7", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(hipsPosition.z.ToString("F7", CultureInfo.InvariantCulture));

            for (var i = 0; i < HumanTrait.BoneName.Length; i++)
            {
                var rotation = player.GetBoneRotation((HumanBodyBones)i);

                builder.Append(';');
                builder.Append(rotation.x.ToString("F7", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(rotation.y.ToString("F7", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(rotation.z.ToString("F7", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(rotation.w.ToString("F7", CultureInfo.InvariantCulture));
            }

            Debug.Log(builder.ToString());
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