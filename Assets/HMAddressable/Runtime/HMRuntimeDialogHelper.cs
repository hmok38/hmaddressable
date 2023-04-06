using System.Diagnostics;
using UnityEngine;
using UnityEngine.Events;
using Debug = UnityEngine.Debug;

namespace HM
{
    public class HMRuntimeDialogHelper : MonoBehaviour
    {
        private static System.Diagnostics.Stopwatch _stopwatch = new Stopwatch();
        /// <summary>
        /// 关闭日志的总开关,通过这个工具发出的日志都受控制
        /// </summary>
        public static bool BeNeedDebug=true;
        /// <summary>
        /// 设置日志输出回调,如果设置了,那么就不在使用debug输出,而采用这个事件输出
        /// </summary>
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