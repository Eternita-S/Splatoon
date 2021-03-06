using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Internal.Notifications;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Newtonsoft.Json;
using PInvoke;
using Splatoon.ConfigGui;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Splatoon
{
    // Master class
    unsafe partial class CGui:IDisposable
    {
        Dictionary<uint, string> ActionNames;
        Dictionary<uint, string> BuffNames;
        const float WidthLayout = 150f;
        internal static float WidthElement
        {
            get
            {
                return 130f.Scale();
            }
        }
        internal const float WidthCombo = 200f;
        internal readonly Splatoon p;
        public bool Open = false;
        string lname = "";
        string ename = "";
        string zlockf = "";
        bool zlockcur = false;
        int curEdit = -1;
        bool enableDeletion = false;
        bool enableDeletionElement = false;
        bool WasOpen = false;
        string jobFilter = "";
        float RightWidth = 0;
        internal static GCHandle imGuiPayloadPtr;

        public CGui(Splatoon p)
        {
            this.p = p;
            Svc.PluginInterface.UiBuilder.Draw += Draw;
            ActionNames = Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>().ToDictionary(x => x.RowId, x => $"{x.RowId} | {x.Name}");
            BuffNames = Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Status>().ToDictionary(x => x.RowId, x => $"{x.RowId} | {x.Name}");
        }

        public void Dispose()
        {
            if (imGuiPayloadPtr.IsAllocated)
            {
                imGuiPayloadPtr.Free();
            }
            Svc.PluginInterface.UiBuilder.Draw -= Draw;
        }

        internal static (IntPtr Ptr, uint Size) GetPayloadPtr(string s)
        {
            if (imGuiPayloadPtr.IsAllocated)
            {
                imGuiPayloadPtr.Free();
            }
            Svc.Chat.Print("Allocating ImGuiPayload");
            imGuiPayloadPtr = GCHandle.Alloc(s, GCHandleType.Pinned);
            return (imGuiPayloadPtr.AddrOfPinnedObject(), (uint)sizeof(ImGuiPayload));
        }
        
        void Draw()
        {
            if (p.s2wInfo != null) return;
            if (!Open) 
            { 
                if(WasOpen)
                {
                    p.Config.Save();
                    WasOpen = false;
                    Notify.Success("Configuration saved");
                    if(p.Config.verboselog) p.Log("Configuration saved");
                }
                return;
            }
            else
            {
                if (!WasOpen)
                {
                    p.Config.Backup();
                }
                if(p.s2wInfo == null && Svc.PluginInterface.UiBuilder.FrameCount % 600 == 0)
                {
                    p.Config.Save();
                    p.Log("Configuration autosaved");
                }
            }
            WasOpen = true;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(700, 200));
            var titleColored = false;
            var ctspan = TimeSpan.FromMilliseconds(Environment.TickCount64 - p.CombatStarted);
            var title = $"Splatoon v{p.loader.splatoonVersion} | {(p.Zones.TryGetValue(Svc.ClientState.TerritoryType, out var t) ? p.Zones[Svc.ClientState.TerritoryType].PlaceName.Value.Name : "Terr: " + Svc.ClientState.TerritoryType)} | {(p.CombatStarted == 0?"Not in combat":$"Combat: {ctspan.Minutes:D2}{(ctspan.Milliseconds < 500?":":" ")}{ctspan.Seconds:D2} ({(int)ctspan.TotalSeconds}.{(ctspan.Milliseconds / 100):D1}s)")} | Phase: {p.Phase} | Layouts: {p.LayoutAmount} | Elements: {p.ElementAmount} | {GetPlayerPositionXZY().X:F1}, {GetPlayerPositionXZY().Y:F1}###Splatoon";
            if (ImGui.Begin(title, ref Open))
            {
                if (ChlogGui.ChlogVersion > p.Config.ChlogReadVer)
                {
                    ImGuiEx.Text("You may not change configuration until you have read changelog and closed window.");
                    if (ImGui.Button("Open changelog now"))
                    {
                        p.ChangelogGui.openLoggedOut = true;
                    }
                }
                else
                {
                    var curCursor = ImGui.GetCursorPos();
                    ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - RightWidth);
                    RightWidth = ImGuiEx.Measure(delegate
                    {
                        if (p.MemoryManager.ErrorCode != 0)
                        {
                            ImGui.TextColored((Environment.TickCount % 1000 < 500 ? Colors.Red : Colors.Yellow).ToVector4(), "Failsafe mode");
                            ImGui.SameLine();
                        }
                        ImGui.SetNextItemWidth(100f);
                        if (ImGui.BeginCombo("##phaseSelector", $"Phase {p.Phase}"))
                        {
                            if (ImGui.Selectable("Phase 1 (doorboss)")) p.Phase = 1;
                            if (ImGui.Selectable("Phase 2 (post-doorboss)")) p.Phase = 2;
                            ImGuiEx.Text("Manual phase selection:");
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(30f);
                            ImGui.DragInt("##mPSel", ref p.Phase, 0.1f, 1, 9);
                            ImGui.EndCombo();
                        }
                    });
                    ImGui.SetCursorPos(curCursor);
                    
                    ImGui.BeginTabBar("SplatoonSettings");
                    if (ImGui.BeginTabItem("General settings"))
                    {
                        DisplayGeneralSettings();
                        ImGui.EndTabItem();
                    }
                    ImGui.PushStyleColor(ImGuiCol.Text, Colors.Green);
                    if (ImGui.BeginTabItem("Layouts"))
                    {
                        ImGui.PopStyleColor();
                        DislayLayouts();
                        ImGui.EndTabItem();
                    }
                    else
                    {
                        ImGui.PopStyleColor();
                    }

                    if (ImGui.BeginTabItem("Logger"))
                    {
                        DisplayLogger();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Debug"))
                    {
                        DisplayDebug();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Log"))
                    {
                        DisplayLog();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Dynamic"))
                    {
                        DisplayDynamicElements();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Profiling"))
                    {
                        DisplayProfiling();
                        ImGui.EndTabItem();
                    }
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.ParsedGold);
                    if (ImGui.BeginTabItem("Contribute"))
                    {
                        ImGui.PopStyleColor();
                        Contribute.Draw();
                        ImGui.EndTabItem();
                    }
                    else
                    {
                        ImGui.PopStyleColor();
                    }
                }
            }
            ImGui.PopStyleVar();
            if (titleColored)
            {
                ImGui.PopStyleColor(2);
            }
            ImGui.EndTabBar();
            ImGui.End();
        }

        private void HTTPExportToClipboard(Layout el)
        {
            var l = JsonConvert.DeserializeObject<Layout>(JsonConvert.SerializeObject(el));
            l.Enabled = true;
            foreach (var e in l.ElementsL) e.Enabled = true;
            var json = "~" + JsonConvert.SerializeObject(l, Formatting.None, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore });
            var jsonraw = "~" + JsonConvert.SerializeObject(l, Formatting.Indented, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore });
            var compressed = json.Compress();
            var base64 = json.ToBase64UrlSafe();
            ImGui.SetClipboardText(ImGui.GetIO().KeyAlt ? jsonraw : ImGui.GetIO().KeyCtrl ? HttpUtility.UrlEncode(json) : compressed.Length>base64.Length?base64:compressed);
        }

        private void HTTPExportToClipboard(Element el)
        {
            var l = JsonConvert.DeserializeObject<Element>(JsonConvert.SerializeObject(el)); ;
            l.Enabled = true;
            var json = JsonConvert.SerializeObject(l, Formatting.None, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore });
            var jsonraw = JsonConvert.SerializeObject(l, Formatting.Indented, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore });
            var compressed = json.Compress();
            var base64 = json.ToBase64UrlSafe();
            ImGui.SetClipboardText(ImGui.GetIO().KeyAlt ? jsonraw : ImGui.GetIO().KeyCtrl ? HttpUtility.UrlEncode(json) : compressed.Length > base64.Length ? base64 : compressed);
        }

        private void SetCursorTo(float refX, float refZ, float refY)
        {
            if (p.MemoryManager.WorldToScreen(new Vector3(refX, refZ, refY), out var screenPos))
            {
                var point = new POINT() { x = (int)screenPos.X, y = (int)screenPos.Y };
                //Chat.Print(point.X + "/" + point.Y);
                if (User32.ClientToScreen(Process.GetCurrentProcess().MainWindowHandle, ref point))
                {
                    //Chat.Print(point.X + "/" + point.Y);
                    User32.SetCursorPos(point.x, point.y);
                }
            }
        }
    }
}
