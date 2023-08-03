using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class ExcelToMiniJson : MonoBehaviour
{
    [MenuItem("Excel导出/选择配置表")]
    static void ShowConfig()
    {
        Selection.activeObject = ExcelToMiniJsonScripteObj.Instance;
        EditorGUIUtility.PingObject(Selection.activeObject);
        EditorUtility.FocusProjectWindow();
        Debug.Log("已经选择并显示配置表");
    }

    [MenuItem("Excel导出/导出")]
    static async void ExcelToMiniJsonExcute()
    {
        DirectoryInfo dir = new DirectoryInfo(Application.dataPath).Parent;
        string batPath = Path.Combine(dir.FullName, ExcelToMiniJsonScripteObj.Instance.batPath);
        DirectoryInfo batRootPath = new DirectoryInfo(batPath);
        string excelPath = Path.Combine(dir.FullName, ExcelToMiniJsonScripteObj.Instance.excelPath);
        string jsonPath = Path.Combine(dir.FullName, ExcelToMiniJsonScripteObj.Instance.jsonOutPath);
        string csCodePath = Path.Combine(dir.FullName, ExcelToMiniJsonScripteObj.Instance.csCodeOutPath);
        string pyPath =
            @"D:\MyTestCode\HMAddressables\HMAddressablesProject\Tools\UnityTools\Excel2Json\GameConfig\excel_to_json\tools\py37\py37.exe ";

        string pyCodePath =
            @"D:\MyTestCode\HMAddressables\HMAddressablesProject\Tools\UnityTools\Excel2Json\GameConfig\excel_to_json\__export_unity_mini.py";
        string arg =
            $"{pyPath} {pyCodePath} '{excelPath}' '{jsonPath}' '{csCodePath}'";

        // Debug.Log(arg);
        // Process process = new Process();
        // ProcessStartInfo startInfo = new ProcessStartInfo();
        // startInfo.FileName = @"cmd.exe";
        // startInfo.Arguments = arg; // list directory contents
        // startInfo.WindowStyle = ProcessWindowStyle.Maximized; 
        // //startInfo.RedirectStandardOutput = true; // redirect output
        // //startInfo.UseShellExecute = false;
        // process.StartInfo = startInfo;
        // process.Start();


        string rootCmd = dir.Root.Name.Split(':')[0] + ":";
        string cdPathCmd =
            $"cd {batRootPath.Parent.FullName}";
        string cmd = $"tools\\py37\\py37.exe __export_unity_mini.py {excelPath} {jsonPath} {csCodePath}";
        string pauseCmd = "pause";

        string[] cmds = new[]
        {
            // "chcp 65001",
            rootCmd,
            cdPathCmd,
            cmd
            //pauseCmd
        };
        Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(cmds));
        UnityEditor.EditorUtility.DisplayProgressBar("导出Excel", "准备导出", 0.1f);

        System.Diagnostics.Process p = new System.Diagnostics.Process();
        p.StartInfo.FileName = "cmd.exe";
        p.StartInfo.StandardOutputEncoding = Encoding.GetEncoding("GBK");
        p.StartInfo.UseShellExecute = false; //是否使用操作系统shell启动
        p.StartInfo.RedirectStandardInput = true; //接受来自调用程序的输入信息
        p.StartInfo.RedirectStandardOutput = true; //由调用程序获取输出信息
        p.StartInfo.RedirectStandardError = true; //重定向标准错误输出
        p.StartInfo.CreateNoWindow = true; //不显示程序窗口
        p.Start(); //启动程序

        for (int i = 0; i < cmds.Length; i++)
        {
            UnityEditor.EditorUtility.DisplayProgressBar("导出Excel", "正在导出",
                0.1f + (0.8f - 0.1f) * ((float) i / cmds.Length));
            p.StandardInput.WriteLine(cmds[i]);
        }

        p.StandardInput.WriteLine("exit");
        p.StandardInput.AutoFlush = true;

        p.WaitForExit(); //等待程序执行完退出进程
       
        string output = p.StandardOutput.ReadToEnd();
        p.Close();
        var index = output.IndexOf("准备导出到MiniJson格式");
        output= output.Substring(index);
        UnityEditor.EditorUtility.DisplayProgressBar("导出Excel", "导出完毕,正在刷新资源", 0.9f);
        Debug.Log(output);
        AssetDatabase.Refresh();
        UnityEditor.EditorUtility.FocusProjectWindow();
        UnityEditor.EditorUtility.DisplayProgressBar("导出Excel", "导出完毕", 1f);
        UnityEditor.EditorUtility.ClearProgressBar();
        UnityEditor.EditorUtility.DisplayDialog("导出完毕", output, "知道了");
    }
}