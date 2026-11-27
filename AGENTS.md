# HMAddressable 项目入口

<!-- MyAICoding-project:begin -->
处理本项目 Unity 任务前，必须完整读取并遵循 [MyAiCoding 项目协作规则](docs/MyAiCodingProject.md)；当前聊天已完整读取且文件未变时无需重复加载。
<!-- MyAICoding-project:end -->

## 工程入口

- 仓库根也是 Unity 工程根。[ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) 记录的编辑器版本为 2022.3.62f2。工程使用 Addressables 1.22.3 和 UniTask，依赖见 [Packages/manifest.json](Packages/manifest.json)。
- [HMAddressableManager](Assets/HMAddressable/Runtime/HMAddressableManager.cs) 属于 `HMAddressable.Runtime` 程序集，提供资源加载、释放及热更接口。编辑器的母包和热更菜单在 [HMAddressablesEditor](Assets/HMAddressable/Editor/HMAddressablesEditor.cs)，属于 `HMAddressable.Editor` 程序集。
- [HMAddressableTest](Assets/Samples/Test/Scripts/HMAddressableTest.cs) 展示通过 `OnGUI`、`LoadAsync` 和 `Instantiate` 加载与创建示例对象。验收场景见[示例使用说明](ReadMe/示例使用说明.md)，发包流程见[版本发布 Git 工作流](ReadMe/版本发布Git工作流.md)。
