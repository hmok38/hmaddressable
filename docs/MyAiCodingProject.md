# MyAiCoding 项目协作规则

本文件由项目 `AGENTS.md` 引用，保存本项目采用的协作要求和技术基准入口。工程事实、局部例外与团队补充留在项目入口或其引用的既有文档中，不写进本分发文件。

## 必读依据

涉及 Unity 设计、实现、修复、重构或评审时，先完整读取：

- [Unity C# 代码规范](CodeStyleGuide.md)
- [Unity 工程结构规范](CodeStructureGuide.md)
- 工程根及目标目录适用的 `.editorconfig`、`.gitattributes` 和模块补充。
- 当前工具用户级入口指向的 `standards/Unity系统设计与编码规范.md`，按其处理规则关系、Skill 路由与执行权限。

## Skill 使用要求

本项目必须按用户级《Unity系统设计与编码规范》第 2 节“Skill 路由”判断以下三个 Skill 的适用条件；适用时完整读取并遵循对应的 `SKILL.md`：

- `unity-collaboration/SKILL.md`
- `coding-habits/SKILL.md`
- `unity-system-design/SKILL.md`

具体条件与例外统一以该节为准，不要求每次任务同时加载三个 Skill。从当前工具已发现的技能及用户级入口定位，不只依赖描述匹配自动触发。各 Skill 的规则只在其正文维护，本文件不复述。

必需规范或 Skill 缺失时，先查已知安装位置与用户指定的 MyAiCoding 源仓库；仍缺失则报告具体文件，继续可独立完成的只读调查，不开始依赖它的业务实现。补装按已有部署授权处理，不能把个人 Skill 复制到项目来代替全局安装。

已在当前聊天完整读取且文件未变时复用已读内容。个人偏好正文留在用户配置中。
