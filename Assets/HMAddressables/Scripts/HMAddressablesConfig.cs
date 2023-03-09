using UnityEngine;


[CreateAssetMenu(fileName = "ConfigHMAddressables", menuName = "HMAddressables/创建Config对象")]
public class HMAddressablesConfig : UnityEngine.ScriptableObject
{
    
    public string TestValues = "TestValues";
   
    [Header("要打包的目录")]
    public string[] AseetsPaths=new []{"Assets/Bundles"} ;
}

