using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Splatoon.ConfigGui.CGuiLayouts.LayoutDrawHeader
{
    internal static class DistanceLimit
    {
        internal static void DrawDistanceLimit(this Layout layout)
        {
            if (layout.UseDistanceLimit)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(150f);
                ImGui.SameLine();
                ImGui.Combo("##dlimittype", ref layout.DistanceLimitType, new string[] { "Distance to current target", "Distance to element" }, 2);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50f);
                ImGui.DragFloat("##dlimit1", ref layout.MinDistance, 0.1f);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Including this value");
                ImGui.SameLine();
                ImGuiEx.Text("-");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50f);
                ImGui.DragFloat("##dlimit2", ref layout.MaxDistance, 0.1f);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Excluding this value");
                if (layout.DistanceLimitType == 0)
                {
                    ImGuiEx.TextV("Hitbox:");
                    ImGui.SameLine();
                    ImGui.Checkbox("+my##", ref layout.DistanceLimitMyHitbox);
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add my hitbox value to distance calculation");
                    ImGui.SameLine();
                    ImGui.Checkbox("+target##", ref layout.DistanceLimitTargetHitbox);
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add target's hitbox value to distance calculation");
                }
            }
        }
    }
}
