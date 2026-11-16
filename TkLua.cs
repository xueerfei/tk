// ============================================================================
// TkLua - 沙盘战争三国 (tk.exe) Lua 修改器 -- 最终版
// ============================================================================
// 原理: 游戏启动时 Main.lua 第14行调用 require("LuaPanda").start("127.0.0.1", 8818),
//       即游戏本身是一个 LuaPanda 调试客户端, 会自动连接本机 8818 端口。
// 本工具扮演调试服务端 (VSCode 角色):
//   1. 完成 initSuccess 握手 (必须携带全部 info 字段, 否则客户端 Lua 报错断连);
//   2. 用 stopOnStep 让游戏暂停在下一条 Lua 行上 (只有暂停时调用栈才存在,
//      表达式环境的全局变量解析才可用);
//   3. 用 getWatchedVariable 让游戏以 loadstring 执行任意 Lua 表达式
//      (自动探测 stackId 2~10), 直接读写 SidlRT 游戏数据;
//   4. 用 continue 恢复游戏运行。
// 所有输出镜像到同目录 TkLua.log (窗口被关也能查)。
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class TkLua
{
    // ---- Lua 表达式模板 ----
    // 读取当前策略点数
    const string GET_POINTS_EXPR =
        "(function()" +
        " local PL=(package and package.loaded) or {}" +
        " local GU=PL[\"Gameplay.GameplayUtil\"] or require(\"Gameplay.GameplayUtil\")" +
        " local ctx=GU.GetContext()" +
        " local fi=ctx.Model.Factions[ctx.SelfFactionId]" +
        " return \"当前策略点数 = \" .. tostring(fi.StrategemPoints)" +
        " end)()";

    // 修改策略点数 ({0} = 目标值)
    const string SET_POINTS_EXPR =
        "(function()" +
        " local PL=(package and package.loaded) or {{}}" +
        " local GU=PL[\"Gameplay.GameplayUtil\"] or require(\"Gameplay.GameplayUtil\")" +
        " local ctx=GU.GetContext()" +
        " local fi=ctx.Model.Factions[ctx.SelfFactionId]" +
        " local old=fi.StrategemPoints" +
        " fi.StrategemPoints={0}" +
        " return \"策略点数 OLD=\" .. tostring(old) .. \" -> NEW=\" .. tostring(fi.StrategemPoints)" +
        " end)()";

    // 查看当前资源 (粮食/木材/石头/铁块/马匹/金钱 + 策略点)
    const string GET_RES_EXPR =
        "(function()" +
        " local PL=(package and package.loaded) or {}" +
        " local GU=PL[\"Gameplay.GameplayUtil\"] or require(\"Gameplay.GameplayUtil\")" +
        " local ctx=GU.GetContext()" +
        " local fid=ctx.SelfFactionId" +
        " local fi=ctx.Model.Factions[fid]" +
        " local f=function(mid) return tostring(GU.GetFactionMaterialNum(fid,mid)) end" +
        " local r={}" +
        " r[#r+1]=\"粮食=\"..f(Sidl.TK.MaterialType.Food)" +
        " r[#r+1]=\"木材=\"..f(Sidl.TK.MaterialType.Wood)" +
        " r[#r+1]=\"石头=\"..f(Sidl.TK.MaterialType.Stone)" +
        " r[#r+1]=\"铁块=\"..f(Sidl.TK.MaterialType.Iron)" +
        " r[#r+1]=\"马匹=\"..f(Sidl.TK.MaterialType.Horse)" +
        " r[#r+1]=\"金钱=\"..f(Sidl.TK.MaterialType.Money)" +
        " r[#r+1]=\"策略点=\"..tostring(fi.StrategemPoints)" +
        " return table.concat(r,\"  \")" +
        " end)()";

    // 资源全满: 木材/石头/铁块/马匹/金钱 -> 9999999, 粮食按地点分配
    const string SET_ALL_RES_EXPR =
        "(function()" +
        " local PL=(package and package.loaded) or {}" +
        " local GU=PL[\"Gameplay.GameplayUtil\"] or require(\"Gameplay.GameplayUtil\")" +
        " local ctx=GU.GetContext()" +
        " local fid=ctx.SelfFactionId" +
        " local fi=ctx.Model.Factions[fid]" +
        " local v=9999999" +
        " local r={}" +
        " local oldMoney=fi.Money fi.Money=v" +
        " r[#r+1]=\"金钱 \"..tostring(oldMoney)..\" -> \"..tostring(fi.Money)" +
        " local m={Sidl.TK.MaterialType.Wood,Sidl.TK.MaterialType.Stone,Sidl.TK.MaterialType.Iron,Sidl.TK.MaterialType.Horse}" +
        " for i=1,4 do" +
        "  local mid=m[i]" +
        "  local old=fi.Materials[mid] fi.Materials[mid]=v" +
        "  r[#r+1]=tostring(old)..\" -> \"..tostring(GU.GetFactionMaterialNum(fid,mid))" +
        " end" +
        " local oldFood=GU.GetFactionAllFood(fid)" +
        " local first=true" +
        " for siteId,_ in pairs(fi.Sites) do" +
        "  local si=GU.GetSiteInfo(siteId)" +
        "  if first then si.Food=v first=false else si.Food=0 end" +
        " end" +
        " r[#r+1]=\"粮食 \"..tostring(oldFood)..\" -> \"..tostring(GU.GetFactionAllFood(fid))" +
        " return table.concat(r,\"  \")" +
        " end)()";

    // 修改单个非粮食资源 ({0}=MaterialType 枚举名 Wood/Stone/Iron/Horse, {1}=目标值)
    const string SET_MATERIAL_EXPR =
        "(function()" +
        " local PL=(package and package.loaded) or {{}}" +
        " local GU=PL[\"Gameplay.GameplayUtil\"] or require(\"Gameplay.GameplayUtil\")" +
        " local ctx=GU.GetContext()" +
        " local fid=ctx.SelfFactionId" +
        " local fi=ctx.Model.Factions[fid]" +
        " local v=math.clamp({1},0,99999999)" +
        " local mid=Sidl.TK.MaterialType.{0}" +
        " local old=fi.Materials[mid]" +
        " fi.Materials[mid]=v" +
        " return \"{0} OLD=\" .. tostring(old) .. \" -> NEW=\" .. tostring(GU.GetFactionMaterialNum(fid,mid))" +
        " end)()";

    // 修改粮食 ({0}=目标值): 全部地点清零, 第一个地点设为目标值 (显示值=各地之和)
    const string SET_FOOD_EXPR =
        "(function()" +
        " local PL=(package and package.loaded) or {{}}" +
        " local GU=PL[\"Gameplay.GameplayUtil\"] or require(\"Gameplay.GameplayUtil\")" +
        " local ctx=GU.GetContext()" +
        " local fid=ctx.SelfFactionId" +
        " local fi=ctx.Model.Factions[fid]" +
        " local v=math.clamp({0},0,99999999)" +
        " local old=GU.GetFactionAllFood(fid)" +
        " local first=true" +
        " for siteId,_ in pairs(fi.Sites) do" +
        "  local si=GU.GetSiteInfo(siteId)" +
        "  if first then si.Food=v first=false else si.Food=0 end" +
        " end" +
        " return \"粮食 OLD=\" .. tostring(old) .. \" -> NEW=\" .. tostring(GU.GetFactionAllFood(fid))" +
        " end)()";

    // 修改金钱 ({0}=目标值, 金钱存在 fi.Money 而非 Materials)
    const string SET_MONEY_EXPR =
        "(function()" +
        " local PL=(package and package.loaded) or {{}}" +
        " local GU=PL[\"Gameplay.GameplayUtil\"] or require(\"Gameplay.GameplayUtil\")" +
        " local ctx=GU.GetContext()" +
        " local fi=ctx.Model.Factions[ctx.SelfFactionId]" +
        " local v=math.clamp({0},0,99999999)" +
        " local old=fi.Money fi.Money=v" +
        " return \"金钱 OLD=\" .. tostring(old) .. \" -> NEW=\" .. tostring(fi.Money)" +
        " end)()";

    // initSuccess 握手消息: 客户端处理函数会直接解引用 info 下大量字段
    // (如 TempFilePath:sub、genUnifiedPath(cwd)), 字段不全会导致客户端报错并断开。
    const string INIT_SUCCESS =
        "{\"cmd\":\"initSuccess\",\"callbackId\":\"0\",\"info\":{" +
        "\"stopOnEntry\":\"false\"," +
        "\"isNeedB64EncodeStr\":\"false\"," +
        "\"luaFileExtension\":\".lua\"," +
        "\"TempFilePath\":\"C:/Temp/\"," +
        "\"cwd\":\"C:/\"," +
        "\"logLevel\":\"1\"," +
        "\"autoPathMode\":\"false\"," +
        "\"pathCaseSensitivity\":\"false\"," +
        "\"truncatedOPath\":\"\"," +
        "\"distinguishSameNameFile\":\"true\"," +
        "\"OSType\":\"Windows_NT\"," +
        "\"clibPath\":\"\"," +
        "\"useCHook\":\"false\"," +
        "\"adapterVersion\":\"1.0.0\"" +
        "}}";

    static TcpClient client;
    static StreamReader reader;
    static StreamWriter writer;
    static readonly object ioLock = new object();
    static readonly object queueLock = new object();
    static List<string> queue = new List<string>();
    static int cb = 1;
    static bool b64 = false;
    static string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TkLua.log");
    static readonly object logLock = new object();

    // 控制台 + 日志文件
    static void Log(string s)
    {
        lock (logLock)
        {
            Console.WriteLine(s);
            try { File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss") + " " + s + "\r\n", Encoding.UTF8); }
            catch { }
        }
    }

    // 仅写日志文件 (原始协议报文等噪音信息)
    static void LogF(string s)
    {
        lock (logLock)
        {
            try { File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss") + " " + s + "\r\n", Encoding.UTF8); }
            catch { }
        }
    }

    static void Pause()
    {
        Log("");
        Log("===== 程序即将退出, 按任意键关闭窗口 =====");
        try { Console.ReadKey(); } catch { }
    }

    static string JsonString(string s)
    {
        StringBuilder sb = new StringBuilder();
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    static void Send(string json)
    {
        lock (ioLock)
        {
            writer.Write(json + "|*|\n");
            writer.Flush();
        }
    }

    static void ReaderLoop()
    {
        try
        {
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) { LogF("[收] 连接被游戏关闭"); break; }
                if (line.EndsWith("|*|")) line = line.Substring(0, line.Length - 3);
                if (line.Length == 0) continue;
                LogF("[收] " + line);
                lock (queueLock) queue.Add(line);
            }
        }
        catch (Exception ex) { LogF("[收] 读取异常: " + ex.Message); }
    }

    // 等待符合条件的一条客户端消息; 其他消息留在队列里
    static string WaitFor(Func<string, bool> pred, int timeoutMs)
    {
        DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < deadline)
        {
            lock (queueLock)
            {
                for (int i = 0; i < queue.Count; i++)
                {
                    string m = queue[i];
                    if (pred(m)) { queue.RemoveAt(i); return m; }
                }
            }
            Thread.Sleep(100);
        }
        return null;
    }

    static void DumpPending()
    {
        lock (queueLock)
        {
            foreach (string m in queue) LogF("[遗留消息] " + m);
            queue.Clear();
        }
    }

    static string Extract(string json, string key, char sep)
    {
        int i = json.IndexOf(key);
        if (i < 0) return null;
        i = json.IndexOf(sep, i);
        if (i < 0) return null;
        i++;
        while (i < json.Length && json[i] == ' ') i++;
        int end = json.IndexOf(',', i);
        if (end < 0) end = json.IndexOf('}', i);
        if (end < 0) end = json.Length;
        return json.Substring(i, end - i).Trim();
    }

    static string Unquote(string s)
    {
        if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            s = s.Substring(1, s.Length - 2);
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                i++;
                switch (s[i])
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    default: sb.Append(s[i]); break;
                }
            }
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }

    // 完整执行循环: 暂停游戏(stopOnStep) -> 执行表达式(探测stackId) -> 继续(continue)
    // 返回是否执行成功
    static bool StopAndEval(string expr, string label)
    {
        DumpPending();
        string sid = (cb++).ToString();
        Send("{\"cmd\":\"stopOnStep\",\"callbackId\":\"" + sid + "\",\"info\":{}}");
        Log("");
        Log(">> [" + label + "] 已请求暂停游戏 (游戏画面会短暂卡住, 正常现象)");
        Log(">> 若游戏迟迟不停, 请在游戏里点几下界面 (开关面板/点地图) 让Lua跑起来");
        DateTime deadline = DateTime.Now.AddMinutes(10);
        string note = null;
        while (DateTime.Now < deadline)
        {
            note = WaitFor(m => m.IndexOf("\"cmd\":\"stopOnStep\"") >= 0 && m.IndexOf("\"stack\"") >= 0, 10000);
            if (note != null) break;
            Log(">> 等待游戏暂停中... (在游戏里操作几下即可)");
        }
        if (note == null)
        {
            Log("!! 10分钟内游戏没有暂停, 本次执行取消 (游戏保持运行)");
            return false;
        }
        Log(">> 游戏已暂停, 开始执行表达式...");

        // 游戏暂停时调用栈才建立; stackId=2 是最内层用户帧, 依次探测 2~10
        for (int st = 2; st <= 10; st++)
        {
            string id = (cb++).ToString();
            string json = "{\"cmd\":\"getWatchedVariable\",\"callbackId\":\"" + id + "\"," +
                          "\"info\":{\"stackId\":\"" + st + "\",\"varName\":\"" + JsonString(expr) + "\"}}";
            Send(json);
            string reply = WaitFor(m => m.IndexOf("\"callbackId\":\"" + id + "\"") >= 0, 20000);
            if (reply == null)
            {
                LogF("[表达式无响应] " + label);
                continue;
            }
            LogF("[表达式原始响应] " + reply);
            string isSuc = Extract(reply, "\"isSuccess\"", ':');
            string val = Extract(reply, "\"value\"", ':');
            if (val != null)
            {
                if (val.StartsWith("\"")) val = Unquote(val);
                if (b64)
                {
                    try { val = Encoding.UTF8.GetString(Convert.FromBase64String(val)); }
                    catch { }
                }
            }
            if (isSuc == "true" || isSuc == "\"true\"")
            {
                string cid = (cb++).ToString();
                Send("{\"cmd\":\"continue\",\"callbackId\":\"" + cid + "\",\"info\":{}}");
                Log("");
                Log("================ 执行成功 ================");
                Log("  " + (val ?? "(无返回值)"));
                Log("==========================================");
                Log(">> 游戏已恢复运行");
                return true;
            }
            LogF("[stackId=" + st + " 失败] " + (val ?? "(空)"));
        }
        // 任何情况下都不能让游戏停在暂停态
        string c2 = (cb++).ToString();
        Send("{\"cmd\":\"continue\",\"callbackId\":\"" + c2 + "\",\"info\":{}}");
        Log("!! 执行失败 (所有 stackId 均未成功), 游戏已恢复运行; 详情见 TkLua.log");
        return false;
    }

    // 握手: 成功返回游戏配置行, 失败返回 null
    static string Handshake()
    {
        NetworkStream ns = client.GetStream();
        reader = new StreamReader(ns, Encoding.UTF8);
        writer = new StreamWriter(ns, new UTF8Encoding(false));
        Thread rt = new Thread(ReaderLoop); rt.IsBackground = true; rt.Start();

        Send(INIT_SUCCESS);
        Log("[连接] 已发送握手消息, 等待游戏确认...");
        string cfg = null;
        for (int attempt = 0; attempt < 8 && cfg == null; attempt++)
        {
            cfg = WaitFor(m => m.IndexOf("\"cmd\":\"initSuccess\"") >= 0, 10000);
            if (cfg == null)
            {
                Log("[连接] 第 " + (attempt + 1) + " 次等待超时, 重发握手... (请在游戏里操作几下)");
                try { Send(INIT_SUCCESS); } catch { break; }
            }
        }
        if (cfg == null) return null;

        LogF("[连接] 游戏配置: " + cfg);
        if (cfg.IndexOf("\"UseLoadstring\":\"1\"") >= 0)
            Log("[连接] 握手成功, loadstring 可用 -> 可以执行任意 Lua 表达式!");
        else
            Log("[连接] 握手成功, 但 loadstring 不可用 (该版本无法用此方案)");
        if (cfg.IndexOf("\"isNeedB64EncodeStr\":\"true\"") >= 0) b64 = true;
        return cfg;
    }

    // 命令菜单 (会话内循环)
    static void Session()
    {
        Thread.Sleep(1500);
        Log("");
        Log("----------------------------------------------------");
        Log(" 使用说明:");
        Log("  1. 去游戏里加载存档, 打开一次策略面板 (让游戏准备好数据)");
        Log("  2. 回到本窗口, 选择下方命令并按回车");
        Log("  3. 执行时游戏会短暂卡住, 属于正常现象, 执行完自动恢复");
        Log("----------------------------------------------------");
        while (true)
        {
            Console.WriteLine("");
            Console.WriteLine("====== 命令菜单 ======");
            Console.WriteLine(" 1. 策略点数 -> 9999");
            Console.WriteLine(" 2. 资源全满 (粮食/木材/石头/铁块/马匹/金钱 -> 9999999)");
            Console.WriteLine(" 3. 查看当前资源 (含策略点)");
            Console.WriteLine(" 4. 修改单个资源");
            Console.WriteLine(" 5. 策略点数 -> 自定义数值");
            Console.WriteLine(" 6. 自定义 Lua 表达式");
            Console.WriteLine(" 0. 退出");
            Console.Write("输入序号: ");
            string cmd = Console.ReadLine();
            if (cmd == null) break;
            cmd = cmd.Trim();
            if (cmd == "0" || cmd == "exit" || cmd == "quit") break;
            try
            {
                if (cmd == "1")
                {
                    StopAndEval(string.Format(SET_POINTS_EXPR, "9999"), "策略点数=9999");
                }
                else if (cmd == "2")
                {
                    StopAndEval(SET_ALL_RES_EXPR, "资源全满");
                }
                else if (cmd == "3")
                {
                    StopAndEval(GET_RES_EXPR, "查看资源");
                }
                else if (cmd == "4")
                {
                    Console.WriteLine(" 1=粮食 2=木材 3=石头 4=铁块 5=马匹 6=金钱");
                    Console.Write("输入 \"资源序号 数值\" (如: 2 50000): ");
                    string line = Console.ReadLine();
                    string[] parts = (line ?? "").Trim().Split(' ');
                    int ri; long n;
                    if (parts.Length >= 2 && int.TryParse(parts[0], out ri) && long.TryParse(parts[1], out n) && ri >= 1 && ri <= 6)
                    {
                        string[] enumNames = { "Food", "Wood", "Stone", "Iron", "Horse", "Money" };
                        string[] labels = { "粮食", "木材", "石头", "铁块", "马匹", "金钱" };
                        string label = labels[ri - 1];
                        if (ri == 1)
                            StopAndEval(string.Format(SET_FOOD_EXPR, n), label + "=" + n);
                        else if (ri == 6)
                            StopAndEval(string.Format(SET_MONEY_EXPR, n), label + "=" + n);
                        else
                            StopAndEval(string.Format(SET_MATERIAL_EXPR, enumNames[ri - 1], n), label + "=" + n);
                    }
                    else
                        Log("!! 格式不对, 应为: 资源序号 数值  (如: 2 50000)");
                }
                else if (cmd == "5")
                {
                    Console.Write("输入目标策略点数: ");
                    string v = Console.ReadLine();
                    long n;
                    if (v != null && long.TryParse(v.Trim(), out n))
                        StopAndEval(string.Format(SET_POINTS_EXPR, n.ToString()), "策略点数=" + n);
                    else
                        Log("!! 输入的不是数字");
                }
                else if (cmd == "6")
                {
                    Console.WriteLine("输入要执行的 Lua 表达式 (一行, 回车执行):");
                    string expr = Console.ReadLine();
                    if (expr != null && expr.Trim().Length > 0)
                        StopAndEval(expr, "自定义表达式");
                }
                else
                {
                    Log("!! 无效选项");
                }
            }
            catch (Exception ex)
            {
                Log("!! 执行出错 (连接可能已断开): " + ex.Message);
                break;
            }
        }
        try { client.Close(); } catch { }
    }

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "TkLua - 沙盘战争三国 Lua 修改器 (日志: TkLua.log)";
        Log("================================================");
        Log(" TkLua - 沙盘战争三国 Lua 修改器");
        Log(" 日志文件: " + logPath);
        Log("================================================");
        try
        {
            Run();
        }
        catch (Exception ex)
        {
            Log("[错误] " + ex);
        }
        Pause();
    }

    static void Run()
    {
        Log("");
        Log("[连接] 正在监听 127.0.0.1:8818, 等待游戏连接...");
        Log("[连接] 提示: 游戏启动时会自动连接; 若已启动, 重新启动游戏即可");

        // 接受循环: 每轮新建监听(会话断开后旧监听器已停, 必须重建);
        // 游戏断开后会自行重连, 一直接受直到一次握手成功
        while (true)
        {
            TcpListener l = new TcpListener(IPAddress.Loopback, 8818);
            try
            {
                l.Start();
            }
            catch (Exception ex)
            {
                Log("[错误] 监听 8818 失败: " + ex.Message);
                return;
            }
            try
            {
                client = l.AcceptTcpClient();
            }
            catch (Exception ex)
            {
                Log("[连接] 等待连接中断: " + ex.Message);
                return;
            }
            l.Stop();
            Log("[连接] 游戏已连接: " + client.Client.RemoteEndPoint);
            lock (queueLock) queue.Clear();
            string cfg = null;
            try
            {
                cfg = Handshake();
            }
            catch (Exception ex)
            {
                Log("[连接] 握手异常: " + ex.Message);
            }
            if (cfg != null)
            {
                Session();
            }
            try { client.Close(); } catch { }
            Log("[连接] 会话结束, 继续等待游戏重新连接...");
        }
    }
}
