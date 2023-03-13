using HM;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class HMAddressableTest : MonoBehaviour
{
    // Start is called before the first frame update
     void Start()
    {
        this.Capsule();
        this.Sphere();
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

    async void Capsule(bool beSecond=false)
    {
       
        var caspsuleHandle = Addressables.InstantiateAsync("Assets/HMAddressables/Sample/RES/Capsule/Capsule.prefab");
        await caspsuleHandle.Task;
        caspsuleHandle.Result.transform.position = new Vector3(beSecond?-1.5f:0,0,2);
    }

    async void Sphere(bool beSecond=false)
    {
        var caspsuleHandle = Addressables.InstantiateAsync("Assets/HMAddressables/Sample/RES/Sphere/Sphere.prefab");
        await caspsuleHandle.Task;
        caspsuleHandle.Result.transform.position = new Vector3(beSecond?-0.5f:1,0,0);
    }

    private void OnGUI()
    {
        if (GUILayout.Button("更新资源"))
        {
            HMAddressableManager.UpdateAddressablesAllAssets(UpdateCb);
        }
    }
}