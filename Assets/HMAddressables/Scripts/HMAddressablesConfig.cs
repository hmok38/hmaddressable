using UnityEngine;


[CreateAssetMenu(fileName = "ConfigHMAddressables", menuName = "HMAddressables/创建Config对象")]
public class HMAddressablesConfig : UnityEngine.ScriptableObject
{
  
    [Header("要打包的目录")]
    public string[] AseetsPaths=new []{"Assets/Bundles"} ;
    [Header("正式资源服务器分发地址")]
    public string RemoteLoadPath = "http://[PrivateIpAddress]/[BuildTarget]";
    [Header("测试资源服务器分发地址")]
    public string TestRemoteLoadPath = "http://[PrivateIpAddress]/Test/[BuildTarget]";
}

