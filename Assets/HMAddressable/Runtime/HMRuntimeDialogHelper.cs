using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HM
{
    public class HMRuntimeDialogHelper : MonoBehaviour
    {
        private static System.Diagnostics.Stopwatch _stopwatch = new Stopwatch();
        public static void DebugStopWatchInfo(string message)
        {
          Debug.Log($"计时器:{message} \n{_stopwatch.ElapsedMilliseconds} ");
        }

        public static void StartStopwatch()
        {
            _stopwatch.Start();
        }
        
        public static void StopStopwatch()
        {
            _stopwatch.Stop();
        }
    }
}
