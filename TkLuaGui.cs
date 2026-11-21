// TkLuaGui.cs - 沙盘战争三国 修改器 (科幻指令台 GUI v2.3)
// 深蓝底 + 青色霓虹发光 + 圆角玻璃面板 + 角标 + 左侧导航 + 右侧数据面板 + F1~F6 圆形快捷键。
// 协议逻辑与 TkLua.cs 完全一致。编译:
// csc /target:winexe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:TkLuaGUI.exe TkLuaGui.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

// ---------- 配色与字体 ----------
static class UI
{
    public static readonly Color BG     = Color.FromArgb(5, 10, 20);
    public static readonly Color PANEL1 = Color.FromArgb(10, 21, 38);
    public static readonly Color PANEL2 = Color.FromArgb(6, 13, 26);
    public static readonly Color INBG   = Color.FromArgb(12, 28, 46);
    public static readonly Color CYAN   = Color.FromArgb(0, 229, 255);
    public static readonly Color CYAN_D = Color.FromArgb(43, 128, 165);
    public static readonly Color TXT    = Color.FromArgb(198, 220, 242);
    public static readonly Color WHITE  = Color.FromArgb(234, 244, 252);
    public static readonly Color DIM    = Color.FromArgb(92, 114, 150);
    public static readonly Color RED    = Color.FromArgb(255, 82, 82);
    public static readonly Color AMBER  = Color.FromArgb(255, 177, 77);
    public static readonly Color GREEN  = Color.FromArgb(62, 224, 138);

    public static readonly Font F8  = new Font("Microsoft YaHei UI", 8.5f);
    public static readonly Font F9  = new Font("Microsoft YaHei UI", 9f);
    public static readonly Font F10 = new Font("Microsoft YaHei UI", 10f);
    public static readonly Font FB  = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
    public static readonly Font F15 = new Font("Microsoft YaHei UI", 15.5f, FontStyle.Bold);
    public static readonly Font FCON= new Font("Consolas", 9.5f);
    public static readonly Font FCONB= new Font("Consolas", 11f, FontStyle.Bold);

    public static GraphicsPath RRect(Rectangle r, int rad)
    {
        GraphicsPath p = new GraphicsPath();
        int d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

// ---------- 圆角发光面板 ----------
class RPanel : Panel
{
    public string Title = "";
    public bool Glow = true;
    public RPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Paint += delegate(object s, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width < 2 || Height < 2) return;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath pr = UI.RRect(r, 12))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, UI.PANEL1, UI.PANEL2, 90f))
                    g.FillPath(b, pr);
                if (Glow)
                {
                    using (Pen pg = new Pen(Color.FromArgb(55, 0, 229, 255), 6)) g.DrawPath(pg, pr);
                    using (Pen pb = new Pen(UI.CYAN_D)) g.DrawPath(pb, pr);
                }
            }
            int L = 16, m = 9;
            using (Pen pc = new Pen(UI.CYAN, 2))
            {
                g.DrawLine(pc, m, m + L, m, m); g.DrawLine(pc, m, m, m + L, m);
                g.DrawLine(pc, Width - 1 - m - L, m, Width - 1 - m, m); g.DrawLine(pc, Width - 1 - m, m, Width - 1 - m, m + L);
                g.DrawLine(pc, m, Height - 1 - m - L, m, Height - 1 - m); g.DrawLine(pc, m, Height - 1 - m, m + L, Height - 1 - m);
                g.DrawLine(pc, Width - 1 - m - L, Height - 1 - m, Width - 1 - m, Height - 1 - m);
                g.DrawLine(pc, Width - 1 - m, Height - 1 - m, Width - 1 - m, Height - 1 - m - L);
            }
            if (Title.Length > 0)
            {
                TextRenderer.DrawText(g, Title, UI.FB, new Rectangle(20, 8, Width - 40, 20), UI.CYAN);
                using (Pen p = new Pen(Color.FromArgb(30, 56, 84))) g.DrawLine(p, 14, 34, Width - 15, 34);
            }
        };
    }
}

// ---------- 发光文字 ----------
class GlowText : Control
{
    public Color Color = UI.CYAN;
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        using (SolidBrush gl = new SolidBrush(Color.FromArgb(55, Color)))
        {
            g.DrawString(Text, Font, gl, 1.6f, 1.6f);
            g.DrawString(Text, Font, gl, -1.6f, -1.6f);
            g.DrawString(Text, Font, gl, 1.6f, -1.6f);
            g.DrawString(Text, Font, gl, -1.6f, 1.6f);
        }
        using (SolidBrush b = new SolidBrush(Color)) g.DrawString(Text, Font, b, 0, 0);
    }
}

// ---------- 导航项 ----------
class NavItem : Control
{
    public string Glyph = "";
    public bool Active;
    bool hover;
    public NavItem(string glyph, string label)
    {
        Glyph = glyph; Text = label;
        Height = 46;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
        MouseEnter += delegate { hover = true; Invalidate(); };
        MouseLeave += delegate { hover = false; Invalidate(); };
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (Width < 2 || Height < 2) return;
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        if (Active)
        {
            using (GraphicsPath pr = UI.RRect(r, 9))
            {
                using (LinearGradientBrush lb = new LinearGradientBrush(r, Color.FromArgb(16, 54, 84), Color.FromArgb(8, 26, 46), 90f))
                    g.FillPath(lb, pr);
                using (Pen pg = new Pen(Color.FromArgb(90, 0, 229, 255), 4)) g.DrawPath(pg, pr);
                using (Pen p = new Pen(UI.CYAN)) g.DrawPath(p, pr);
            }
            using (SolidBrush b = new SolidBrush(UI.CYAN)) g.FillRectangle(b, 5, 11, 3, Height - 22);
        }
        else if (hover)
        {
            using (GraphicsPath pr = UI.RRect(r, 9))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(14, 32, 52))) g.FillPath(b, pr);
                using (Pen p = new Pen(Color.FromArgb(60, 0, 229, 255))) g.DrawPath(p, pr);
            }
        }
        using (SolidBrush ib = new SolidBrush(Active ? UI.CYAN : UI.DIM))
            g.DrawString(Glyph, new Font("Microsoft YaHei UI", 14f), ib, 16, (Height - 21) / 2f);
        using (SolidBrush tb = new SolidBrush(Active ? UI.WHITE : UI.TXT))
            g.DrawString(Text, UI.F10, tb, 47, (Height - 20) / 2f);
    }
}

// ---------- 圆角按钮 ----------
class RButton : Button
{
    bool hover;
    public RButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = UI.FB;
        TabStop = false;
        MouseEnter += delegate { hover = true; Invalidate(); };
        MouseLeave += delegate { hover = false; Invalidate(); };
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath pr = UI.RRect(r, 8))
        {
            Color fill = (hover && Enabled) ? Color.FromArgb(0, 140, 180) : Color.FromArgb(9, 22, 38);
            using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, pr);
            if (hover && Enabled)
            {
                using (Pen pg = new Pen(Color.FromArgb(80, 0, 229, 255), 4)) g.DrawPath(pg, pr);
            }
            Color border = Enabled ? (hover ? UI.CYAN : UI.CYAN_D) : Color.FromArgb(50, 64, 84);
            using (Pen p = new Pen(border)) g.DrawPath(p, pr);
        }
        Color tc = (hover && Enabled) ? Color.Black : (Enabled ? UI.CYAN : UI.DIM);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), tc,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

// ---------- 圆形快捷键 ----------
class CircleKey : Control
{
    bool hover;
    public CircleKey(string glyph, string key, string name)
    {
        Tag = glyph; Text = key; AccessibleName = name;
        Height = 100; Width = 74;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
        MouseEnter += delegate { hover = true; Invalidate(); };
        MouseLeave += delegate { hover = false; Invalidate(); };
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle c = new Rectangle(Width / 2 - 23, 4, 46, 46);
        if (hover)
        {
            using (SolidBrush hb = new SolidBrush(Color.FromArgb(80, 0, 160, 200))) g.FillEllipse(hb, c);
            using (Pen hg = new Pen(Color.FromArgb(70, 0, 229, 255), 5)) g.DrawEllipse(hg, c);
        }
        using (SolidBrush ib = new SolidBrush(Color.FromArgb(12, 26, 44))) g.FillEllipse(ib, c);
        using (Pen p = new Pen(hover ? UI.WHITE : UI.CYAN, 2)) g.DrawEllipse(p, c);
        using (SolidBrush b = new SolidBrush(UI.CYAN))
            g.DrawString((string)Tag, new Font("Microsoft YaHei UI", 15f), b,
                new RectangleF(c.X, c.Y + 2, 46, 40),
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        TextRenderer.DrawText(g, Text, UI.FB, new Rectangle(0, 52, Width, 20), UI.CYAN,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, AccessibleName, UI.F8, new Rectangle(0, 74, Width, 22), UI.DIM,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

// ---------- 状态方块 ----------
class StatusLed : Control
{
    public Color Led = UI.DIM;
    public bool Pulse;
    System.Windows.Forms.Timer t;
    double ph;
    public StatusLed()
    {
        Size = new Size(14, 14);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        t = new System.Windows.Forms.Timer();
        t.Interval = 80;
        t.Tick += delegate { ph += 0.35; Invalidate(); };
        t.Start();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (Pulse)
        {
            int hr = 5 + (int)(2.0 * (Math.Sin(ph) + 1));
            using (SolidBrush hb = new SolidBrush(Color.FromArgb(50, Led))) g.FillEllipse(hb, Width / 2 - hr, Height / 2 - hr, hr * 2, hr * 2);
        }
        using (SolidBrush b = new SolidBrush(Led)) g.FillRectangle(b, 3, 3, Width - 6, Height - 6);
    }
}

// ---------- 主窗体 ----------
class MainForm : Form
{
    // ---- Lua 表达式 (与 TkLua.cs 一致) ----
    const string GET_RES_EXPR =
        "(function()" +
        " local PL=(package and package.loaded) or {}" +
        " local GU=PL[\"Gameplay.GameplayUtil\"] or require(\"Gameplay.GameplayUtil\")" +
        " local ctx=GU.GetContext()" +
        " local fid=ctx.SelfFactionId" +
        " local fi=ctx.Model.Factions[fid]" +
        " local f=function(mid) return tostring(GU.GetFactionMaterialNum(fid,mid)) end" +
        " local r={}" +
        " r[#r+1]=\"金钱=\"..f(Sidl.TK.MaterialType.Money)" +
        " r[#r+1]=\"粮食=\"..f(Sidl.TK.MaterialType.Food)" +
        " r[#r+1]=\"木材=\"..f(Sidl.TK.MaterialType.Wood)" +
        " r[#r+1]=\"石头=\"..f(Sidl.TK.MaterialType.Stone)" +
        " r[#r+1]=\"铁块=\"..f(Sidl.TK.MaterialType.Iron)" +
        " r[#r+1]=\"马匹=\"..f(Sidl.TK.MaterialType.Horse)" +
        " r[#r+1]=\"预备兵=\"..tostring(GU.GetFactionAllReservist(fid))" +
        " r[#r+1]=\"策略点=\"..tostring(fi.StrategemPoints)" +
        " return table.concat(r,\"  \")" +
        " end)()";

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

    const string SET_ALL_RES_EXPR =
        "(function()" +
        " local PL=(package and package.loaded) or {}" +
        " local GU=PL[\"Gameplay.GameplayUtil\"] or require(\"Gameplay.GameplayUtil\")" +
        " local GO=PL[\"Gameplay.GameplayOperation\"]" +
        " if GO == nil then if pcall(require, \"Gameplay.GameplayOperation\") then GO = require(\"Gameplay.GameplayOperation\") end end" +
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
        " local foodCap=GU.SiteLimitFood or 99999" +
        " local foodN=0" +
        " for siteId,_ in pairs(fi.Sites) do" +
        "  local si=GU.GetSiteInfo(siteId)" +
        "  if si ~= nil and si.FactionId == fid and si.Food < foodCap then" +
        "   if GO ~= nil and GO.ChangeSiteFoodNum ~= nil then" +
        "    GO.ChangeSiteFoodNum(siteId, foodCap - si.Food, false, true)" +
        "   else" +
        "    si.Food = foodCap" +
        "   end" +
        "   foodN = foodN + 1" +
        "  end" +
        " end" +
        " if GO ~= nil and GO.ChangeFactionCommonFoodNum ~= nil then" +
        "  GO.ChangeFactionCommonFoodNum(fid, 999999)" +
        " else" +
        "  fi.CommonFood = 999999" +
        " end" +
        " r[#r+1]=\"粮食(每城补至\"..tostring(foodCap)..\", 已改\"..tostring(foodN)..\"城, 超出不减+粮仓) \"..tostring(oldFood)..\" -> \"..tostring(GU.GetFactionAllFood(fid))" +
        " return table.concat(r,\"  \")" +
        " end)()";

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

    const string SET_FOOD_EXPR =
        "(function()" +
        " local PL=(package and package.loaded) or {{}}" +
        " local GU=PL[\"Gameplay.GameplayUtil\"] or require(\"Gameplay.GameplayUtil\")" +
        " local GO=PL[\"Gameplay.GameplayOperation\"]" +
        " if GO == nil then if pcall(require, \"Gameplay.GameplayOperation\") then GO = require(\"Gameplay.GameplayOperation\") end end" +
        " local ctx=GU.GetContext()" +
        " local fid=ctx.SelfFactionId" +
        " local fi=ctx.Model.Factions[fid]" +
        " local v=math.clamp({0},0,99999999)" +
        " local n=0" +
        " for siteId,_ in pairs(fi.Sites) do" +
        "  local si=GU.GetSiteInfo(siteId)" +
        "  if si ~= nil and si.FactionId == fid and si.Food < v then" +
        "   if GO ~= nil and GO.ChangeSiteFoodNum ~= nil then" +
        "    GO.ChangeSiteFoodNum(siteId, v - si.Food, true, true)" +
        "   else" +
        "    si.Food = v" +
        "   end" +
        "   n = n + 1" +
        "  end" +
        " end" +
        " return \"粮食: 每城补到=\" .. tostring(v) .. \" (低于该值的城, 已改\" .. tostring(n) .. \"城, 超出不减) 总计=\" .. tostring(GU.GetFactionAllFood(fid))" +
        " end)()";

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

    // 修改预备兵 ({0}=目标值): 每城上限=人口10%(各不相同), 不超上限, 可重复执行 (幂等), 通用预备兵不动
    const string SET_RESERVIST_EXPR =
        "(function()" +
        " local PL=(package and package.loaded) or {{}}" +
        " local GU=PL[\"Gameplay.GameplayUtil\"] or require(\"Gameplay.GameplayUtil\")" +
        " local GO=PL[\"Gameplay.GameplayOperation\"]" +
        " if GO == nil then if pcall(require, \"Gameplay.GameplayOperation\") then GO = require(\"Gameplay.GameplayOperation\") end end" +
        " local ctx=GU.GetContext()" +
        " local fid=ctx.SelfFactionId" +
        " local fi=ctx.Model.Factions[fid]" +
        " local v=math.clamp({0},0,99999999)" +
        " local n=0" +
        " for siteId,_ in pairs(fi.Sites) do" +
        "  local si=GU.GetSiteInfo(siteId)" +
        "  if si ~= nil and si.FactionId == fid then" +
        "   local limit=math.floor(si.Population/10)" +
        "   local target=math.min(v,limit)" +
        "   if si.Reservist ~= target then" +
        "    if GO ~= nil and GO.ChangeSiteReservist ~= nil then" +
        "     GO.ChangeSiteReservist(siteId, target - si.Reservist, false)" +
        "    else" +
        "     si.Reservist = target" +
        "    end" +
        "    n = n + 1" +
        "   end" +
        "  end" +
        " end" +
        " return \"预备兵: 目标=\" .. tostring(v) .. \" (每城上限=人口10%, 已改\" .. tostring(n) .. \"城) 总计=\" .. tostring(GU.GetFactionAllReservist(fid))" +
        " end)()";

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

    // ---- 协议状态 ----
    TcpClient client;
    StreamReader reader;
    StreamWriter writer;
    readonly object ioLock = new object();
    readonly object queueLock = new object();
    readonly object logLock = new object();
    List<string> queue = new List<string>();
    int cb = 1;
    bool b64;
    volatile bool sessionActive;
    volatile bool executing;
    volatile bool ready;
    string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TkLuaGUI.log");

    // ---- UI 引用 ----
    StatusLed led;
    Label lblStatus, lblConnVal;
    RichTextBox rtb;
    ComboBox cmbRes;
    TextBox txtResVal, txtPointsVal, txtLua;
    Label lblOverview;
    Dictionary<string, Label> valMap = new Dictionary<string, Label>();
    RPanel centerBox;
    Panel[] centerPages;
    NavItem[] navs;

    public MainForm()
    {
        Text = "沙盘战争三国 · 修改中枢";
        ClientSize = new Size(900, 800);
        MinimumSize = new Size(900, 800);
        MaximumSize = new Size(900, 800);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = UI.BG;
        ForeColor = UI.WHITE;
        KeyPreview = true;
        KeyDown += OnHotkey;
        BuildUi();
        Shown += delegate { new Thread(ListenerLoop) { IsBackground = true }.Start(); };
        FormClosing += OnClose;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // 最小化/恢复等瞬间 ClientRectangle 可能为 0x0, 此时创建渐变画刷会抛 ArgumentException
        Rectangle rc = ClientRectangle;
        if (rc.Width <= 0 || rc.Height <= 0)
        {
            base.OnPaintBackground(e);
            return;
        }
        using (LinearGradientBrush b = new LinearGradientBrush(rc, Color.FromArgb(7, 15, 30), Color.FromArgb(3, 7, 15), 90f))
            e.Graphics.FillRectangle(b, rc);
    }

    void BuildUi()
    {
        // ---- 头部 ----
        Panel head = new Panel();
        head.Location = new Point(0, 0);
        head.Size = new Size(900, 72);
        head.BackColor = Color.FromArgb(6, 11, 22);
        head.Paint += delegate(object s, PaintEventArgs e)
        {
            using (Pen p = new Pen(Color.FromArgb(0, 229, 255), 2)) e.Graphics.DrawLine(p, 0, 70, 899, 70);
            using (Pen p2 = new Pen(Color.FromArgb(60, 0, 229, 255), 4)) e.Graphics.DrawLine(p2, 0, 71, 899, 71);
        };
        Controls.Add(head);

        GlowText lblTitle = new GlowText();
        lblTitle.Text = "沙盘战争三国 · 修改中枢";
        lblTitle.Font = UI.F15;
        lblTitle.Location = new Point(20, 10);
        lblTitle.Size = new Size(340, 34);
        head.Controls.Add(lblTitle);

        Label lblSub = new Label();
        lblSub.AutoSize = true;
        lblSub.Text = "TRAINER · LuaPanda 调试通道 v2.3";
        lblSub.Font = UI.FCON;
        lblSub.ForeColor = UI.DIM;
        lblSub.Location = new Point(24, 44);
        head.Controls.Add(lblSub);

        led = new StatusLed();
        led.Location = new Point(738, 26);
        head.Controls.Add(led);

        lblStatus = new Label();
        lblStatus.AutoSize = true;
        lblStatus.Text = "离线 · 等待游戏连接";
        lblStatus.ForeColor = UI.RED;
        lblStatus.Font = UI.FB;
        lblStatus.Location = new Point(760, 22);
        head.Controls.Add(lblStatus);

        // ---- 左侧导航 ----
        RPanel navBox = new RPanel();
        navBox.Title = "导航 · NAV";
        navBox.Location = new Point(14, 88);
        navBox.Size = new Size(150, 448);
        Controls.Add(navBox);

        navs = new NavItem[4];
        navs[0] = new NavItem("◆", "总览");
        navs[1] = new NavItem("✦", "策略点");
        navs[2] = new NavItem("▣", "资源");
        navs[3] = new NavItem("λ", "高级 Lua");
        for (int i = 0; i < 4; i++)
        {
            NavItem n = navs[i];
            n.Width = 126;
            n.Location = new Point(11, 44 + i * 56);
            int idx = i;
            n.Click += delegate { SetNav(idx); };
            navBox.Controls.Add(n);
        }

        // ---- 中央功能区 ----
        centerBox = new RPanel();
        centerBox.Location = new Point(176, 88);
        centerBox.Size = new Size(470, 448);
        Controls.Add(centerBox);

        // 标题由 centerBox.Title 统一绘制 (与左/右面板对齐)

        centerPages = new Panel[4];
        for (int i = 0; i < 4; i++)
        {
            Panel p = new Panel();
            p.Location = new Point(12, 34);
            p.Size = new Size(446, 302);
            p.BackColor = Color.Transparent;
            centerBox.Controls.Add(p);
            centerPages[i] = p;
        }

        // F1~F6 圆形快捷键 (中央面板底部, 所有页共用)
        AddKey(0, "◆", "F1", "点数", delegate { DoPoints(9999); });
        AddKey(1, "★", "F2", "全满", delegate { Exec(SET_ALL_RES_EXPR, "资源全满 (粮食至上限)"); });
        AddKey(2, "◉", "F3", "查询", delegate { DoQuery(); });
        AddKey(3, "▣", "F4", "单项", delegate { DoRes(); });
        AddKey(4, "✎", "F5", "自定义", delegate { DoPoints(0); });
        AddKey(5, "λ", "F6", "Lua", delegate { DoLua(); });

        // 0 总览
        Panel pg0 = centerPages[0];
        Label h0 = new Label();
        h0.Text = "获取当前势力资源快照";
        h0.ForeColor = UI.DIM;
        h0.Font = UI.F9;
        h0.Location = new Point(18, 12);
        h0.AutoSize = true;
        pg0.Controls.Add(h0);
        RButton b0 = new RButton();
        b0.Text = "立 即 查 询   [F3]";
        b0.Size = new Size(220, 44);
        b0.Location = new Point(113, 42);
        b0.Click += delegate { DoQuery(); };
        pg0.Controls.Add(b0);
        lblOverview = new Label();
        lblOverview.Text = "尚无数据, 点击查询获取";
        lblOverview.ForeColor = UI.TXT;
        lblOverview.Font = UI.FCON;
        lblOverview.Location = new Point(24, 100);
        lblOverview.Size = new Size(400, 195);
        pg0.Controls.Add(lblOverview);

        // 1 策略点
        Panel pg1 = centerPages[1];
        RButton b1 = new RButton();
        b1.Text = "一键 9999   [F1]";
        b1.Size = new Size(230, 46);
        b1.Location = new Point(108, 36);
        b1.Click += delegate { DoPoints(9999); };
        pg1.Controls.Add(b1);
        Label l1 = new Label();
        l1.Text = "自定义数值";
        l1.ForeColor = UI.DIM;
        l1.Font = UI.F9;
        l1.Location = new Point(62, 122);
        l1.AutoSize = true;
        pg1.Controls.Add(l1);
        txtPointsVal = Box("数值");
        txtPointsVal.Location = new Point(150, 116);
        txtPointsVal.Width = 110;
        pg1.Controls.Add(txtPointsVal);
        RButton b1b = new RButton();
        b1b.Text = "执行 [F5]";
        b1b.Size = new Size(110, 30);
        b1b.Location = new Point(272, 115);
        b1b.Click += delegate { DoPoints(0); };
        pg1.Controls.Add(b1b);
        Label h1 = new Label();
        h1.Text = "提示: 修改后打开一次策略面板即可看到新数值";
        h1.ForeColor = UI.DIM;
        h1.Font = UI.F8;
        h1.Location = new Point(20, 170);
        h1.AutoSize = true;
        pg1.Controls.Add(h1);

        // 2 资源
        Panel pg2 = centerPages[2];
        RButton b2 = new RButton();
        b2.Text = "资源全满 (粮食补到上限)   [F2]";
        b2.Size = new Size(260, 46);
        b2.Location = new Point(93, 36);
        b2.Click += delegate { Exec(SET_ALL_RES_EXPR, "资源全满 (粮食至上限)"); };
        pg2.Controls.Add(b2);
        Label l2 = new Label();
        l2.Text = "单项修改";
        l2.ForeColor = UI.DIM;
        l2.Font = UI.F9;
        l2.Location = new Point(56, 116);
        l2.AutoSize = true;
        pg2.Controls.Add(l2);
        cmbRes = new ComboBox();
        cmbRes.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbRes.FlatStyle = FlatStyle.Flat;
        cmbRes.Font = UI.F9;
        cmbRes.BackColor = UI.INBG;
        cmbRes.ForeColor = UI.WHITE;
        cmbRes.Items.AddRange(new object[] { "粮食", "木材", "石头", "铁块", "马匹", "金钱", "预备兵" });
        cmbRes.SelectedIndex = 0;
        cmbRes.Location = new Point(136, 110);
        cmbRes.Width = 88;
        pg2.Controls.Add(cmbRes);
        txtResVal = Box("数值");
        txtResVal.Location = new Point(234, 110);
        txtResVal.Width = 86;
        pg2.Controls.Add(txtResVal);
        RButton b2b = new RButton();
        b2b.Text = "执行 [F4]";
        b2b.Size = new Size(110, 30);
        b2b.Location = new Point(330, 109);
        b2b.Click += delegate { DoRes(); };
        pg2.Controls.Add(b2b);
        Label h2 = new Label();
        h2.Text = "提示: 资源全满粮食每城补至99999(超出不减); 预备兵每城不超该城人口10%上限";
        h2.ForeColor = UI.DIM;
        h2.Font = UI.F8;
        h2.Location = new Point(20, 164);
        h2.AutoSize = true;
        pg2.Controls.Add(h2);
        RButton b2c = new RButton();
        b2c.Text = "预备兵每城补满 (至人口10%上限)";
        b2c.Size = new Size(260, 40);
        b2c.Location = new Point(93, 196);
        b2c.Click += delegate { Exec(string.Format(SET_RESERVIST_EXPR, "99999999"), "预备兵每城补满"); };
        pg2.Controls.Add(b2c);

        // 3 高级 Lua
        Panel pg3 = centerPages[3];
        txtLua = new TextBox();
        txtLua.Multiline = true;
        txtLua.ScrollBars = ScrollBars.Vertical;
        txtLua.BackColor = UI.INBG;
        txtLua.ForeColor = UI.WHITE;
        txtLua.BorderStyle = BorderStyle.FixedSingle;
        txtLua.Font = UI.FCON;
        txtLua.Location = new Point(18, 12);
        txtLua.Size = new Size(410, 240);
        txtLua.Text = "(function() return \"hello\" end)()";
        pg3.Controls.Add(txtLua);
        RButton b3 = new RButton();
        b3.Text = "执行表达式 [F6]";
        b3.Size = new Size(180, 38);
        b3.Location = new Point(133, 262);
        b3.Click += delegate { DoLua(); };
        pg3.Controls.Add(b3);

        // ---- 右侧数据面板 ----
        RPanel rightBox = new RPanel();
        rightBox.Title = "主览 · INFO";
        rightBox.Location = new Point(658, 88);
        rightBox.Size = new Size(228, 448);
        Controls.Add(rightBox);
        string[] keys = { "金钱", "粮食", "木材", "石头", "铁块", "马匹", "预备兵", "策略点" };
        for (int i = 0; i < keys.Length; i++)
        {
            Label lk = new Label();
            lk.Text = keys[i];
            lk.ForeColor = UI.DIM;
            lk.Font = UI.F10;
            lk.Location = new Point(18, 44 + i * 38);
            lk.AutoSize = true;
            rightBox.Controls.Add(lk);
            Label lv = new Label();
            lv.Text = "—";
            lv.ForeColor = UI.CYAN;
            lv.Font = UI.FCONB;
            lv.Location = new Point(106, 44 + i * 38);
            lv.Size = new Size(108, 24);
            lv.TextAlign = ContentAlignment.MiddleRight;
            rightBox.Controls.Add(lv);
            valMap[keys[i]] = lv;
        }
        Label lc = new Label();
        lc.Text = "连接状态";
        lc.ForeColor = UI.DIM;
        lc.Font = UI.F10;
        lc.Location = new Point(18, 350);
        lc.AutoSize = true;
        rightBox.Controls.Add(lc);
        lblConnVal = new Label();
        lblConnVal.Text = "离线";
        lblConnVal.ForeColor = UI.RED;
        lblConnVal.Font = UI.FB;
        lblConnVal.Location = new Point(106, 350);
        lblConnVal.Size = new Size(108, 24);
        lblConnVal.TextAlign = ContentAlignment.MiddleRight;
        rightBox.Controls.Add(lblConnVal);
        Label lh = new Label();
        lh.Text = "按 F3 查询后自动刷新";
        lh.ForeColor = UI.DIM;
        lh.Font = UI.F8;
        lh.Location = new Point(18, 396);
        lh.AutoSize = true;
        rightBox.Controls.Add(lh);

        // ---- 日志面板 ----
        RPanel logBox = new RPanel();
        logBox.Title = "运行日志 · LOG";
        logBox.Location = new Point(14, 548);
        logBox.Size = new Size(872, ClientSize.Height - 548 - 14);
        Controls.Add(logBox);
        rtb = new RichTextBox();
        rtb.Location = new Point(12, 40);
        rtb.Size = new Size(848, logBox.Height - 52);
        rtb.ReadOnly = true;
        rtb.BackColor = Color.FromArgb(5, 11, 20);
        rtb.ForeColor = UI.WHITE;
        rtb.BorderStyle = BorderStyle.None;
        rtb.Font = UI.FCON;
        rtb.DetectUrls = false;
        logBox.Controls.Add(rtb);

        SetNav(0);
    }

    void AddKey(int idx, string glyph, string key, string name, EventHandler onClick)
    {
        CircleKey c = new CircleKey(glyph, key, name);
        c.Location = new Point(14 + idx * 75, 344);
        c.Click += onClick;
        centerBox.Controls.Add(c);
    }

    TextBox Box(string placeholder)
    {
        TextBox t = new TextBox();
        t.BackColor = UI.INBG;
        t.ForeColor = UI.WHITE;
        t.BorderStyle = BorderStyle.FixedSingle;
        t.Font = UI.FCON;
        return t;
    }

    void SetNav(int i)
    {
        for (int j = 0; j < navs.Length; j++)
        {
            navs[j].Active = (j == i);
            navs[j].Invalidate();
        }
        for (int j = 0; j < centerPages.Length; j++) centerPages[j].Visible = (j == i);
        string[] titles = { "总览 · 资源快照", "策略点 · 修改", "资源 · 修改", "高级 · 自定义 Lua" };
        centerBox.Title = titles[i];
        centerBox.Invalidate();
    }

    // ---- 动作入口 ----

    void DoPoints(long preset)
    {
        long n = preset;
        if (preset == 0)
        {
            if (!long.TryParse(txtPointsVal.Text.Trim(), out n)) { Log("!! 请输入有效数字"); return; }
        }
        Exec(string.Format(SET_POINTS_EXPR, n.ToString()), "策略点数=" + n);
    }

    void DoRes()
    {
        int ri = cmbRes.SelectedIndex + 1;
        long n;
        if (!long.TryParse(txtResVal.Text.Trim(), out n)) { Log("!! 请输入有效数字"); return; }
        string[] names = { "Food", "Wood", "Stone", "Iron", "Horse", "Money" };
        string[] labels = { "粮食", "木材", "石头", "铁块", "马匹", "金钱", "预备兵" };
        string label = labels[ri - 1];
        if (ri == 1) Exec(string.Format(SET_FOOD_EXPR, n), label + "=" + n);
        else if (ri == 6) Exec(string.Format(SET_MONEY_EXPR, n), label + "=" + n);
        else if (ri == 7) Exec(string.Format(SET_RESERVIST_EXPR, n), label + "=" + n);
        else Exec(string.Format(SET_MATERIAL_EXPR, names[ri - 1], n), label + "=" + n);
    }

    void DoQuery()
    {
        Exec(GET_RES_EXPR, "查看资源", UpdateResUi);
    }

    void DoLua()
    {
        string expr = txtLua.Text.Trim();
        if (expr.Length == 0) { Log("!! 请输入 Lua 表达式"); return; }
        Exec(expr, "自定义表达式");
    }

    void OnHotkey(object s, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.F1: DoPoints(9999); e.Handled = true; break;
            case Keys.F2: Exec(SET_ALL_RES_EXPR, "资源全满 (粮食至上限)"); e.Handled = true; break;
            case Keys.F3: DoQuery(); e.Handled = true; break;
            case Keys.F4: DoRes(); e.Handled = true; break;
            case Keys.F5: DoPoints(0); e.Handled = true; break;
            case Keys.F6: DoLua(); e.Handled = true; break;
        }
    }

    // 解析查询结果, 更新右侧面板与总览页
    void UpdateResUi(string val)
    {
        string[] lines = val.Split(new string[] { "  " }, StringSplitOptions.RemoveEmptyEntries);
        StringBuilder sb = new StringBuilder();
        foreach (string ln in lines)
        {
            string[] kv = ln.Split('=');
            if (kv.Length < 2) continue;
            string k = kv[0].Trim(), v = kv[1].Trim();
            sb.AppendLine(k + "    " + v);
            Label lbl;
            if (valMap.TryGetValue(k, out lbl)) lbl.Text = v;
        }
        if (sb.Length > 0) lblOverview.Text = sb.ToString();
        else lblOverview.Text = val;
    }

    // ---------- UI 线程辅助 ----------

    void Ui(Action a)
    {
        try { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); }
        catch { }
    }

    void AppendLog(string s, Color c)
    {
        Ui(delegate
        {
            if (rtb == null || rtb.IsDisposed) return;
            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionLength = 0;
            rtb.SelectionColor = c;
            rtb.AppendText(s + Environment.NewLine);
            rtb.SelectionStart = rtb.TextLength;
            rtb.ScrollToCaret();
        });
    }

    void Log(string s)
    {
        Color c = UI.WHITE;
        if (s.StartsWith("!!")) c = UI.RED;
        else if (s.StartsWith(">>")) c = UI.CYAN;
        else if (s.StartsWith("[连接]") || s.StartsWith("[提示]") || s.StartsWith("[错误]")) c = UI.DIM;
        else if (s.IndexOf("====") >= 0) c = UI.GREEN;
        lock (logLock)
        {
            try { File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss") + " " + s + "\r\n", Encoding.UTF8); }
            catch { }
        }
        AppendLog(s, c);
    }

    void LogF(string s)
    {
        lock (logLock)
        {
            try { File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss") + " " + s + "\r\n", Encoding.UTF8); }
            catch { }
        }
    }

    void SetState(int st) // 0=等待(离线) 1=握手中 2=就绪
    {
        Ui(delegate
        {
            switch (st)
            {
                case 0:
                    led.Led = UI.RED; led.Pulse = false;
                    lblStatus.Text = "离线 · 等待游戏连接";
                    lblStatus.ForeColor = UI.RED;
                    lblConnVal.Text = "离线";
                    lblConnVal.ForeColor = UI.RED;
                    break;
                case 1:
                    led.Led = UI.AMBER; led.Pulse = true;
                    lblStatus.Text = "握手中...";
                    lblStatus.ForeColor = UI.AMBER;
                    lblConnVal.Text = "连接中";
                    lblConnVal.ForeColor = UI.AMBER;
                    break;
                case 2:
                    led.Led = UI.GREEN; led.Pulse = false;
                    lblStatus.Text = "就绪 · 可执行";
                    lblStatus.ForeColor = UI.GREEN;
                    lblConnVal.Text = "就绪";
                    lblConnVal.ForeColor = UI.GREEN;
                    break;
            }
            ready = (st == 2);
        });
    }

    // ---------- 协议实现 (与 TkLua.cs 一致) ----------

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

    void Send(string json)
    {
        lock (ioLock)
        {
            writer.Write(json + "|*|\n");
            writer.Flush();
        }
    }

    void ReaderLoop()
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
        finally { sessionActive = false; }
    }

    string WaitFor(Func<string, bool> pred, int timeoutMs)
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

    void DumpPending()
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
        // 取到 ',' 或 '}' 为止, 跳过引号内的字符(值本身可能含 , 或 } )
        // 注: Lua 表的 JSON 序列化字段顺序不定, isSuccess 可能是最后一个字段,
        //     旧实现只认 ',' 会把 "true"} 整个拿走导致成功被误判为失败
        int j = i;
        bool inStr = false;
        for (; j < json.Length; j++)
        {
            char c = json[j];
            if (c == '"')
            {
                if (j == i || json[j - 1] != '\\') inStr = !inStr;
            }
            else if (!inStr && (c == ',' || c == '}'))
            {
                break;
            }
        }
        return json.Substring(i, j - i).Trim();
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

    string StopAndEval(string expr, string label)
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
            return null;
        }
        Log(">> 游戏已暂停, 开始执行表达式...");

        bool sawWrong = false, sawOther = false;
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
                if (val.IndexOf("输入了错误的变量信息") >= 0) sawWrong = true;
                else sawOther = true;
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
                return val;
            }
            LogF("[stackId=" + st + " 失败] " + (val ?? "(空)"));
        }
        string c2 = (cb++).ToString();
        Send("{\"cmd\":\"continue\",\"callbackId\":\"" + c2 + "\",\"info\":{}}");
        Log("!! 执行失败 (所有 stackId 均未成功), 游戏已恢复运行; 详情见 TkLuaGUI.log");
        if (sawWrong && !sawOther)
            Log("!! 提示: 游戏环境尚未初始化(刚开新档/还没打开过面板), 请在游戏里加载存档并打开一次面板后再点按钮");
        return null;
    }

    string Handshake()
    {
        NetworkStream ns = client.GetStream();
        reader = new StreamReader(ns, Encoding.UTF8);
        writer = new StreamWriter(ns, new UTF8Encoding(false));
        Thread rt = new Thread(ReaderLoop);
        rt.IsBackground = true;
        rt.Start();

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

    void ListenerLoop()
    {
        while (true)
        {
            TcpListener l = new TcpListener(IPAddress.Loopback, 8818);
            try { l.Start(); }
            catch (Exception ex)
            {
                Log("!! 监听 8818 失败: " + ex.Message);
                return;
            }
            SetState(0);
            Log("[连接] 正在监听 127.0.0.1:8818, 等待游戏连接...");
            Log("[提示] 启动顺序: 先开修改器, 再启动游戏; 游戏启动时会自动连接");
            TcpClient c = null;
            try { c = l.AcceptTcpClient(); }
            catch
            {
                try { l.Stop(); } catch { }
                return;
            }
            try { l.Stop(); } catch { }
            client = c;
            lock (queueLock) queue.Clear();
            SetState(1);
            Log("[连接] 游戏已连接: " + c.Client.RemoteEndPoint);
            string cfg = null;
            try { cfg = Handshake(); }
            catch (Exception ex) { Log("[连接] 握手异常: " + ex.Message); }
            if (cfg != null)
            {
                sessionActive = true;
                SetState(2);
                Log("[提示] 已就绪: 在游戏里加载存档并打开一次面板后, 按 F1~F6 或点按钮即可修改");
                while (sessionActive) Thread.Sleep(400);
                Log("[连接] 连接已断开");
            }
            else
            {
                Log("[连接] 握手失败, 关闭连接");
            }
            try { c.Close(); } catch { }
            client = null;
            SetState(0);
            Log("[连接] 等待游戏重新连接... (若游戏加载过存档, 需重启游戏)");
        }
    }

    void Exec(string expr, string label)
    {
        Exec(expr, label, null);
    }

    void Exec(string expr, string label, Action<string> onOk)
    {
        if (executing) return;
        if (!ready)
        {
            Log("!! 尚未连接游戏: 请先启动游戏, 等状态变为『就绪』");
            return;
        }
        executing = true;
        new Thread(new ThreadStart(delegate
        {
            try
            {
                string v = StopAndEval(expr, label);
                if (v != null && onOk != null)
                {
                    string vv = v;
                    Ui(delegate { onOk(vv); });
                }
            }
            catch (Exception ex) { Log("!! 执行异常: " + ex.Message); }
            finally { executing = false; }
        }))
        { IsBackground = true }.Start();
    }

    void OnClose(object s, FormClosingEventArgs e)
    {
        if (executing)
        {
            DialogResult dr = MessageBox.Show(
                "正在执行中, 此时退出可能导致游戏停留在暂停态!\r\n建议等执行完成后再关闭。\r\n\r\n仍要退出吗?",
                "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }
        try { if (client != null) client.Close(); } catch { }
    }
}
