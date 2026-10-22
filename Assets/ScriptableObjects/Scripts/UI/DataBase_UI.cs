using System.Collections.Generic;
using UnityEngine;

public static class UIIds
{
    public const string TitleStill = "title_still";
    public const string DefaultBackGround = "default_background"; 
    public const string RailTrack = "rail_track";
    public const string TrainIcon = "train_icon";
    public const string StageMenuPanel = "stage_menu_panel";
    public const string StageButton = "stage_button";
    public const string UnitSlotBg = "unit_slot_bg";
    public const string UnitSlotFrame = "unit_slot_frame";
    public const string SelectBoxBg = "select_box_bg";
    public const string MenuButton = "menu_button";
    public const string SectionEventIcon = "section_event_icon";
    public const string SectionBattleIcon = "section_battle_icon";
}

[CreateAssetMenu(fileName = "DataBase_UI", menuName = "Scriptable Objects/UI/DataBase_UI")]
public class DataBase_UI : ScriptableObject
{
    public List<Data_UI> UIs;

    public Data_UI GetUI(string id)
    {
        return UIs.Find(u => u.id == id);
    }

    public bool HasUI(string id)
    {
        return UIs.Exists(u => u.id == id);
    }
}
