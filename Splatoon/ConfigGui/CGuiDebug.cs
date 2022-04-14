﻿using Dalamud.Interface.Internal.Notifications;

namespace Splatoon
{
    partial class CGui
    {
        bool autoscrollLog = true;
        float s2wx, s2wy, s2wz, s2wrx, s2wry;
        bool s2wb = false;

        void DisplayDebug()
        {
            ImGui.BeginChild("##splatoonmaindbg");
            ImGui.Checkbox("Disable line fix", ref p.DisableLineFix);
            var t = Environment.TickCount64 - p.CombatStarted;
            ImGui.Text("CombatStarted = " + t);
            ImGui.SetNextItemWidth(60f);
            ImGui.DragInt($"Message concurrency", ref p.dequeueConcurrency, float.Epsilon);
            ImGui.Separator();
            if (Svc.ClientState.LocalPlayer != null)
            {
                var mypos = GetPlayerPositionXZY();
                ImGui.Text("My pos XYZ: \n" + mypos.X + "\n" + mypos.Y + "\n" + mypos.Z);
                ImGui.Text("Rotation: " + Svc.ClientState.LocalPlayer.Rotation);
                var tar = Svc.Targets.Target;
                if (tar != null)
                {
                    ImGui.Text("Target pos XYZ: \n" + tar.GetPositionXZY().X + "\n" + tar.GetPositionXZY().Y + "\n" + tar.GetPositionXZY().Z);
                    ImGui.Text("Rotation: " + tar.Rotation.ToString());
                    ImGui.Text("Hitbox: " + tar.HitboxRadius.ToString());
                }
            }
            ImGui.Separator();
            ImGui.Text("Query WorldToScreen");
            ImGui.PushItemWidth(50f);
            ImGui.DragFloat("##qs2wx", ref s2wx);
            ImGui.SameLine();
            ImGui.DragFloat("##qs2wy", ref s2wy);
            ImGui.SameLine();
            ImGui.DragFloat("##qs2wz", ref s2wz);
            ImGui.PopItemWidth();
            if (ImGui.Button("To my pos"))
            {
                var mypos = GetPlayerPositionXZY();
                s2wx = mypos.X;
                s2wy = mypos.Y;
                s2wz = mypos.Z;
            }
            ImGui.SameLine();
            if (ImGui.Button("Query"))
            {
                Safe(delegate
                {
                    s2wb = Svc.GameGui.WorldToScreen(new Vector3(s2wx, s2wz, s2wy), out Vector2 pos);
                    s2wrx = pos.X;
                    s2wry = pos.Y;
                });
            }
            ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(s2wb ? Colors.Green : Colors.Red), "X:" + s2wrx + "\nY:" + s2wry);
            ImGui.Separator();
            ImGui.Text("Camera angle X:" + p.CamAngleX);
            ImGui.Text("Camera angle Y:" + p.CamAngleY);
            ImGui.Separator();
            ImGui.Text("Object table:");
            ImGui.Text("Name");
            ImGui.SameLine();
            ImGui.SetCursorPosX(200f);
            ImGui.Text("Object ID");
            ImGui.SameLine();
            ImGui.SetCursorPosX(300f);
            ImGui.Text($"Data ID");
            ImGui.SameLine();
            ImGui.SetCursorPosX(400f);
            ImGui.Text($"Targetable");
            ImGui.SameLine();
            ImGui.SetCursorPosX(500f);
            ImGui.Text($"Visible");
            ImGui.SameLine();
            ImGui.SetCursorPosX(600f);
            ImGui.Text($"Model ID");
            foreach (var a in Svc.Objects)
            {
                Safe(delegate
                {
                    ImGui.Text(a.Name.ToString());
                    ImGui.SameLine();
                    ImGui.SetCursorPosX(200f);
                    ImGui.Text($"{a.ObjectId:X8}");
                    ImGui.SameLine();
                    ImGui.SetCursorPosX(300f);
                    ImGui.Text($"{a.DataId:X8}");
                    ImGui.SameLine();
                    ImGui.SetCursorPosX(400f);
                    ImGui.Text($"{p.MemoryManager.GetIsTargetable((a is Character ch) ? ch : a)}");
                    ImGui.SameLine();
                    ImGui.SetCursorPosX(500f);
                    ImGui.Text($"{((a is Character chr) ? p.MemoryManager.GetIsVisible(chr) : "Not a char")}");
                    ImGui.SameLine();
                    ImGui.SetCursorPosX(600f);
                    ImGui.Text(a is Character chr2 ? $"{p.MemoryManager.GetModelId(chr2):X8}" : "Not a char");
                });
            }
            ImGui.EndChild();
        }
    }
}
