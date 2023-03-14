using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HM;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class HMAddressableTest : MonoBehaviour
{
     string capsulePath = "Assets/Samples/Test/RES/Capsule/Capsule.prefab";
     string spherePath = "Assets/Samples/Test/RES/Sphere/Sphere.prefab";
     private List<GameObject> allObjs = new List<GameObject>();
     void Start()
    {
       
    }

     async void Wait()
     {
         await UniTask.Delay(TimeSpan.FromSeconds(2), DelayType.UnscaledDeltaTime);
         HMAddressableManager.ReleaseRes(capsulePath);
         // await UniTask.Delay(TimeSpan.FromSeconds(10), DelayType.UnscaledDeltaTime);
         // this.Capsule();
         // this.Capsule();
         // this.Capsule();
         // this.Capsule();
         // this.Capsule();
     }

   

    async void Capsule(bool beSecond=false)
    {
       

        var prefabUniTask = HMAddressableManager.LoadAsync<GameObject>(capsulePath);
       var prefab= await prefabUniTask;
      var gameObj =Instantiate(prefab);
      gameObj.transform.position = new Vector3(beSecond?-1.5f:0,0,2);
      allObjs.Add(gameObj);
    }

   

    async void Sphere(bool beSecond=false)
    {
        var prefab = HMAddressableManager.Load<GameObject>(spherePath);
       
        var gameObj =Instantiate(prefab);
        gameObj.transform.position = new Vector3(beSecond?-0.5f:1,0,0);
        allObjs.Add(gameObj);
    }
    
    
    private void UpdateCb(AsyncOperationStatus status, float progeress, string message)
    {
        switch (status)
        {
            case  AsyncOperationStatus.Failed:
                Debug.LogError(message);
                break;
            case  AsyncOperationStatus.Succeeded:
                Debug.Log($"更新成功{message}");
                this.Capsule(true);
                this.Sphere(true);
                break;
            case AsyncOperationStatus.None:
                Debug.Log($"正在下载,进度={progeress}");
                break;
        }
    }

    private GameObject ob;
    private async void OnGUI()
    {
        if (GUILayout.Button("更新资源"))
        {
            //使用此接口进行统一的资源更新
            HMAddressableManager.UpdateAddressablesAllAssets(UpdateCb);
           
        }
       
        if (GUILayout.Button("载入资源"))
        {
           HMAddressableManager.LoadAsync<GameObject>(capsulePath);
          
        }
      
        if (GUILayout.Button("创建对象"))
        {
            this.Capsule();

        }
        if (GUILayout.Button("删除所有对象"))
        {
            for (int i = 0; i < this.allObjs.Count; i++)
            {
                Destroy(this.allObjs[i]);
            }
            this.allObjs.Clear();

        }
        if (GUILayout.Button("卸载资源"))
        {
          
            HMAddressableManager.ReleaseRes(capsulePath);

        }
    }
}