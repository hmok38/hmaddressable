using HM;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class HMAddressableTest : MonoBehaviour
{
    public string capsulePath = "Assets/HMAddressables/Sample/RES/Capsule/Capsule.prefab";
    public string spherePath = "Assets/HMAddressables/Sample/RES/Sphere/Sphere.prefab";
     void Start()
    {
        this.Capsule();
        this.Sphere();
    }

   

    async void Capsule(bool beSecond=false)
    {
       
        var caspsuleHandle = Addressables.InstantiateAsync(this.capsulePath);
        await caspsuleHandle.Task;
        caspsuleHandle.Result.transform.position = new Vector3(beSecond?-1.5f:0,0,2);
    }

    async void Sphere(bool beSecond=false)
    {
        var caspsuleHandle = Addressables.InstantiateAsync(this.spherePath);
        await caspsuleHandle.Task;
        caspsuleHandle.Result.transform.position = new Vector3(beSecond?-0.5f:1,0,0);
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

    private void OnGUI()
    {
        if (GUILayout.Button("更新资源"))
        {
            //使用此接口进行统一的资源更新
            HMAddressableManager.UpdateAddressablesAllAssets(UpdateCb);
            //使用此类接口进行实例化及加载和释放
            HMAddressableManager.InstantiateGameObject(this.spherePath);
        }
    }
}