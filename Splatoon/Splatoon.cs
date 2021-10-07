﻿using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Lumina.Excel.GeneratedSheets;

namespace Splatoon;
unsafe class Splatoon : IDalamudPlugin
{
    public string Name => "Splatoon";
    internal Gui DrawingGui;
    internal CGui ConfigGui;
    internal Commands CommandManager;
    internal Memory MemoryManager;
    internal ChlogGui ChangelogGui;
    internal Configuration Config;
    internal Dictionary<ushort, TerritoryType> Zones;
    internal string[] LogStorage = new string[100];
    internal long CombatStarted = 0;
    internal HashSet<DisplayObject> displayObjects = new HashSet<DisplayObject>();
    internal double CamAngleX;
    internal Dictionary<int, string> Jobs = new Dictionary<int, string>();
    internal HashSet<(float x, float y, float z, float r)> draw = new HashSet<(float x, float y, float z, float r)>();
    internal float CamAngleY;
    internal float CamZoom;
    internal bool S2WActive = false;
    internal bool prevMouseState = false;
    internal string SFind = null;
    internal int CurrentLineSegments;
    internal ConcurrentQueue<System.Action> tickScheduler;
    internal List<DynamicElement> dynamicElements;
    internal HTTPServer HttpServer;
    internal bool prevCombatState = false;
    static internal Vector3? PlayerPosCache = null;
    internal Profiling Profiler;
    internal Dictionary<(IntPtr Addr, uint Id), string> NamesCache;
    internal Dictionary<(IntPtr Addr, long Id, int StrHash), bool> LookupResultCache;
    internal Queue<string> ChatMessageQueue;
    internal string CurrentChatMessage = null;
    internal Element Clipboard = null;

    public string AssemblyLocation { get => assemblyLocation; set => assemblyLocation = value; }
    private string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

    public void Dispose()
    {
        Config.Save();
        DrawingGui.Dispose();
        ConfigGui.Dispose();
        CommandManager.Dispose();
        Svc.ClientState.TerritoryChanged -= TerritoryChangedEvent;
        Svc.Framework.Update -= Tick;
        SetupShutdownHttp(false);
        Svc.Chat.ChatMessage -= OnChatMessage;
        //Svc.Chat.Print("Disposing");
    }

    public Splatoon(DalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Svc>();
        //Svc.Chat.Print("Loaded");
        ChatMessageQueue = new Queue<string>();
        Profiler = new Profiling(this);
        CommandManager = new Commands(this);
        NamesCache = new Dictionary<(IntPtr Addr, uint Id), string>();
        LookupResultCache = new Dictionary<(IntPtr Addr, long Id, int StrHash), bool>();
        Zones = Svc.Data.GetExcelSheet<TerritoryType>().ToDictionary(row => (ushort)row.RowId, row => row);
        Jobs = Svc.Data.GetExcelSheet<ClassJob>().ToDictionary(row => (int)row.RowId, row => row.Name.ToString());
        Svc.ClientState.TerritoryChanged += TerritoryChangedEvent;
        MemoryManager = new Memory(this);
        Config = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(this);
        Svc.Framework.Update += Tick;
        DrawingGui = new Gui(this);
        ConfigGui = new CGui(this);
        if(ChlogGui.ChlogVersion > Config.ChlogReadVer)
        {
            ChangelogGui = new ChlogGui(this);
        }
        tickScheduler = new ConcurrentQueue<System.Action>();
        dynamicElements = new List<DynamicElement>();
        SetupShutdownHttp(Config.UseHttpServer);
        Svc.Chat.ChatMessage += OnChatMessage;
    }

    private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (Profiler.Enabled) Profiler.MainTickChat.StartTick();
        var inttype = (int)type;
        if(inttype == 68 || inttype == 2105 || type == XivChatType.SystemMessage || Config.TriggerAnyMessages)
        {
            ChatMessageQueue.Enqueue(message.ToString());
        }
        if (Profiler.Enabled) Profiler.MainTickChat.StopTick();
    }

    internal void SetupShutdownHttp(bool enable)
    {
        if (enable)
        {
            if(HttpServer == null)
            {
                try
                {
                    HttpServer = new HTTPServer(this);
                }
                catch(Exception e)
                {
                    Log("Critical error occurred while starting HTTP server.", true);
                    Log(e.Message, true);
                    Log(e.StackTrace);
                    HttpServer = null;
                }
            }
        }
        else
        {
            if (HttpServer != null)
            {
                HttpServer.Dispose();
                HttpServer = null;
            }
        }
    }

    private void TerritoryChangedEvent(object sender, ushort e)
    {
        NamesCache.Clear();
        LookupResultCache.Clear();
        if (SFind != null)
        {
            SFind = null;
            Svc.Toasts.ShowQuest("[Splatoon] Search stopped");
        }
        for (var i = dynamicElements.Count - 1; i >= 0; i--)
        {
            var de = dynamicElements[i];
            foreach(var l in de.Layouts)
            {
                if (l.UseTriggers)
                {
                    foreach (var t in l.Triggers)
                    {
                        if (t.ResetOnTChange) t.FiredState = 0;
                    }
                }
            }
            foreach (var dt in de.DestroyTime)
            {
                if (dt == (long)DestroyCondition.TERRITORY_CHANGE)
                {
                    dynamicElements.RemoveAt(i);
                }
            }
        }
        foreach(var l in Config.Layouts.Values)
        {
            if (l.UseTriggers)
            {
                foreach(var t in l.Triggers)
                {
                    if (t.ResetOnTChange) t.FiredState = 0;
                }
            }
        }
    }

    [HandleProcessCorruptedStateExceptions]
    public void Tick(Framework framework)
    {
        if (Profiler.Enabled) Profiler.MainTick.StartTick();
        try
        {
            if (Profiler.Enabled) Profiler.MainTickDequeue.StartTick();
            if (tickScheduler.TryDequeue(out var action))
            {
                action.Invoke();
            }
            if (Profiler.Enabled)
            {
                Profiler.MainTickDequeue.StopTick();
                Profiler.MainTickPrepare.StartTick();
            }
            PlayerPosCache = null;
            displayObjects.Clear();
            if (Svc.ClientState?.LocalPlayer != null)
            {
                ChatMessageQueue.TryDequeue(out CurrentChatMessage);
                if (Config.verboselog && CurrentChatMessage != null) Log("Dequeued message: " + CurrentChatMessage);
                var pl = Svc.ClientState.LocalPlayer;
                if (Svc.ClientState.LocalPlayer.Address == IntPtr.Zero)
                {
                    Log("Pointer to LocalPlayer.Address is zero");
                    return;
                }
                CamAngleX = *MemoryManager.CameraAddressX + Math.PI;
                if (CamAngleX > Math.PI) CamAngleX -= 2 * Math.PI;
                CamAngleY = *MemoryManager.CameraAddressY;
                CamZoom = *MemoryManager.CameraZoom;
                /*Range conversion https://stackoverflow.com/questions/5731863/mapping-a-numeric-range-onto-another
                slope = (output_end - output_start) / (input_end - input_start)
                output = output_start + slope * (input - input_start) */
                CurrentLineSegments = (int)((3f + -0.108108f * (CamZoom - 1.5f)) * Config.lineSegments);

                if (Svc.Condition[ConditionFlag.InCombat])
                {
                    if (CombatStarted == 0)
                    {
                        CombatStarted = Environment.TickCount64;
                    }
                }
                else
                {
                    if (CombatStarted != 0)
                    {
                        CombatStarted = 0;
                        Log("Combat ended event");
                        foreach (var l in Config.Layouts.Values)
                        {
                            if (l.UseTriggers)
                            {
                                foreach (var t in l.Triggers)
                                {
                                    if (t.ResetOnCombatExit)
                                    {
                                        t.FiredState = 0;
                                        l.TriggerCondition = 0;
                                    }
                                }
                            }
                        }
                        foreach (var de in dynamicElements)
                        {
                            foreach (var l in de.Layouts)
                            {
                                if (l.UseTriggers)
                                {
                                    foreach (var t in l.Triggers)
                                    {
                                        if (t.ResetOnCombatExit)
                                        {
                                            t.FiredState = 0;
                                            l.TriggerCondition = 0;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                //if (CamAngleY > Config.maxcamY) return;

                if(Profiler.Enabled)
                {
                    Profiler.MainTickPrepare.StopTick();
                    Profiler.MainTickFind.StartTick();
                }

                if (SFind != null)
                {
                    var col = Environment.TickCount64 % 1000 < 500 ? Colors.Red : Colors.Yellow;
                    var findEl = new Element(1)
                    {
                        thicc = 3f,
                        radius = 0f,
                        refActorName = SFind,
                        overlayText = "Search: " + SFind,
                        overlayVOffset = 1.7f,
                        overlayTextColor = col,
                        color = col,
                        includeHitbox = true,
                        onlyTargetable = true,
                        tether = Config.TetherOnFind,
                    };
                    ProcessElement(findEl);
                }

                if (Profiler.Enabled)
                {
                    Profiler.MainTickFind.StopTick();
                    Profiler.MainTickCalcPresets.StartTick();
                }

                foreach (var i in Config.Layouts.Values)
                {
                    ProcessLayout(i);
                }

                if (Profiler.Enabled)
                {
                    Profiler.MainTickCalcPresets.StopTick();
                    Profiler.MainTickCalcDynamic.StartTick();
                }

                for (var i = dynamicElements.Count - 1; i >= 0; i--)
                {
                    var de = dynamicElements[i];

                    foreach (var dt in de.DestroyTime)
                    {
                        if (dt == (long)DestroyCondition.COMBAT_EXIT)
                        {
                            if (!Svc.Condition[ConditionFlag.InCombat] && prevCombatState)
                            {
                                dynamicElements.RemoveAt(i);
                                continue;
                            }
                        }
                        else if (dt > 0)
                        {
                            if (Environment.TickCount64 > dt)
                            {
                                dynamicElements.RemoveAt(i);
                                continue;
                            }
                        }
                    }
                    foreach (var l in de.Layouts)
                    {
                        ProcessLayout(l);
                    }
                    foreach (var e in de.Elements)
                    {
                        ProcessElement(e);
                    }
                }

                if (Profiler.Enabled) Profiler.MainTickCalcDynamic.StopTick();
            }
            else
            {
                Profiler.MainTickPrepare.StopTick();
            }
            prevCombatState = Svc.Condition[ConditionFlag.InCombat];
            CurrentChatMessage = null;
        }
        catch(Exception e)
        {
            Log("Caught exception: "+e.Message);
            Log(e.StackTrace);
        }
        if (Profiler.Enabled) Profiler.MainTick.StopTick();
    }

    private void ProcessLayout(Layout i)
    {
        if (!IsLayoutVisible(i)) return;
        if (i.UseTriggers)
        {
            foreach(var t in i.Triggers)
            {
                if (t.FiredState == 2) continue;
                else if (t.FiredState == 0)
                {
                    if (t.Type == 0 || t.Type == 1)
                    {
                        if (CombatStarted != 0 && Environment.TickCount64 - CombatStarted > t.TimeBegin * 1000)
                        {
                            if(t.Duration == 0)
                            {
                                t.FiredState = 2;
                            }
                            else
                            {
                                t.FiredState = 1;
                                t.DisableAt = Environment.TickCount64 + t.Duration * 1000;
                            }
                            i.TriggerCondition = t.Type == 0 ? 1 : -1;
                        }
                    }
                    else if (CurrentChatMessage != null && (t.Type == 2 || t.Type == 3))
                    {
                        if (CurrentChatMessage.ContainsIgnoreCase(t.Match))
                        {
                            if (t.Duration == 0)
                            {
                                t.FiredState = 0;
                            }
                            else
                            {
                                t.FiredState = 1;
                                t.DisableAt = Environment.TickCount64 + t.Duration * 1000;
                            }
                            i.TriggerCondition = t.Type == 2 ? 1 : -1;
                        }
                    }
                }
                else if(t.FiredState == 1)
                {
                    if(Environment.TickCount64 > t.DisableAt)
                    {
                        t.FiredState = (t.Type == 2 || t.Type == 3)?0:2;
                        t.DisableAt = 0;
                        i.TriggerCondition = 0;
                    }
                }
                       
            }
            if (i.TriggerCondition == -1 || (i.TriggerCondition == 0 && i.DCond == 5)) return;
        }
        foreach (var e in i.Elements.Values.ToArray())
        {
            ProcessElement(e);
        }
    }

    internal bool IsNameContainsValue(GameObject a, string value)
    {
        if (Config.DirectNameComparison) return a.Name.ToString().ContainsIgnoreCase(value);
        var hash = value.GetHashCode();
        var objectID = MemoryManager.GameObject_GetObjectID(a.Address);
        if (!LookupResultCache.ContainsKey((a.Address, objectID, hash)))
        {
            LookupResultCache.Add((a.Address, objectID, hash), a.Name.ToString().ContainsIgnoreCase(value));
        }
        return LookupResultCache[(a.Address, objectID, hash)];
    }

    internal string GetObjectName(GameObject a)
    {
        if(!NamesCache.ContainsKey((a.Address, a.ObjectId)))
        {
            NamesCache.Add((a.Address, a.ObjectId), a.Name.ToString());
        }
        return NamesCache[(a.Address, a.ObjectId)];
    }

    internal void ProcessElement(Element e)
    {
        if (!e.Enabled) return;
        draw.Clear();
        if (e.screen2world != 0)
        {
            var lmbdown = Bitmask.IsBitSet(Native.GetKeyState(0x01), 15);
            S2WActive = true;
            //1: editing absolute point 
            //2: editing main point
            //3: editing secondary point
            var mousePos = ImGui.GetIO().MousePos;
            if (Svc.GameGui.ScreenToWorld(new Vector2(mousePos.X, mousePos.Y), out var worldPos, Config.maxdistance * 5))
            {
                if (e.screen2world == 1 || e.screen2world == 2)
                {
                    e.refX = worldPos.X;
                    e.refY = worldPos.Z;
                    e.refZ = worldPos.Y;
                }
                else if (e.screen2world == 3)
                {
                    e.offX = worldPos.X;
                    e.offY = worldPos.Z;
                    e.offZ = worldPos.Y;
                }
            }
            if (!lmbdown && prevMouseState)
            {
                e.screen2world = 0;
                S2WActive = false;
            }
            prevMouseState = lmbdown;
            if (Environment.TickCount64 % 500 < 250)
            {
                var x = e.screen2world == 3 ? e.offX : e.refX;
                var y = e.screen2world == 3 ? e.offY : e.refY;
                var z = e.screen2world == 3 ? e.offZ : e.refZ;
                displayObjects.Add(new DisplayObjectLine(x + 2f, y + 2f, z, x - 2f, y - 2f, z, 2f, Colors.Red));
                displayObjects.Add(new DisplayObjectLine(x - 2f, y + 2f, z, x + 2f, y - 2f, z, 2f, Colors.Red));
            }
        }
        
        float radius = e.radius;
        if (e.type == 0)
        {
            draw.Add((e.refX, e.refY, e.refZ, radius));
            if (e.tether)
            {
                displayObjects.Add(new DisplayObjectLine(e.refX + e.offX,
                    e.refY + e.offY,
                    e.refZ + e.offZ,
                    GetPlayerPositionXZY().X, GetPlayerPositionXZY().Y, GetPlayerPositionXZY().Z,
                    e.thicc, e.color));
            }
        }
        else if (e.type == 1)
        {
            if (e.includeOwnHitbox) radius += Svc.ClientState.LocalPlayer.HitboxRadius;
            if (e.refActorType == 1)
            {
                draw.Add((GetPlayerPositionXZY().X, GetPlayerPositionXZY().Y,
                    GetPlayerPositionXZY().Z, radius));
            }
            else if (e.refActorType == 2 && Svc.Targets.Target != null
                && Svc.Targets.Target is BattleNpc)
            {
                if (e.includeHitbox) radius += Svc.Targets.Target.HitboxRadius;
                draw.Add((Svc.Targets.Target.GetPositionXZY().X, Svc.Targets.Target.GetPositionXZY().Y,
                    Svc.Targets.Target.GetPositionXZY().Z, radius));

                if (e.tether)
                {
                    displayObjects.Add(new DisplayObjectLine(Svc.Targets.Target.GetPositionXZY().X + e.offX,
                        Svc.Targets.Target.GetPositionXZY().Y + e.offY,
                        Svc.Targets.Target.GetPositionXZY().Z + e.offZ,
                        GetPlayerPositionXZY().X, GetPlayerPositionXZY().Y, GetPlayerPositionXZY().Z,
                        e.thicc, e.color));
                }
            }
            else if (e.refActorType == 0 && e.refActorName.Length > 0)
            {
                if (Profiler.Enabled) Profiler.MainTickActorTableScan.StartTick();
                foreach (var a in Svc.Objects)
                {
                    if ((e.refActorName == "*" || IsNameContainsValue(a, e.refActorName))
                            && (!e.onlyTargetable || GetIsTargetable(a)))
                    {
                        var aradius = radius;
                        if (e.includeHitbox) aradius += a.HitboxRadius;
                        draw.Add((a.GetPositionXZY().X, a.GetPositionXZY().Y, a.GetPositionXZY().Z, aradius));
                        if (e.tether)
                        {
                            displayObjects.Add(new DisplayObjectLine(a.GetPositionXZY().X + e.offX,
                                a.GetPositionXZY().Y + e.offY,
                                a.GetPositionXZY().Z + e.offZ,
                                GetPlayerPositionXZY().X, GetPlayerPositionXZY().Y, GetPlayerPositionXZY().Z,
                                e.thicc, e.color));
                        }
                    }
                }
                if (Profiler.Enabled) Profiler.MainTickActorTableScan.StopTick();
            }

        }
        else if (e.type == 2)
        {
            if (ShouldDraw(e.offX, GetPlayerPositionXZY().X, e.offY, GetPlayerPositionXZY().Y)
                || ShouldDraw(e.refX, GetPlayerPositionXZY().X, e.refY, GetPlayerPositionXZY().Y))
                displayObjects.Add(new DisplayObjectLine(e.refX, e.refY, e.refZ, e.offX, e.offY, e.offZ, e.thicc, e.color));
        }
        if (draw.Count == 0) return;
        foreach (var (x, y, z, r) in draw)
        {
            if (!ShouldDraw(x + e.offX, GetPlayerPositionXZY().X, y + e.offY, GetPlayerPositionXZY().Y)) continue;
            if (e.thicc > 0)
            {
                if (r > 0)
                {
                    displayObjects.Add(new DisplayObjectCircle(x + e.offX, y + e.offY, z + e.offZ, r, e.thicc, e.color));
                }
                else
                {
                    displayObjects.Add(new DisplayObjectDot(x + e.offX, y + e.offY, z + e.offZ, e.thicc, e.color));
                }
            }
            if (e.overlayText.Length > 0)
            {
                displayObjects.Add(new DisplayObjectText(x + e.offX, y + e.offY, z + e.offZ + e.overlayVOffset, e.overlayText, e.overlayBGColor, e.overlayTextColor));
            }
        }
    }

    internal bool IsLayoutVisible(Layout i)
    {
        if (!i.Enabled) return false;
        if (i.DisableInDuty && Svc.Condition[ConditionFlag.BoundByDuty]) return false;
        if (i.ZoneLockH.Count > 0 && !i.ZoneLockH.Contains(Svc.ClientState.TerritoryType)) return false;
        if (i.JobLock != 0 && !Bitmask.IsBitSet(i.JobLock, (int)Svc.ClientState.LocalPlayer.ClassJob.Id)) return false;
        if ((i.DCond == 1 || i.DCond == 3) && !Svc.Condition[ConditionFlag.InCombat]) return false;
        if ((i.DCond == 2 || i.DCond == 3) && !Svc.Condition[ConditionFlag.BoundByDuty]) return false;
        if (i.DCond == 4 && !(Svc.Condition[ConditionFlag.InCombat]
            || Svc.Condition[ConditionFlag.BoundByDuty])) return false;
        return true;
    }

    public bool ShouldDraw(float x1, float x2, float y1, float y2)
    {
        return ((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2)) < Config.maxdistance * Config.maxdistance;
    }

    public void Log(string s, bool tochat = false)
    {
        if (tochat)
        {
            Svc.Chat.Print("[Splatoon]" + s);
        }
        if (Config.dumplog)
        {
            try { PluginLog.Log(s); } catch (Exception) { }
        }
        var line = DateTimeOffset.Now.ToString() + ": " + s;
        for (var i = 0; i < LogStorage.Length; i++)
        {
            if (LogStorage[i] == null)
            {
                LogStorage[i] = line;
                return;
            }
        }
        for (var i = 1; i < LogStorage.Length; i++)
        {
            LogStorage[i - 1] = LogStorage[i];
        }
        LogStorage[LogStorage.Length - 1] = line;
    }

    internal bool GetIsTargetable(GameObject a)
    {
        if(a is Character)
        {
            return MemoryManager.GetIsTargetable_Character(a.Address) != 0;
        }
        else
        {
            return MemoryManager.GetIsTargetable_GameObject(a.Address) != 0;
        }
    }

    public void HandleChat()
    {

    }
}
