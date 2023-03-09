using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class HMAddressableTest : MonoBehaviour
{
    // Start is called before the first frame update
    async void Start()
    {
        this.Capsule();
        this.Sphere();
    }

    async void Capsule()
    {
        var caspsuleHandle = Addressables.InstantiateAsync("Assets/HMAddressables/Sample/RES/Capsule/Capsule.prefab");
        await caspsuleHandle.Task;
        caspsuleHandle.Result.transform.position = new Vector3();
    }

    async void Sphere()
    {
        var caspsuleHandle = Addressables.InstantiateAsync("Assets/HMAddressables/Sample/RES/Sphere/Sphere.prefab");
        await caspsuleHandle.Task;
        caspsuleHandle.Result.transform.position = new Vector3(1,0,0);
    }

    // Update is called once per frame
    void Update()
    {
    }
}