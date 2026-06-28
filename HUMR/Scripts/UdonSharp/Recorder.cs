
/*******
 * Recorder.cs
 * 
 * UdonSharp用。VRCSDK3-WorldとUdonSharpを導入すること
 * 
 * プレイヤーの座標とボーンの回転値をログに出力している
 * 
 * *****/

using System.Globalization;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
namespace HUMR
{
    public class Recorder : UdonSharpBehaviour
    {
        [SerializeField,Tooltip("チェックを入れるとワールド内の全ての人のモーションが記録されます（周知を推奨）")]
        private bool recordAllPlayers = false;
        [Tooltip("記録毎に最低何秒間の間隔を空けるかの設定")]
        public float secondsPerRecord = 0.1f;

        private Quaternion[] _boneRotations;
        private VRCPlayerApi[] _players;
        private VRCPlayerApi _player;
        private float _time;
        private float _beforeTime;

        private void Start()
        {
            _players = new VRCPlayerApi[80];
            _players[0] = Networking.LocalPlayer;
            _boneRotations = new Quaternion[HumanTrait.BoneName.Length];
            _time = 0;
            _beforeTime = _time;
        }
        private void Update()
        {
            if (_time - _beforeTime > secondsPerRecord)
            {
                if (recordAllPlayers)
                {
                    VRCPlayerApi.GetPlayers(_players);
                }

                foreach (var player in _players)
                {
                    if (player == null)
                    {
                        continue;
                    }
                    _player = player;

                    var strOutputLog = "HUMR:";
                    strOutputLog += _player.displayName;
                    strOutputLog += _time.ToString(CultureInfo.InvariantCulture);
                    strOutputLog += ",";
                    //hipbone = root
                    strOutputLog += _player.GetBonePosition(HumanBodyBones.Hips).x.ToString("F7");
                    strOutputLog += ",";
                    strOutputLog += _player.GetBonePosition(HumanBodyBones.Hips).y.ToString("F7");
                    strOutputLog += ",";
                    strOutputLog += _player.GetBonePosition(HumanBodyBones.Hips).z.ToString("F7");
                    for (var j = 0; j < _boneRotations.Length; j++)
                    {
                        _boneRotations[j] = _player.GetBoneRotation((HumanBodyBones)j);
                        strOutputLog += ",";
                        strOutputLog += _boneRotations[j].x.ToString("F7");
                        strOutputLog += ",";
                        strOutputLog += _boneRotations[j].y.ToString("F7");
                        strOutputLog += ",";
                        strOutputLog += _boneRotations[j].z.ToString("F7");
                        strOutputLog += ",";
                        strOutputLog += _boneRotations[j].w.ToString("F7");
                    }
                    Debug.Log(strOutputLog);
                    _beforeTime = _time;
                }
            }
            _time += Time.deltaTime;
        }
    }
}
