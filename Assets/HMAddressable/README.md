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

资源移动或重命名后，如果条目的 Address 与移动前的完整资源路径相同，插件会在 Unity 导入回调中将它更新为新路径；移动整个目录也会同步其中资源的默认地址。其他自定义 Address 保持原样，目录标签在下次整理分组时更新。调用方使用的路径字符串仍需同步修改。

启用此修复之前已经发生的移动，以及 Unity 未报告移动事件的外部文件操作，无法可靠区分旧路径地址与自定义地址；这些历史条目需要在 Addressables Groups 中人工核对 Address，插件不会猜测并覆盖。

---

**基于长期 debug 分支的热更测试：**

1. 新母包周期从 `develop` 创建 `debug/5.0.0`，使用“一键打出包资源(测试包)”并打出测试游戏包。修复在 `develop` 完成，再将业务修复同步到 `debug/5.0.0`。测试完毕后提交最终构建数据，长期保留该分支。
2. 从 `develop` 创建 `release/5.0.0`，使用“一键打出包资源(正式包)”并打出正式游戏包，保存各平台最终的分组、state 和构建产物。正式上线后打标签 `5.0.0`，测试母包对应标签为 `5.0.0_debug`，按团队 Gitflow 结束 release 分支并合回主线。
3. 后续热更只把 `develop` 中本次需要的业务修改摘取到 `debug/5.0.0`，遵守下面的隔离规则。保留 debug 自己的原测试母包 state 和累计更新组，保持对应目标平台及测试 Catalog 地址，使用“一键打更新资源包(测试包)”。发布到测试服务器后，使用原测试母包客户端检查热更；测试完毕后保存本轮最终构建数据，打标签 `5.0.1_debug`。
4. 直接从当时的 `develop` 创建 `hotfix/5.0.1`。该提交必须包含正式母包 state、累计正式更新组，以及 debug 已验证的全部业务内容，不能夹带未经测试的新内容。使用“一键打更新资源包(正式包)”，保存本轮最终构建数据，正式发布后打标签 `5.0.1`，按团队 Gitflow 结束 hotfix 分支并合回主线。
5. 后续热更重复上述流程，继续使用 `debug/5.0.0`，递增热更版本号，例如 `hotfix/5.0.2`、`5.0.2_debug` 和 `5.0.2`。每轮只提交最终构建数据，持续保留对应平台的原母包 state；热更阶段不要重新构建母包或重新分析重复依赖。
6. 需要重发母包时，丢弃旧 `debug/5.0.0`，不要合并它。从 `develop` 创建 `debug/5.1.0`，开始新的母包周期。

**摘取到 debug 时，必须排除正式版本的资源分组、Schemas、Settings、DataBuilders、组模板、各平台 state 及这些文件的 `.meta`。** 默认排除 `Assets/AddressableAssetsData/`；不要直接摘取正式构建提交，也不要把 `release`、`master` 或 `hotfix` 整支合并到 debug。业务资源自身的 `.meta` 要随业务修改保留，以维持资源 GUID。有意修改 Address、Labels 时，在 debug 自己的条目中落实，不复制正式组文件。

还要审核 Addressables 目录外的平台配置，保留测试服务器地址及既定的 preset、Provider、`ForceRemoteAssetsToLocal` 等构建设置。各环境、各平台保存自己的构建数据与产物，不能相互覆盖。完整规则见仓库文档 `ReadMe/版本发布Git工作流.md`。

---

**升级接口和其他接口参考:**

`UpdateAddressablesAllAssets` 的回调有四个参数：状态、进度、消息、`UpdateStatusCode`。状态为 `None` 时表示进行中，`Succeeded` 或 `Failed` 才是本次最终结果。

- `await` 正常返回只表示调用结束，不代表更新成功。等待结束后，必须检查本次最终回调状态，成功才继续加载业务资源；传入 `null` 回调将无法取得结果。
- 已有更新尚未结束时，重复调用会抛出 `InvalidOperationException`，且不会通知第二次调用的回调。示例中的实例标记防止本按钮重复触发，其他调用方仍可能占用更新流程，因此还需要 `try/catch/finally`。
- 回调应只记录状态或更新 UI，不要在最终回调中重新调用更新接口。失败后等待当前调用结束，再由用户点击按钮重试。
- 插件隔离的是同步回调直接抛出的异常；不要把回调写成 `async void`，其中的异常不由插件处理。

下面是保留现有 OnGUI 调试按钮形式的最小示例：

```csharp
using System;
using HM;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AddressablesUpdateExample : MonoBehaviour
{
    private bool updating;
    private string updateMessage = "点击按钮更新资源";

    private void OnGUI()
    {
        GUILayout.Label(updateMessage);
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && !updating;
        if (GUILayout.Button("更新资源 / 失败后重试"))
        {
            UpdateResources();
        }
        GUI.enabled = wasEnabled;
    }

    private async void UpdateResources()
    {
        if (updating) return;
        updating = true;
        var finalStatus = AsyncOperationStatus.None;
        try
        {
            await HMAddressableManager.UpdateAddressablesAllAssets((status, progress, message, code) =>
            {
                finalStatus = status;
                updateMessage = $"{code}：{progress:P0} {message}";
            });
            if (finalStatus != AsyncOperationStatus.Succeeded)
            {
                updateMessage = $"更新未成功，请点击按钮重试。{updateMessage}";
                return;
            }

            updateMessage = "更新成功，可以继续加载业务资源。";
            // 只有在这里继续初始化或加载业务资源。
            Debug.Log(updateMessage);
        }
        catch (Exception exception)
        {
            updateMessage = $"更新请求未完成，请稍后点击按钮重试：{exception.Message}";
            Debug.LogException(exception);
        }
        finally
        {
            updating = false;
        }
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
