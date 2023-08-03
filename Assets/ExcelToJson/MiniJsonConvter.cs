using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using ExcelConfig;
using Newtonsoft.Json;
using UnityEngine;

public class MiniJsonConvter : MonoBehaviour
{
    // Start is called before the first frame update
    async Task Start()
    {
        var text = await HM.HMAddressableManager.LoadAsync<TextAsset>("Assets/Bundles/ConfigMini/Testconfig.json");
        TestConfigCategory.Instance.Init(text.text);
        var map = TestConfigCategory.Instance.GetAll();
        if (map == null)
        {
            Debug.Log("map==null");
        }
        else
        {
            Debug.Log(JsonConvert.SerializeObject(map));
        }
    }

    public class ClassA
    {
        public List<string> a;
    }
}