using UnityEngine;

namespace Humr
{
    [RequireComponent(typeof(Animator))]
    public class HumrRecordingLoader : MonoBehaviour
    {
        public Animator Animator => GetComponent<Animator>();
    }
}