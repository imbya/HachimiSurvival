# R01-P01：Unity 工程与空场景

状态：待 review｜执行者：Codex｜依赖：可用 Unity 6.3 LTS｜估时：1–2 小时（不含下载安装）

## 要做什么

1. 阅读[本轮说明](../README.md)。重新检查执行机器的编辑器和许可状态；此前只在常用目录发现 2021.3，不能据此断言其他位置没有 6.3。
2. 使用可用的 Unity 6.3 LTS 补丁版在 `HachimiSurvival/` 创建 URP 2D 工程，记录实际版本与包版本。若没有可用 6.3 或受许可／安装权限阻碍，报告具体缺项，不静默降级到 2021，也不把未启动的工程报完成。
3. 配置键鼠 Input System、uGUI，建立 `Assets/Hachimi/` 下 Scenes、Scripts、Settings 目录；美术和音效接入目录为 `Assets/Art/`、`Assets/Audio/`。只引入本轮实际使用的包。
4. 创建并保存 `R01_Combat.unity`：正交相机、18×10 可走区域与边界、占位玩家、可读中文提示。进入 Play 后场景可见、无编译错误。
5. 写简短 `HachimiSurvival/README.md`：所需版本、打开场景及运行方法。Unity 工程的忽略规则排除 Library、Temp、Logs、Obj、UserSettings 和 Builds；保留 Assets 及 `.meta`、Packages、ProjectSettings。

## 交付检查

- 实际用 Unity 打开工程并进入 Play，记录编辑器版本和结果；关闭后可重新打开保存场景。
- 运行方法不依赖执行者机器上未记录的绝对资源路径；编辑器文件使用正常项目记录即可。
- 不在此任务实现战斗，也不创建远端 GitHub 仓库。工程成功运行后，交付记录提醒制作人询问用户是否建仓并提交。

## 交付记录（执行者填写）

- 文件与实际编辑器版本：
- 启动／检查结果：
- 已知问题或阻碍：
- review 记录（制作人填写）：

## 本次交付记录

- 文件与实际编辑器版本：`HachimiSurvival/Assets/Hachimi/Scenes/R01_Combat.unity`、`Assets/Hachimi/Scripts/`、`Assets/Hachimi/Settings/R01_CombatConfig.asset`；Unity `6000.3.24f1`，Input System `1.20.0`，URP `17.3.0`。
- 启动／检查结果：通过 Unity CLI 创建并保存场景，进入 Play Mode 后生成场地、边界、玩家、对峙猫、HUD；复测无编译错误、无控制台警告或异常。
- 已知问题或阻碍：当前使用程序几何和内置字体占位，尚未接入 A01／S01 资源；完整移动、猫扑和战斗手感仍由 P02／P03 实测。
- review 记录（制作人填写）：待 review。
