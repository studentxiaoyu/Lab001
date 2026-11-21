# Fusion 双机化身测试

这次接入同步头部和双手姿态，复用已有化身及双臂 IK。实验器材、参数、记录表和抓取控制权尚未联网，不作为本次通过条件。

## 两台电脑准备

1. GitHub Desktop 选择 Lab001 的 main 分支，Fetch origin 后 Pull origin，确认两台电脑使用同一提交。等待 Unity 导入和编译完成，Console 不应出现编译错误。不需要重新安装 SDK 或添加 Building Block。
2. 使用 Unity 6000.3.27f1，打开 `Assets/Scenes/Main.unity`。网络预制体在 `Assets/Resources/LaboratoryNetworkAvatar.prefab`，已带 FusionPrefab 标签。若报告预制体未注册，先停止 Play，执行 Tools → Fusion → Rebuild Prefab Table。
3. 两端 Tools → Fusion → Realtime Settings 使用同一个 Fusion App ID、App Version 和区域。若自动选区不同，可将 Fixed Region 指定为双方都可连接的同一区域。不要把 App ID 发到截图或报告里。
4. Main 的 `[BuildingBlock] Auto Matchmaking` 保持 Automatic、Shared，房间名保持 `MultiplayerSession`，两端一致。Peer Mode 使用 Single；不要在同一编辑器内追加多个 Runner。这里的 Single 是“一进程一个 Runner”，并非单人房间。
5. 真实 VR 测试时，两台电脑分别用 USB 连接自己的 Quest 3，进入 Quest Link，并停用 Meta XR Simulator。

## 先测连接和远端显示

1. 两端进入 Play。电脑 Game 窗口左下角显示房间、区域、玩家数及可见远端化身数。房间和区域应一致，Players 应为 2。
2. 如果没有头显，两端分别点击 Game 窗口并按 F3，开启明确标记为 SIMULATED 的网络姿态。对方应能看到程序生成的摆臂化身，Remote avatars 应为 1；远端姿态是模拟数据时也会显示提示。这只检查网络和显示，不是头显验收。F2 仍只是本地预览，不会自动广播其姿态。
3. 真实头显接入时不允许用 F3 替代追踪。保持佩戴、输入焦点和追踪有效，远端化身才显示。
4. 实机玩家初始虚拟位置可能重合。先用原有左摇杆让一人向侧面移动约 1–2 米，再回头观察；也可在 Scene 视图检查 `Remote Avatar ...`。不要为分开化身而盲目在现实中走动。本版不做现实空间共定位。

## 双人实机验收

双方轮流转头、抬左手、抬右手、转身和用摇杆移动，确认看到的是另一位参与者的动作。腿部维持原型站姿，不是腿部追踪。

打开头显系统菜单或取下头显后，检查对方的身体是否隐藏；单手失追踪时，该手应回到放松姿态，另一手继续跟随。网络姿态停止更新超过约 2 秒也会隐藏身体。关闭一端后，另一端对应化身应移除；重新加入应重新生成且不重复。

## 出问题时记录

- Players 不是 2：先检查房间、区域、App ID／版本和连接错误。`Client A` 对象存在不能证明已入房。连接超时是网络问题，化身代码不会消除服务不可达。
- Players 是 2，Remote avatars 是 0：无头显时确认双方都开启 F3；有头显时确认双方佩戴、焦点及追踪有效。查看 Console 中的 `[LabNetwork] Avatar ready`、`Spawned` 或异常。
- 化身看不见但 Remote avatars 是 1：检查是否重合、是否在视野外；不要把 F2 本地预览误当成对方。
- 收集两端 Game 状态栏、Console 首个错误和动作视频。分别写桌面模拟／实机测试结果。

## 已有检查与未验证项

启动修复后，请两端先停止 Play，再拉取同一版本，重新进入 Play；不要在旧的异常运行状态下继续测试。Game 新增 Avatar 状态行：只有 `Avatar spawned` 才表示化身初始化完成；`Connected; waiting ...` 表示正在等待玩家／场景准备；生成失败会要求 STOP Play，不再自动反复尝试。

如需先单独检查一端，可在编辑模式通过 Tools → Lab → Check Shared Avatar Startup (Play) 启动有时间上限的回归检查。成功条件是完成 Shared 启动、只生成一个本地网络化身，并在随后 4 秒内无相关生成异常；测试会自动退出 Play。输出在 Temp/LaboratoryNetworkStartupCheck.result.txt，不加入 Git。该检查不等于双机互相可见测试。

2026-11-21 启动顺序修复已通过上述单端 Main Play 检查，未复现先前 GetNextId 空引用和 already owned 重复注册。后续仍需双机和真实动作对照。

2026-11-21：C# 编译、Fusion IL Weaver、网络预制体导入，以及编辑器中的远端模型创建、失追踪隐藏、无重复相机／网络会话检查已通过。尚未完成双进程入同房、跨电脑同步、Quest 3 双机动作对照、持续运行与退出重连实测。
