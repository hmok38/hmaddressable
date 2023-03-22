using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class EditoToolsTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        var sub = new Sub();
        sub.Test();

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

public class Parrent
{
    private static string staticString;
    private string xxx1;
    public string xxx;
    private string xxx2 { get; set; }
    public string xxx3 { get; set; }

    private string Returnx(string x)
    {
        Debug.Log(this.xxx1);
        Debug.Log(staticString);
        return x;
    }

}

public class Sub:Parrent
{
    public void Test()
    {
      //var x=  MyEditorTools.CallPrivateMethodWithReturn<string>(this.GetType().BaseType,this, "Returnx","传值");
      //Debug.Log(x);
    }
    
}
