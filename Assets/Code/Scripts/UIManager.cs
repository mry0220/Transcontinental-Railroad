using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : MonoBehaviour
{
    //==== Inspector Config====
    [SerializeField] private DataBase_Unit unitDB;
    
    //==== References ====
    private UIDocument uiDocument;
    private UIInputHandler uIInputHandler;

    //==== Runtime UI State
    private VisualElement root;
    private VisualElement fuelBarFill;
    private Dictionary<string, VisualElement> unitIcons = new();
    private Dictionary<string, VisualElement> reviveFills = new();
    private Dictionary<string, Label> reviveLabels = new();

    private VisualElement routeBar;
    private VisualElement routeTrain;
    private readonly List<VisualElement> routeMarkers = new();
    private int routePassedCount = -1;

    private VisualElement reviveOverlay;
    private Label reviveCostLabel;

    private VisualElement resultOverlay;
    private Label resultHeaderLabel;

    private VisualElement reviveIcon;

    private VisualElement stageSelectScreen;
    private VisualElement stageListPanel;

    private VisualElement titleScreen;
    private Label titleTapLabel;
    private IVisualElementScheduledItem titleBlink;
    private Button menuButton;

    private VisualElement unitSelectScreen;
    private VisualElement trainImage;
    private Button[] unitSlotButtons = new Button[Loadout.SlotCount];
    private ScrollView trainList;
    private ScrollView unitList;
    private Button unitSelectConfirmButton;
    private VisualElement unitContainer;
    private readonly Dictionary<Item_Train, Button> trainCandidateButtons = new();
    private readonly Dictionary<Item_Unit, Button> unitCandidateButtons = new();


    public event System.Action<Item_Train> OnTrainChosen;
    public event System.Action<Item_Unit> OnUnitCandidateChosen;
    public event System.Action<int> OnUnitSlotChosen;
    public event System.Action OnUnitSelectConfirmed;
    public event System.Action OnUnitSelectBack;

    private VisualElement fuelSetScreen;
    private VisualElement fuelSetBarFill;
    private VisualElement fuelSetTrainImage;
    private Label fuelSetTapLabel;
    private IVisualElementScheduledItem fuelSetBlink;

    public event System.Action OnFuelSetTapped;

    public event System.Action OnTitleTapped;
    public event System.Action OnMenuRequested;

    public event System.Action<int> OnStageChosen; 

    public event System.Action OnReviveConfirmed;
    public event System.Action OnReviveCancelled;
    
    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("UIDocument not found!");
            return;
        }

        uIInputHandler = GetComponent<UIInputHandler>();
        if(uIInputHandler == null)
        {
            Debug.LogError("UIInputHandler not Null");
            return;
        }
    }

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        if(root == null)
        {
            Debug.LogError("rootVisualElement is null!");
            return;
        }
        if(unitDB.units != null && unitDB.units.Count > 0)
        {
            SetupUI();
        }
        SetupRouteBar();
        SetupTitleScreen();
        SetupStageSelectScreen();
        SetupUnitSelectScreen();
        SetupFuelSetScreen();
        SetupMenuButton();
        SetupRevivePopup();
        SetupResultScreen();
        uIInputHandler.InitializeInputHadler(root);
    }

    private void SetupRevivePopup()
    {
        reviveOverlay = new VisualElement();
        reviveOverlay.AddToClassList("revive-overlay");

        var panel = new VisualElement();
        panel.AddToClassList("revive-panel");

        reviveIcon = new VisualElement();
        reviveIcon.AddToClassList("revive-icon");

        reviveCostLabel = new Label();
        reviveCostLabel.AddToClassList("revive-cost-label");

        var buttonRow = new VisualElement();
        buttonRow.AddToClassList("revive-button-row");

        var confirmButton = new Button(() => OnReviveConfirmed?.Invoke()) { text = "Revive" };
        confirmButton.AddToClassList("revive-confirm-button");

        var cancelButton = new Button(() => OnReviveCancelled?.Invoke()) { text = "Cancel" };
        cancelButton.AddToClassList("revive-cancel-button");

        panel.Add(confirmButton);
        panel.Add(cancelButton);

        panel.Add(reviveIcon);
        panel.Add(reviveCostLabel);
        panel.Add(buttonRow);
        reviveOverlay.Add(panel);

        reviveOverlay.style.display = DisplayStyle.None;
        root.Add(reviveOverlay);
    }

    private void SetupRouteBar()
    {
        routeBar = new VisualElement();
        routeBar.AddToClassList("route-bar");
        routeBar.pickingMode = PickingMode.Ignore;

        var line = new VisualElement();
        line.AddToClassList("route-line");
        line.pickingMode = PickingMode.Ignore;
        routeBar.Add(line);

        routeTrain = new VisualElement();
        routeTrain.AddToClassList("route-train");
        routeTrain.pickingMode = PickingMode.Ignore;
        routeBar.Add(routeTrain);

        routeBar.style.display = DisplayStyle.None;
        root.Add(routeBar);
    }

    private void SetupFuelSetScreen()
    {
        fuelSetScreen = new VisualElement();
        fuelSetScreen.AddToClassList("fuelset-screen");

        var barContainer = new VisualElement();
        barContainer.AddToClassList("fs-fuel-container");

        var wrapper = new VisualElement();
        var background = new VisualElement();
        fuelSetBarFill = new VisualElement();

        wrapper.AddToClassList("fuel-Wrapper");
        background.AddToClassList("fuel-BackGround");
        fuelSetBarFill.AddToClassList("fuel-Fill");

        background.Add(fuelSetBarFill);
        wrapper.Add(background);
        barContainer.Add(wrapper);
        fuelSetScreen.Add(barContainer);

        fuelSetTrainImage = new VisualElement();
        fuelSetTrainImage.AddToClassList("fs-train-image");
        fuelSetScreen.Add(fuelSetTrainImage);

        fuelSetTapLabel = new Label("TAP");
        fuelSetTapLabel.AddToClassList("fs-tap");
        fuelSetTapLabel.style.display = DisplayStyle.None;
        fuelSetScreen.Add(fuelSetTapLabel);

        fuelSetBlink = fuelSetTapLabel.schedule
            .Execute(() => fuelSetTapLabel.ToggleInClassList("fs-tap-dim"))
            .Every(700);
        fuelSetBlink.Pause();

        fuelSetScreen.RegisterCallback<PointerDownEvent>(_ => OnFuelSetTapped?.Invoke());

        fuelSetScreen.style.display = DisplayStyle.None;
        root.Add(fuelSetScreen);
    }

    private void SetupUnitSelectScreen()
    {
        unitSelectScreen = new VisualElement();
        unitSelectScreen.AddToClassList("unit-select-screen");

        //上段:出撃Trainと出撃Unit
        var top = new VisualElement();
        top.AddToClassList("us-top");

        var trainPanel = new VisualElement();
        trainPanel.AddToClassList("us-train-panel");
        trainImage = new VisualElement();
        trainImage.AddToClassList("us-train-image");
        trainPanel.Add(trainImage);
        top.Add(trainPanel);

        var slotRow = new VisualElement();
        slotRow.AddToClassList("us-slot-row");
        for(int i = 0;i<Loadout.SlotCount; i++)
        {
            int slotIndex = i;
            var slot = new Button(() => OnUnitSlotChosen?.Invoke(slotIndex));
            slot.AddToClassList("us-unit-slot");
            unitSlotButtons[i] = slot;
            slotRow.Add(slot);
        }

        top.Add(slotRow);
        unitSelectScreen.Add(top);


        var bottom = new VisualElement();
        bottom.AddToClassList("us-bottom");
        
        trainList = new ScrollView(ScrollViewMode.Horizontal);
        trainList.AddToClassList("us-train-list");
        bottom.Add(trainList);

        unitList = new ScrollView(ScrollViewMode.Horizontal);
        unitList.AddToClassList("us-unit-list");
        bottom.Add(unitList);
        unitSelectScreen.Add(bottom);

        var backButton = new Button(() => OnUnitSelectBack?.Invoke()) { text = "BACK" };
        backButton.AddToClassList("us-back-button");
        unitSelectScreen.Add(backButton);

        unitSelectConfirmButton = new Button(() => OnUnitSelectConfirmed?.Invoke()) { text = "START" };
        unitSelectConfirmButton.AddToClassList("us-confirm-button");
        unitSelectScreen.Add(unitSelectConfirmButton);

        unitSelectScreen.style.display = DisplayStyle.None;
        root.Add(unitSelectScreen);
    }

    public void SetUnitSelectCandidates(List<Item_Train> trains)
    {
        trainList.Clear();
        trainCandidateButtons.Clear();
        if(trains != null)
        {
            foreach(var train in trains)
            {
                if (train == null) continue;

                var button = new Button(() => OnTrainChosen?.Invoke(train));
                button.AddToClassList("us-candidate");
                SetIcon(button,train.icon);
                trainList.Add(button);
                trainCandidateButtons[train] = button;
            }
        }

        unitList.Clear();
        unitCandidateButtons.Clear();
        if(unitDB != null && unitDB.units != null)
        {
            foreach(var unit in unitDB.units)
            {
                if (unit == null) continue;

                var button = new Button(() => OnUnitCandidateChosen?.Invoke(unit));
                button.AddToClassList("us-candidate");
                SetIcon(button, unit.icon);
                unitList.Add(button);
                unitCandidateButtons[unit] = button;
            }
             
        }
    }

    /// <summary>
    /// Operation突入時に、Sectionの種類に合わせ印を作りなおす
    /// </summary>
    public void BuildRouteBar(List<SectionType> sectionTypes)
    {
        foreach (var marker in routeMarkers) marker.RemoveFromHierarchy();
        routeMarkers.Clear();
        routePassedCount = -1;

        int count = sectionTypes != null ? sectionTypes.Count : 0;
        for(int i = 0;i < count;i++)
        {
            var marker = new VisualElement();
            marker.AddToClassList("route-marker");
            marker.AddToClassList(sectionTypes[i] switch
            {
                SectionType.Battle => "route-marker-battle",
                SectionType.Event => "route-marker-event",
                _ => "route-marker-goal"
            });
            marker.pickingMode = PickingMode.Ignore;
            marker.style.left = new Length((i + 1) * 100f / count,LengthUnit.Percent);

            routeBar.Add(marker);
            routeMarkers.Add(marker);
        }

        routeTrain.BringToFront();
        UpdateRouteProgress(0, count, 0f);
    }

    /// <summary>
    /// currentIndexのSectionへ向かう道のりのうち、LegProgressだけ進んだ位置に列車を置く
    /// </summary>
    /// <param name="currentIndex"></param>
    /// <param name="sectionCount"></param>
    /// <param name="legProgress"></param>
    public void UpdateRouteProgress(int currentIndex,int sectionCount,float legProgress)
    {
        if (routeBar == null || sectionCount <= 0) return;

        routeTrain.style.left = new Length((currentIndex + legProgress) * 100f / sectionCount,LengthUnit.Percent);

        int passed = currentIndex + (legProgress >= 1f ? 1 : 0);
        if (passed == routePassedCount) return;

        routePassedCount = passed;
        for(int i = 0;i<routeMarkers.Count;i++)
        {
            routeMarkers[i].EnableInClassList("route-marker-passed", i < passed);
        }
    }

    public void SetRouteBarVisible(bool visible)
    {
        if (routeBar == null) return;
        routeBar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void RefreshUnitSelect(Loadout loadout)
    {
        SetIcon(trainImage, loadout.Train != null ? loadout.Train.icon : null);

        for(int i = 0;i<unitSlotButtons.Length;i++)
        {
            var unit = loadout.slots[i];
            SetIcon(unitSlotButtons[i], unit != null ? unit.icon : null);
        }

        foreach(var pair in trainCandidateButtons)
        {
            pair.Value.EnableInClassList("us-candidate-selected", pair.Key == loadout.Train);
        }
        foreach(var pair in unitCandidateButtons)
        {
            pair.Value.EnableInClassList("us-candidate-selected", loadout.Contains(pair.Key));
        }
        unitSelectConfirmButton.SetEnabled(loadout.IsValid);
    }

    public void SetFuelSetTrain(Item_Train train)
    {
        SetIcon(fuelSetTrainImage, train != null ? train.icon : null);
    }

    public void SetFuelSetReady(bool ready)
    {
        if (fuelSetTapLabel == null) return;

        fuelSetTapLabel.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
        if (ready) fuelSetBlink?.Resume();
        else fuelSetBlink?.Pause();
    }

    public  void HidePrepScreens()
    {
        stageSelectScreen.style.display = DisplayStyle.None;
        unitSelectScreen.style.display = DisplayStyle.None;
        fuelSetScreen.style.display = DisplayStyle.None;
        SetFuelSetReady(false);
    }

    private static void SetIcon(VisualElement element,Sprite sprite)
    {
        element.style.backgroundImage = sprite != null
            ? new StyleBackground(sprite)
            : new StyleBackground(StyleKeyword.None);
    }


    private void SetupResultScreen()
    {
        resultOverlay = new VisualElement();
        resultOverlay.AddToClassList("result-overlay");

        var panel = new VisualElement();
        panel.AddToClassList("result-panel");

        resultHeaderLabel = new Label();
        resultHeaderLabel.AddToClassList("result-header");

        var hintLabel = new Label("タップでリトライ");
        hintLabel.AddToClassList("result-hint");

        panel.Add(resultHeaderLabel);
        panel.Add(hintLabel);
        resultOverlay.Add(panel);

        resultOverlay.style.display = DisplayStyle.None;
        root.Add(resultOverlay);
    }

    private void SetupTitleScreen()
    {
        titleScreen = new VisualElement();
        titleScreen.AddToClassList("title-screen");

        var still = new VisualElement();
        still.AddToClassList("title-still");

        var logo = new VisualElement();
        logo.AddToClassList("title-logo");

        titleTapLabel = new Label("TAP TO START");
        titleTapLabel.AddToClassList("title-tap");

        titleScreen.Add(still);
        titleScreen.Add(logo);
        titleScreen.Add(titleTapLabel);

        titleScreen.RegisterCallback<PointerDownEvent>(_ => OnTitleTapped?.Invoke());

        titleBlink = titleTapLabel.schedule
            .Execute(() => titleTapLabel.ToggleInClassList("title-tap-dim"))
            .Every(700);
        titleBlink.Pause();

        titleScreen.style.display = DisplayStyle.None;
        root.Add(titleScreen);
    }

    private void SetupMenuButton()
    {
        menuButton = new Button(() => OnMenuRequested?.Invoke());
        menuButton.AddToClassList("menu-button");

        for(int i =0;i < 3;i++)
        {
            var line = new VisualElement();
            line.AddToClassList("menu-button-line");
            line.pickingMode = PickingMode.Ignore;
            menuButton.Add(line);
        }

        root.Add(menuButton);
    }

    public void ShowTitleScreen(bool visible)
    {
        if (titleScreen == null) return;

        titleScreen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (visible) titleBlink?.Resume();
        else titleBlink?.Pause();
    }



    private void SetupStageSelectScreen()
    {
        stageSelectScreen = new VisualElement();
        stageSelectScreen.AddToClassList("stage-select-screen");

        var characterFrame = new VisualElement();
        characterFrame.AddToClassList("stage-select-character");
        stageSelectScreen.Add(characterFrame);

        stageListPanel = new VisualElement();
        stageListPanel.AddToClassList("stage-select-list-panel");
        stageSelectScreen.Add(stageListPanel);

        stageSelectScreen.style.display = DisplayStyle.None;
        root.Add(stageSelectScreen);
    }

    private void SetupUI()
    {
        SetupUIFuel();
        SetupUIUnit();
    }
    private void SetupUIFuel()
    {
        var container = root.Q<VisualElement>("fuel-container");

        Debug.Log($"setting up fuel");

        var fuelBarWrapper = new VisualElement();
        var fuelBarBackGround = new VisualElement();
        fuelBarFill = new VisualElement();

        fuelBarWrapper.AddToClassList("fuel-Wrapper");
        fuelBarBackGround.AddToClassList("fuel-BackGround");
        fuelBarFill.AddToClassList("fuel-Fill");
        fuelBarBackGround.Add(fuelBarFill);
        fuelBarWrapper.Add(fuelBarBackGround);
        container.Add(fuelBarWrapper);
    }
    public void HandleUnitDeployed(string itemId,GameObject unit)
    {
        if(unitIcons.TryGetValue(itemId,out var icon))
        {
            icon.AddToClassList("unit-icon-deployed");
        }
    }
    public void HandleUnitReturned(string itemId,GameObject unit)
    {
        if(unitIcons.TryGetValue(itemId,out var icon))
        {
            icon.RemoveFromClassList("unit-icon-deployed");
        }
    }

    public void HandleUnitDied(string itemId)
    {
        if(unitIcons.TryGetValue(itemId,out var icon))
        {
            icon.RemoveFromClassList("unit-icon-deployed");
            icon.AddToClassList("unit-icon-dead");
        }
    }

    public void HandleUnitRevived(string itemId)
    {
        if(unitIcons.TryGetValue(itemId,out var icon))
        {
            icon.RemoveFromClassList("unit-icon-dead");
        }
        SetReviveVisible(itemId, false);
    }

    public void HandleReviveStarted(string itemId)
    {
        SetReviveVisible(itemId,true);
    }

    public void HandleReviveProgress(string itemId,float remaining,float progress)
    {
        if(reviveFills.TryGetValue(itemId,out var fill))
        {
            fill.style.height = new Length(progress * 100f, LengthUnit.Percent);
        }
        if(reviveLabels.TryGetValue(itemId,out var label))
        {
            label.text = remaining > 0f ? remaining.ToString("F1") : "READY";
        }
    }

    private void SetReviveVisible(string itemId,bool visible)
    {
        var display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if(reviveFills.TryGetValue(itemId,out var fill))
        {
            fill.style.display = display;
            if (visible) fill.style.height = new Length(0f, LengthUnit.Percent);
        }
        if(reviveLabels.TryGetValue(itemId,out var timeLabel))
        {
            timeLabel.style.display = display;
        }
    }

    

    public void ShowRevivePopup(Sprite icon,int fuelCost)
    {
        SetIcon(reviveIcon, icon);
        reviveCostLabel.text = $"必要燃料: {fuelCost}";
        reviveOverlay.style.display = DisplayStyle.Flex;
    }

    public void ShowResult(RunOutcome outcome)
    {
        bool cleared = outcome == RunOutcome.Cleared;
        resultHeaderLabel.text = cleared ? "complete" : "failed";
        resultHeaderLabel.EnableInClassList("result-header-cleared", cleared);
        resultHeaderLabel.EnableInClassList("result-header-failed", !cleared);
        resultOverlay.style.display = DisplayStyle.Flex;
    }

    public void HideResult()
    {
        resultOverlay.style.display = DisplayStyle.None;
    }

    public void ResetUnitIcons()
    {
        foreach(var icon in unitIcons.Values)
        {
            icon.RemoveFromClassList("unit-icon-deployed");
            icon.RemoveFromClassList("unit-icon-dead");
        }
        foreach(var itemId in reviveFills.Keys)
        {
            SetReviveVisible(itemId, false);
        }
        HideRevivePopup();
    }


    public void HideRevivePopup()
    {
        reviveOverlay.style.display = DisplayStyle.None;
    }

    private void SetupUIUnit()
    {
        unitContainer = root.Q<VisualElement>("unit-container");
    }

    private void OnDisable()
    {
        uIInputHandler.DeinitializeInputHandler();
    }

    public void UpdateFuelUI(int currentFuel,int maxFuel)
    {
        float percent = maxFuel > 0 ? (float)currentFuel / maxFuel * 100f : 0f;
        var width = new Length(percent, LengthUnit.Percent);

        if (fuelBarFill != null) fuelBarFill.style.width = width;
        if (fuelBarFill != null) fuelSetBarFill.style.width = width;
    }

    public void SetStageList(List<StageData> stages)
    {
        stageListPanel.Clear();
        if (stages == null) return;
        
        for(int i = 0;i < stages.Count;i++)
        {
            int index = i;
            string label = stages[i] != null ? stages[i].stageName : "(未設定)";

            var button = new Button(() => OnStageChosen?.Invoke(index)) { text = label };
            stageListPanel.Add(button);
        }
    }

    public void ShowPrepScreen(PrepPhase phase)
    {
        stageSelectScreen.style.display =
            phase == PrepPhase.StageSelect ? DisplayStyle.Flex : DisplayStyle.None;
        unitSelectScreen.style.display =
            phase == PrepPhase.UnitSelect ? DisplayStyle.Flex : DisplayStyle.None;
        fuelSetScreen.style.display =
            phase == PrepPhase.FuelSet ? DisplayStyle.Flex : DisplayStyle.None;

        SetFuelSetReady(false);
    }

    public void BuildUnitIcons(List<Item_Unit> units)
    {
        unitContainer.Clear();
        unitIcons.Clear();
        reviveFills.Clear();
        reviveLabels.Clear();

        foreach(var unitData in units)
        {
            var icon = new VisualElement();
            icon.AddToClassList("unit-icon");
            icon.style.backgroundImage = new StyleBackground(unitData.icon);
            icon.userData = unitData.id;

            var fill = new VisualElement();
            fill.AddToClassList("unit-icon-revive-fill");
            fill.pickingMode = PickingMode.Ignore;
            fill.style.display = DisplayStyle.None;
            icon.Add(fill);

            var timeLabel = new Label();
            timeLabel.AddToClassList("unit-icon-revive-label");
            timeLabel.pickingMode = PickingMode.Ignore;
            timeLabel.style.display = DisplayStyle.None;
            icon.Add(timeLabel);

            unitContainer.Add(icon);

            reviveFills[unitData.id] = fill;
            reviveLabels[unitData.id] = timeLabel;
            unitIcons[unitData.id] = icon;
        }
    }

}
