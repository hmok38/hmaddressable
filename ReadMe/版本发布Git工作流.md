# 基于 Gitflow 的版本发布工作流


### 1 开发时序图
```mermaid
sequenceDiagram
autonumber
participant DEV as develop
    create participant MAN as feature/11.24_main
    DEV->>MAN: 创建版本主分支
    create participant FA as feature/11.24_A功能
    MAN->>FA: 创建功能A的分支
    create participant FB as feature/11.24_B功能
    MAN->>FB: 创建功能B的分支
    FA->>FA: 开发功能A
    FB->>FB: 开发功能B
    loop develop有更新需要合并
    DEV->>DEV: develop有更新需要合并
    DEV->>MAN: 合并到版本主分支
    loop 版本主分支有更新需要合并
    MAN->>MAN:版本主分支有更新需要合并
    MAN->>FA: 从版本主分支合并到功能A
    MAN->>FB: 从版本主分支合并到功能B
    end
    end
    FB->>FB: 功能B开发完成,交付策划验收
    destroy FB
    FB->>MAN: 策划验收完成后,由主程决定合并回版本主分支的时机
    FA->>FA: 功能A开发完成,交付策划验收
    destroy FA
    FA->>MAN: 策划验收完成后,由主程决定合并回版本主分支的时机
    MAN->>MAN: 交付测试验收
    MAN->>MAN: 各功能bug修复
    destroy MAN
    MAN->>DEV: 测试完毕,冒烟完毕
    DEV->>DEV: 准备发布,走下面的母包发布时序图
    


```


###

### 2 母包发布时序图

```mermaid
sequenceDiagram
    autonumber
    participant DEV as develop
    
    create participant DBG as debug/5.0.0
    DEV->>DBG: 创建测试分支
    DBG->>DBG: 构建测试母包
    DBG->>DEV: 反馈bug
    DEV->>DEV: 修复bug
    DEV->>DBG: 合并到测试分支
    DBG->>DBG: 重新构建测试母包
    Note over DEV,DBG: 测试完毕
    DBG->>DBG: 提交git保留debug/5.0.0分支
    
    create participant REL as release/5.0.0
    DEV->>REL: 从develop创建正式分支
    REL->>REL: 构建正式母包
    REL->>REL: 提交git,保存构建分组、state 和产物

    Note over DEV,REL: 正式上线
    DBG->>DBG: 打tag:5.0.0_debug
    destroy REL
    REL->>DEV: 结束分支,打tag:5.0.0

    loop 热更新流程
    DEV->>DEV: 继续开发
    Note over DEV,REL: 需要热更新
    DEV->>DBG: 摘抄到测试分支(避开release打包产物)
    DBG->>DBG: 测试热更新,提交git保留打包产物,tag:5.0.1_debug
     
    
    create participant HOT as hotfix/5.0.1
    DEV->>HOT: 创建hotfix分支
    Note over DEV,HOT: 热更上线
    destroy HOT
    HOT->>DEV: 结束hotfix分支,打tag:5.0.1
    end

    Note over DEV,HOT: 大版本更新,重新打母包

    destroy DBG
    DBG-xREL: 丢弃,不要合并
    
    create participant DBG2 as debug/5.1.0
    DEV->>DBG2: 创建新的测试分支
    Note over DEV,DBG2: 重走时序图
    
   
```

## 3. 摘取业务提交时的隔离规则

### 3.1 debug 摘取前必须排除什么

**不要直接 cherry-pick 正式版本的构建提交，也不要把 release、master 或 hotfix 整支合并到 debug。** 分支名不会保护文件，Git 也不会自动跳过 state 或资源组。

| 修改内容 | 是否直接摘取到 debug | 正确处理方式 |
|---|---|---|
| 经批准的业务资源、配置表及依赖 | 可以 | 保持与本次正式发布目标相同的业务内容 |
| 业务资源的 `.meta`，包括新增、移动和删除涉及的元数据 | 需要一起处理 | 保留资源 GUID，不能按扩展名一律排除所有 `.meta` |
| 正式 `AssetGroups`、`Schemas`、Settings、DataBuilders、组模板及其 `.meta` | **禁止** | 保留 debug 自己的配置，由 debug 的热更流程在其基础上更新 |
| 正式 `addressables_content_state.bin` 及其 `.meta`，任何目标平台 | **禁止** | 保留 debug 对应平台的原始测试母包 state |
| 业务和正式组配置、state 混在一起的提交 | **禁止整笔摘取** | 在来源处拆成纯业务提交，或在临时分支中整理成可审核的纯业务补丁 |
| 有意改变资源的 Address、Labels | 不复制整份正式组文件 | 记录资源 GUID 和目标 Address、Labels，在 debug 自己的条目中落实相同业务含义 |
| `ConfigHMAddressables` 中新增资源目录 | 按字段审核 | 同步必要的业务目录；保留目标分支的服务器地址和既定打包设置 |
| Provider、加密、preset、打包模式、Unity 或 Addressables 版本 | 不作为普通业务修改摘取 | 按母包兼容性评估；需要改变框架或构建基础时进入新母包流程 |

`Assets/AddressableAssetsData/` 应作为跨环境摘取的默认排除范围。该规则禁止的是**导入另一环境的构建配置**，不禁止 debug 自己的热更工具创建更新组、移动条目并保存配置。

当前 `UpdateAASetting` 会再次读取 HMAA 平台配置并应用 Schema/preset。因此，只有排除 `Assets/AddressableAssetsData/` 还不够；也要审核该目录外的平台配置，尤其是服务器地址、`ForceRemoteAssetsToLocal`、重复依赖组策略和加密 Provider。

### 3.2 让摘取过程容易检查

把“业务修改”和“某环境构建后生成的配置”分成不同提交，例如 `biz: 调整角色资源` 与 `build(debug): 保存 5.0.1 更新组`。正式分支和 debug 可以对应同一个业务来源提交，但分别拥有自己的构建提交。

摘取前先查看每笔提交的文件清单与实际差异。只有确认是纯业务提交后才执行摘取；`cherry-pick` 默认处理整笔提交，`-x` 只记录来源，不会过滤文件。见 [Git cherry-pick 文档](https://git-scm.com/docs/git-cherry-pick)。

以下命令是人工检查示例，`业务提交ID`、`摘取前提交ID` 需要替换成实际值：

```text
git status --short
git show --format=fuller --name-status 业务提交ID
git show 业务提交ID
git cherry-pick -x 业务提交ID
git diff --name-status 摘取前提交ID HEAD
git diff 摘取前提交ID HEAD -- Assets/AddressableAssetsData
```

最后一项应能证明没有导入正式构建数据。业务资源导入后，如果移动回调更新了 debug 自己的 Address，需要区分这些本地同步修改与来源提交携带的正式组替换；不能把两者混为一谈。热更构建完成后的更新组变化也应单独保存和审核。

发生冲突时不使用“一律采用来源分支”的方式处理 Addressables 目录。若已误带入正式数据，先停止构建和发布，再依据摘取前 debug 的记录恢复受影响配置。不要用重新打测试母包掩盖问题，否则原测试客户端与 state 的对应关系会改变。

