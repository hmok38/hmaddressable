HMAddresables 资源管理插件
===

by:黄敏
* HMAddresable资源管理插件是基于 **Unity Addressables Assets** 系统的自动化打包管理工具
* 资源分组和打包基于文件夹目录进行分组,并在发布游戏包体时一次性打包进入APK包,
* 后续热更时采用增量更新的方式进行热更新
* 同时能对资源进行加密避免解包(1.2.0以上)
* 插件具有高度自动化和热更新体量小的特点,使用它完全不用关心太多资源包知识和原理,只要管理好资源目录,并将打包好的资源放入设定的服务器目录下即可
* 运行态时不会自动更新资源,可以在游戏初始化时手动调用 HMAddressableManager.UpdateAddressablesAllAssets()进行更新

---

**先手动添加依赖 UniTask 插件:**

项目依赖于UniTask插件,它是当前Unity中最好的Await/Async实现异步和等待的插件,可以完美无GC的替代Unity的协程,
且可以在非Mono脚本中使用,因为自定义包中不支持git包到git包的依赖,所以需要手动添加:
1. 打开Unity的**PackageManager**中点+号;
2. 选择**Add package from git URL**
3. 填入:https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.3.3
4. 等待安装完毕,因为git访问原因,可能会添加失败,请多试2次,实在不行就去拉下代码导入项目
5. git地址:https://github.com/Cysharp/UniTask

###注意:依赖的UnityAddressabls包在  PackageManager 中的In Project选项中是找不到的,如果需要对它升级,请到Unity Registry中寻找

---

**添加本包:**

1. 在Unity的**PackageManager**中点+号,选择**Add package from git URL**
2. 输入:https://github.com/hmok38/hmaddressable.git?path=Assets/HMAddressable
3. 点击add按钮,等待安装完毕.因为网络的原因,可以会添加失败,请多试两次,实在不行就去git拉下代码导入项目
4. git地址:https://github.com/hmok38/hmaddressable

**请帮忙点star哦**


---

**开始使用:**

1. 配置好目录下的 **ConfigHMAddressables** 配置表,要打包的目录按照'Assets/xxx'的方式填入AssetsPaths列表,并按照填入的域名准备好资源服务器
2. 选择unity上方菜单的HMAA资源管理的 一键打出包资源
3. 到工程目录下的ServerData目录获取刚刚打出的相应平台的资源,并将其放入资源服务器中
4. 发布游戏包
5. 待需要热更资源时,选择unity上方菜单的HMAA资源管理的 一键打更新资源包
6. 到工程目录下的ServerData目录获取刚刚打出的相应平台的资源,并将其放入资源服务器中
7. 游戏启动后,根据逻辑调用 HMAddressableManager.UpdateAddressablesAllAssets() 接口进行热更即可更新资源

---

**不影响线上的热更测试功能:**
1. 需要进行热更前,请将工程使用git回退到上一次发布的版本;
2. 使用一键打出包资源(测试包) 选项打出测试资源,并打出游戏包;
3. 将资源发布到测试用的资源服务器,并运行游戏包检查是否正常,此时已经准备好了跟线上游戏相同的游戏,只是资源地址不同
4. git还原修改,但不要还原数据文件(如:Assets/AddressableAssetsData/[发布平台]/addressables_content_state.bin),然后再切换到最新的版本
5. 使用一键打更新资源包(测试包) 打出测试用的热更包,然后发布到测试用的资源服务器,再次运行测试游戏包,即可在不影响线上产品的同时检查热更是否成功
6. 还原全部Git

---

**升级接口和其他接口参考:**

```c#

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


```

---

**依赖说明**
1. 依赖 Addressables的版本号在1.20.5以上
2. 依赖 newtonsoft.Json包(Unity2021后内置,2021前版本请在PackageManage的UnityRegistry中搜索)
3. 依赖 UniTask异步插件 请在PackageManage中点+号,选择git url
   输入: https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask 有时候会加载失败,多试几次
   实在不行就去 https://github.com/Cysharp/UniTask.git 下载unityPackage包

---


**更新记录**
* <4.2.1版本>:修复当更新资源时,因为修改内置shader导致BuildinShader组内容发生变化引起运行时更新失败的问题(打资源包时会提示错误)
* <4.2.0版本>:修改安卓平台发布谷歌商店超过150M采用PAD分发的机制,缩短首次启动时间
* <4.1.1版本>:远程组和本地组路径设置可以互相包含了,最终的远程/本地设置会按照资源目录一级一级向上在2个列表中查找,按照最先找到的列表的类型设置远程/本地设置,相同的话远程组优先
* <4.1.0版本>:加密与不加密组的设置可以在配置表中进行预先设置,不再/也不能额外在组设置中修改加密设置
* <4.0.0版本>:安卓支持谷歌 Play Asset Delivery(GPAD)分包分发,大于150M的包体可以发布为.aab或者导出安卓工程,会将设置为远程组的资源组也打入一份到本地包中,随同主包一同发布.
