using System.Diagnostics;
using UnityEngine;
using UnityEngine.Events;
using Debug = UnityEngine.Debug;

namespace HM
{
    public class HMRuntimeDialogHelper : MonoBehaviour
    {
        private static System.Diagnostics.Stopwatch _stopwatch = new Stopwatch();
        public static bool BeNeedDebug=true;
        public static UnityAction<string> LogAction;

       
        public static void DebugStopWatchInfo(string message)
        {
            if(!BeNeedDebug)return;
            string str = $"计时器:{message} \n当前时间{_stopwatch.ElapsedMilliseconds}ms ";
            if (LogAction == null)
            {
                Debug.Log(str);
            }
            else
            {
                LogAction.Invoke(str);
            }
             
        }

        public static void StartStopwatch()
        {
            _stopwatch.Start();
        }
        
        public static void RestartStopwatch()
        {
            _stopwatch.Restart();
        }

        
        public static void StopStopwatch()
        {
            _stopwatch.Stop();
        }
    }
}