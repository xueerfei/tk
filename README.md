# 沙盘战争三国 · 修改器 (SandTable War Three Kingdoms Trainer)

《沙盘战争三国》(SandTable War Three Kingdoms, Steam 单机游戏) 的玩家修改器。

通过游戏内置的 **LuaPanda 调试通道**注入 Lua 代码，可修改策略点、金钱、粮食、木材、石头、铁块、马匹等数值，也支持执行任意自定义 Lua 表达式。

> 本工具仅供个人单机学习与研究使用。请勿用于联机作弊或任何商业用途。

## 功能

| 快捷键 | 功能 |
| --- | --- |
| F1 | 策略点数一键 9999 |
| F2 | 资源全满（金钱/粮食/木材/石头/铁块/马匹 = 9999999） |
| F3 | 查询当前资源（右侧面板实时显示） |
| F4 | 修改单个资源（0 ~ 99999999） |
| F5 | 策略点数自定义数值 |
| F6 | 自定义 Lua 表达式（高级玩法） |

界面为深蓝科幻「指挥中枢」风格：左侧导航（总览/策略点/资源/高级 Lua）、中央功能区、右侧数据面板、底部运行日志，全部操作也可通过 F1~F6 全局快捷键完成。

## 使用方法

1. **先启动修改器**：双击 `TkLuaGUI.exe`（或命令行版 `TkLua.exe`）
2. **再启动游戏**：游戏启动时会自动连接本机 `127.0.0.1:8818`
3. 等待右上角状态灯变绿「就绪」
4. 在游戏里加载存档并打开一次任意面板，然后按 F1~F6 或点界面按钮

## 重要提示

- **启动顺序必须正确**：先开修改器，再开游戏
- **加载存档后**调试通道会失效，如需继续使用请**重启游戏**（修改器保持开启即可）
- 修改后打开对应面板即可看到新数值
- 执行时游戏画面会短暂卡住（调试暂停），属正常现象；若游戏迟迟不停，在游戏里点几下界面让 Lua 跑起来
- GUI 版与控制台版**不能同时运行**（都监听 8818 端口）

## 原理

游戏 (Unity IL2CPP + xLua) 启动时 `Main.lua` 会调用 `require("LuaPanda").start("127.0.0.1", 8818)`，即游戏本身是一个 LuaPanda **调试客户端**，会不断尝试连接本机 8818 端口。本工具充当**调试服务端**接入：

1. 握手：发送完整的 `initSuccess` 消息（缺字段会导致游戏内 Lua 报错断连）
2. 发送 `stopOnStep` 暂停游戏 Lua（此时才可解析全局变量）
3. 通过 `getWatchedVariable` 执行表达式（被包装为 `(function() ... end)()`，并探测 stackId 2~10）
4. 发送 `continue` 恢复游戏

数据路径（从游戏 Lua 源码验证）：

- 策略点：`fi.StrategemPoints`（`fi` 为 `ctx.Model.Factions[SelfFactionId]`）
- 金钱：`fi.Money`
- 木材/石头/铁块/马匹：`fi.Materials[Sidl.TK.MaterialType.Wood/Stone/Iron/Horse]`
- 粮食：各据点 `siteInfo.Food` 之和

完整的协议细节、踩坑记录见 [最终方案.md](最终方案.md)。

## 文件说明

| 文件 | 说明 |
| --- | --- |
| `TkLuaGui.cs` / `TkLuaGUI.exe` | GUI 版修改器（推荐日常使用） |
| `TkLua.cs` / `TkLua.exe` | 命令行版修改器（GUI 不可用时的备选） |
| `最终方案.md` | 完整技术文档（协议、数据路径、踩坑记录） |
| `LICENSE` | MIT 开源许可证 |
| `TkLuaGUI.log` / `TkLua.log` | 运行日志（含协议原始报文，工具自动生成） |
| `ser_batch/743623110/` | 游戏 Lua 源码提取（开发参考，**建议勿提交到公开仓库**） |

## 编译

需要 .NET Framework 4.x 的 `csc.exe`（Windows 自带）：

```
csc /target:winexe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:TkLuaGUI.exe TkLuaGui.cs
csc /out:TkLua.exe TkLua.cs
```

## 免责声明

本工具仅供个人单机学习与存档研究使用。请尊重游戏开发者，请勿用于联机/多人模式作弊。使用本工具产生的一切后果由使用者自行承担。

## 许可证

本项目采用 [MIT License](LICENSE) 开源，可自由使用、修改和分发。
