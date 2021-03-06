using Dalamud;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Splatoon
{
    [Serializable]
    public class InternationalString
    {
        [NonSerialized] internal string guid = Guid.NewGuid().ToString();
        [DefaultValue("")] public string En = string.Empty;
        [DefaultValue("")] public string Jp = string.Empty;
        [DefaultValue("")] public string De = string.Empty;
        [DefaultValue("")] public string Fr = string.Empty;
        [DefaultValue("")] public string Other = string.Empty;

        public string Get(string Default = "")
        {
            if (Svc.Data.Language == ClientLanguage.English) return this.En == string.Empty ? Default : this.En;
            if (Svc.Data.Language == ClientLanguage.Japanese) return this.Jp == string.Empty ? Default : this.Jp;
            if (Svc.Data.Language == ClientLanguage.German) return this.De == string.Empty ? Default : this.De;
            if (Svc.Data.Language == ClientLanguage.French) return this.Fr == string.Empty ? Default : this.Fr;
            return Other;
        }

        internal ref string CurrentLangString
        {
            get
            {
                if (Svc.Data.Language == ClientLanguage.English)
                {
                    return ref En;
                }
                else if(Svc.Data.Language == ClientLanguage.Japanese)
                {
                    return ref Jp;
                }
                else if(Svc.Data.Language == ClientLanguage.German)
                {
                    return ref De;
                }
                else if(Svc.Data.Language == ClientLanguage.French)
                {
                    return ref Fr;
                }
                else
                {
                    return ref Other;
                }
            }
        }

        public void ImGuiEdit(ref string DefaultValue, string helpMessage = null)
        {
            if (ImGui.BeginCombo($"##{guid}", this.Get(DefaultValue)))
            {
                ImGuiEx.ImGuiLineCentered($"line{guid}", delegate
                {
                    ImGuiEx.Text("International string");
                });
                EditLangSpecificString(ClientLanguage.English, ref this.En);
                EditLangSpecificString(ClientLanguage.Japanese, ref this.Jp);
                EditLangSpecificString(ClientLanguage.French, ref this.Fr);
                EditLangSpecificString(ClientLanguage.German, ref this.De);
                if(!Svc.Data.Language.EqualsAny(ClientLanguage.English, ClientLanguage.Japanese, ClientLanguage.German, ClientLanguage.French))
                {
                    EditLangSpecificString(Svc.Data.Language, ref this.Other);
                }
                
                SImGuiEx.SizedText("Default:", 100);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(300f);
                ImGui.InputText($"##{guid}default", ref DefaultValue, 1000);
                ImGuiComponents.HelpMarker("Default value will be applied when language-specific is missing.");
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                if (!helpMessage.IsNullOrEmpty())
                {
                    ImGuiEx.Text(helpMessage + "\n");
                }
                ImGuiEx.Text(ImGuiColors.DalamudGrey, "International string\nFor your current language value is:");
                ImGuiEx.Text(this.Get(DefaultValue));
                ImGui.EndTooltip();

            }
        }

        public bool IsEmpty()
        {
            return this.En.IsNullOrEmpty() && this.Jp.IsNullOrEmpty() && this.De.IsNullOrEmpty() && this.Fr.IsNullOrEmpty();
        }

        void EditLangSpecificString(ClientLanguage language, ref string str)
        {
            var col = false;
            if (str == string.Empty)
            {
                col = true;
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey3);
            }
            else if (Svc.Data.Language == language)
            {
                col = true;
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.ParsedGreen);
            }
            SImGuiEx.SizedText($"{language}:", 100);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(300f);
            ImGui.InputText($"##{guid}{language}", ref str, 1000);
            if (col)
            {
                ImGui.PopStyleColor();
            }
        }
    }
}
