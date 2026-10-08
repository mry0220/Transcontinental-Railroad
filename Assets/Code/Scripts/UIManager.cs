using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using UnityEngine;
using UnityEngine.iOS;
using UnityEngine.UIElements;

public class UIManager : MonoBehaviour
{
    #region InspectorConfig
    //==== Inspector Config====
    [SerializeField] private DataBase_Unit unitDB;

    #endregion

    #region References
    //==== References ====
    private UIDocument uiDocument;
    private UIInputHandler uIInputHandler;
    private VisualElement root;
    #endregion

    #region Events

    public event System.Action<Item_Train> OnTrainChosen;
    public event System.Action<Item_Unit> OnUnitCandidateChosen;
    public event System.Action<int> OnUnitSlotChosen;
    public event System.Action OnUnitSelectConfirmed;
    public event System.Action OnUnitSelectBack;
    public event System.Action OnRamRequested;
    public event System.Action OnReturnAllRequested;

    public event System.Action OnFuelSetTapped;

    public event System.Action OnTitleTapped;
    public event System.Action OnMenuRequested;

    public event System.Action<int> OnStageChosen;

    public event System.Action OnReviveConfirmed;
    public event System.Action OnReviveCancelled;

    public event System.Action OnMenuResumeResuested;
    public event System.Action OnMenuReturnRequested;
    public event System.Action<int> OnEventChoiceChosen;

    #endregion

    #region UnityLifecycle
    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("UIDocument not found!");
            return;
        }

        uIInputHandler = GetComponent<UIInputHandler>();
        if (uIInputHandler == null)
        {
            Debug.LogError("UIInputHandler not Null");
            return;
        }
    }
    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        if (root == null)
        {
            Debug.LogError("rootVisualElement is null!");
            return;
        }

        SetupUI();

        SetupRouteBar();
        SetupTitleScreen();
        SetupStageSelectScreen();
        SetupUnitSelectScreen();
        SetupFuelSetScreen();
        SetupBattleControls();
        SetupServantResultScreen();
        SetupEventPopup();
        SetupMenuButton();
        SetupRevivePopup();
        SetupResultScreens();
        SetupMenuOverlay();
        uIInputHandler.InitializeInputHadler(root);
    }
    private void OnDisable()
    {
        uIInputHandler.DeinitializeInputHandler();
    }

    #endregion

    #region Common

    private Button menuButton;

    private VisualElement menuOverlay;
    private Button menuReturnButton;
    public bool IsMenuVisible => menuOverlay != null && menuOverlay.style.display == DisplayStyle.Flex;

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
    private void SetupUIUnit()
    {
        unitContainer = root.Q<VisualElement>("unit-container");
    }
    public void UpdateFuelUI(int currentFuel, int maxFuel)
    {
        float percent = maxFuel > 0 ? (float)currentFuel / maxFuel * 100f : 0f;
        var width = new Length(percent, LengthUnit.Percent);

        if (fuelBarFill != null) fuelBarFill.style.width = width;
        if (fuelSetBarFill != null) fuelSetBarFill.style.width = width;
    }
    private static void SetIcon(VisualElement element, Sprite sprite)
    {
        element.style.backgroundImage = sprite != null
            ? new StyleBackground(sprite)
            : new StyleBackground(StyleKeyword.None);
    }
    private void SetupMenuButton()
    {
        menuButton = new Button(() => OnMenuRequested?.Invoke());
        menuButton.AddToClassList("menu-button");

        for (int i = 0; i < 3; i++)
        {
            var line = new VisualElement();
            line.AddToClassList("menu-button-line");
            line.pickingMode = PickingMode.Ignore;
            menuButton.Add(line);
        }

        root.Add(menuButton);
    }

    private void SetupMenuOverlay()
    {
        menuOverlay = new VisualElement();
        menuOverlay.AddToClassList("menu-overlay");

        var panel = new VisualElement();
        panel.AddToClassList("menu-panel");

        var header = new Label("MENU");
        header.AddToClassList("menu-header");

        var resumeButton = new Button(() => OnMenuResumeResuested?.Invoke()) { text = "再開" };
        resumeButton.AddToClassList("menu-item-button");

        menuReturnButton = new Button(() => OnMenuReturnRequested?.Invoke());
        menuReturnButton.AddToClassList("menu-item-button");

        panel.Add(header);
        panel.Add(resumeButton);
        panel.Add(menuReturnButton);
        menuOverlay.Add(panel);

        menuOverlay.style.display = DisplayStyle.None;
        root.Add(menuOverlay);
    }

    public void ShowMenu(bool visible,string returnLabel)
    {
        if (menuOverlay == null) return;

        bool hasReturn = !string.IsNullOrEmpty(returnLabel);
        menuReturnButton.style.display = hasReturn ? DisplayStyle.Flex : DisplayStyle.None;
        if (hasReturn) menuReturnButton.text = returnLabel;

        menuOverlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    #endregion

    #region TitleScreen
    private VisualElement titleScreen;
    private Label titleTapLabel;
    private IVisualElementScheduledItem titleBlink;
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
    public void ShowTitleScreen(bool visible)
    {
        if (titleScreen == null) return;

        titleScreen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (visible) titleBlink?.Resume();
        else titleBlink?.Pause();
    }

    #endregion

    #region PrepScreens
    public void ShowPrepScreen(PrepPhase phase)
    {
        stageSelectScreen.style.display =
            phase == PrepPhase.StageSelect ? DisplayStyle.Flex : DisplayStyle.None;
        unitSelectScreen.style.display =
            phase == PrepPhase.UnitSelect ? DisplayStyle.Flex : DisplayStyle.None;
        fuelSetScreen.style.display =
            phase == PrepPhase.FuelSet ? DisplayStyle.Flex : DisplayStyle.None;

        SetFuelSetStatus(FuelSetStatus.None);
    }
    public void HidePrepScreens()
    {
        HideStatusPopup();
        stageSelectScreen.style.display = DisplayStyle.None;
        unitSelectScreen.style.display = DisplayStyle.None;
        fuelSetScreen.style.display = DisplayStyle.None;
        SetFuelSetStatus(FuelSetStatus.None);
    }

    #region StageSelect

    private VisualElement stageSelectScreen;
    private VisualElement stageListContainer;
    private const int StageSlotCount = 6;
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

        var header = new Label("StageMenu");
        header.AddToClassList("stage-select-header");
        stageListPanel.Add(header);

        stageListContainer = new VisualElement();
        stageListContainer.AddToClassList("stage-select-list");
        stageListPanel.Add(stageListContainer);


        stageSelectScreen.style.display = DisplayStyle.None;
        root.Add(stageSelectScreen);
    }
    public void SetStageList(List<StageData> stages)
    {
        stageListContainer.Clear();
        int count = stages != null ? stages.Count : 0;

        for(int i =0;i<Mathf.Max(count,StageSlotCount);i++)
        {
            if(i<count)
            {
                int index = i;
                var stage = stages[i];
                string label = stage != null
                    ? $"{stage.stageName}\nSection{stage.SectionCount} / 距離　約{stage.RoughTotalDistance}"
                    : "(未設定)";

                var button = new Button(() => OnStageChosen?.Invoke(index)) { text = label };
                button.AddToClassList("stage-select-button");
                stageListContainer.Add(button);
            }
            else
            {
                var slot = new VisualElement();
                slot.AddToClassList("stage-select-button");
                slot.AddToClassList("stage-select-empty");
                stageListContainer.Add(slot);
            }
        }
    }
    private VisualElement stageListPanel;

    #endregion

    #region UnitSelect

    private VisualElement unitSelectScreen;
    private VisualElement trainImage;
    private Button[] unitSlotButtons = new Button[Loadout.SlotCount];
    private ScrollView trainList;
    private ScrollView unitList;
    private Button unitSelectConfirmButton;
    private readonly Dictionary<Item_Train, Button> trainCandidateButtons = new();
    private readonly Dictionary<Item_Unit, Button> unitCandidateButtons = new();
    private VisualElement statusPopup;
    private Loadout _unitSelectLoadout;
    private bool _suppressNextClick;
    private const long LongPressMs = 500;
    private Label unitCostLabel;
    private VisualElement slotMask;
    private VisualElement unitListMask;


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
        RegisterStatusPopup(trainImage, () => _unitSelectLoadout != null ? _unitSelectLoadout.Train : null);
        trainPanel.Add(trainImage);
        top.Add(trainPanel);

        var slotRow = new VisualElement();
        slotRow.AddToClassList("us-slot-row");
        for (int i = 0; i < Loadout.SlotCount; i++)
        {
            int slotIndex = i;
            var slot = new Button(() =>
            {
                if (ConsumeSuppressedClick()) return;
                OnUnitSlotChosen?.Invoke(slotIndex);
                });
            slot.AddToClassList("us-unit-slot");
            unitSlotButtons[i] = slot;
            RegisterStatusPopup(slot, () => _unitSelectLoadout != null ? _unitSelectLoadout.Slots[slotIndex] : null);
            slotRow.Add(slot);
        }

        slotMask = new VisualElement();
        slotMask.AddToClassList("us-mask");
        slotRow.Add(slotMask);

        top.Add(slotRow);
        unitSelectScreen.Add(top);


        var bottom = new VisualElement();
        bottom.AddToClassList("us-bottom");

        trainList = new ScrollView(ScrollViewMode.Horizontal);
        trainList.AddToClassList("us-train-list");
        bottom.Add(trainList);

        unitList = new ScrollView(ScrollViewMode.Horizontal);
        unitList.AddToClassList("us-unit-list");
        unitListMask = new VisualElement();
        unitListMask.AddToClassList("us-mask");
        unitList.hierarchy.Add(unitListMask);
        bottom.Add(unitList);
        unitSelectScreen.Add(bottom);

        var backButton = new Button(() => OnUnitSelectBack?.Invoke()) { text = "BACK" };
        backButton.AddToClassList("us-back-button");
        unitSelectScreen.Add(backButton);

        unitCostLabel = new Label("- / -");
        unitCostLabel.AddToClassList("us-cost-label");
        unitCostLabel.pickingMode = PickingMode.Ignore;
        unitSelectScreen.Add(unitCostLabel);

        unitSelectConfirmButton = new Button(() => OnUnitSelectConfirmed?.Invoke()) { text = "START" };
        unitSelectConfirmButton.AddToClassList("us-confirm-button");
        unitSelectScreen.Add(unitSelectConfirmButton);

        statusPopup = new VisualElement();
        statusPopup.AddToClassList("us-popup");
        statusPopup.pickingMode = PickingMode.Ignore;
        statusPopup.style.display = DisplayStyle.None;
        unitSelectScreen.Add(statusPopup);

        unitSelectScreen.style.display = DisplayStyle.None;
        root.Add(unitSelectScreen);
    }
    public void SetUnitSelectCandidates(List<Item_Train> trains)
    {
        trainList.Clear();
        trainCandidateButtons.Clear();
        if (trains != null)
        {
            foreach (var train in trains)
            {
                if (train == null) continue;

                var button = new Button(() =>
                {
                    if (ConsumeSuppressedClick()) return;
                    OnTrainChosen?.Invoke(train);
                });
                button.AddToClassList("us-candidate");
                RegisterStatusPopup(button, () => train);
                SetIcon(button, train.icon);
                trainList.Add(button);
                trainCandidateButtons[train] = button;
            }
        }

        unitList.Clear();
        unitCandidateButtons.Clear();
        if (unitDB != null && unitDB.units != null)
        {
            foreach (var unit in unitDB.units)
            {
                if (unit == null) continue;

                var button = new Button(() =>
                {
                    if (ConsumeSuppressedClick()) return;
                    OnUnitCandidateChosen?.Invoke(unit);
                });
                button.AddToClassList("us-candidate");
                RegisterStatusPopup(button, () => unit);
                SetIcon(button, unit.icon);
                unitList.Add(button);
                unitCandidateButtons[unit] = button;
            }

        }
    }
    public void RefreshUnitSelect(Loadout loadout,int budget)
    {
        _unitSelectLoadout = loadout;

        SetIcon(trainImage,GetFormationSprite(loadout.Train));

        for (int i = 0; i < unitSlotButtons.Length; i++)
        {
            SetIcon(unitSlotButtons[i],GetFormationSprite(loadout.Slots[i]));
        }

        foreach (var pair in trainCandidateButtons)
        {
            pair.Value.EnableInClassList("us-candidate-selected", pair.Key == loadout.Train);
        }
        foreach (var pair in unitCandidateButtons)
        {
            pair.Value.EnableInClassList("us-candidate-selected", loadout.Contains(pair.Key));
        }
        bool hasTrain = loadout.Train != null;
        bool overBudget = hasTrain && loadout.TotalFuelCost > budget;

        unitCostLabel.text = hasTrain ? $"{loadout.TotalFuelCost} / {budget}" : "- / -";
        unitCostLabel.EnableInClassList("us-cost-label-over", overBudget);

        var maskDisplay = hasTrain ? DisplayStyle.None : DisplayStyle.Flex;
        slotMask.style.display = maskDisplay;
        unitListMask.style.display = maskDisplay;

        unitSelectConfirmButton.SetEnabled(loadout.IsValid && !overBudget);
    }

    private static Sprite GetFormationSprite(Base_Item item)
    {
        if (item == null) return null;
        if (item.formationSprite != null) return item.formationSprite;
        return item switch
        {
            Item_Unit u => u.icon,
            Item_Train t => t.icon,
            _ => null
        };
    }

    private bool ConsumeSuppressedClick()
    {
        if (!_suppressNextClick) return false;
        _suppressNextClick = false;
        return true;
    }

    private void RegisterStatusPopup(VisualElement element,System.Func<Base_Item> getItem)
    {
#if UNITY_ANDROID || UNITY_IOS
        const float MoveTolerance = 12f;
        IVisualElementScheduledItem press = null;
        Vector2 downPos = Vector2.zero;

        void Cancel()
        {
            press?.Pause();
            press = null;
            HideStatusPopup();
        }

        element.RegisterCallback<PointerDownEvent>(e =>
        {
            Debug.Log("[LongPress]Down");
            _suppressNextClick = false;
            press?.Pause();
            downPos = e.position;
            press = element.schedule.Execute(() =>
            {
                Debug.Log("[LongPress]Fire");
                var item = getItem();
                if (item == null) return;
                ShowStatusPopup(item, element);
                _suppressNextClick = true;
            }).StartingIn(LongPressMs);
        }, TrickleDown.TrickleDown);

        element.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (press == null) return;
            if (((Vector2)e.position - downPos).sqrMagnitude > MoveTolerance * MoveTolerance) Cancel();
        });
        element.RegisterCallback<PointerUpEvent>(_ => Cancel());
        element.RegisterCallback<PointerCancelEvent>(_ => Cancel());
#else
        element.RegisterCallback<PointerEnterEvent>(_ =>
        {
            var item = getItem();
            if(item != null) ShowStatusPopup(item,element);
        });
        element.RegisterCallback<PointerLeaveEvent>(_ => HideStatusPopup());
#endif
    }

/*#if true //UNITY_ANDROID || UNITY_IOS
        IVisualElementScheduledItem press = null;
        element.RegisterCallback<PointerDownEvent>(_ =>
        {
            _suppressNextClick = false;
            press?.Pause();
            press = element.schedule.Execute(() =>
            {
                var item = getItem();
                if (item == null) return;
                ShowStatusPopup(item, element);
                _suppressNextClick = true;
                Debug.Log("[UIManager] : PointerDown");
            });
            press.ExecuteLater(LongPressMs);
        });
        element.RegisterCallback<PointerUpEvent>(_ => { press?.Pause(); HideStatusPopup(); });
        element.RegisterCallback<PointerCancelEvent>(_ => { press?.Pause(); HideStatusPopup(); });
        element.RegisterCallback<PointerLeaveEvent>(_ => { press?.Pause(); HideStatusPopup(); Debug.Log("[UIManagr] : PointerLeave"); });
#else
        element.RegisterCallback<PointerEnterEvent>(_ =>
        {
            var item = getItem();
            if (item != null) ShowStatusPopup(item, element);
        });
        element.RegisterCallback<PointerLeaveEvent>(_ => HideStatusPopup());
#endif*/

    private void ShowStatusPopup(Base_Item item, VisualElement anchor)
    {
        if (statusPopup == null) return;

        statusPopup.Clear();

        var title = new Label(GetItemName(item));
        title.AddToClassList("us-popup-title");
        title.pickingMode = PickingMode.Ignore;
        statusPopup.Add(title);

        if(item is Item_Train)
        {
            AddPopupRow("燃料", item.maxHP.ToString());
            AddPopupRow("燃費", item.attackPower.ToString());
            AddPopupRow("速度", item.moveSpeed.ToString("F1"));
        }
        else
        {
            AddPopupRow("最大HP", item.maxHP.ToString());
            AddPopupRow("攻撃力", item.attackPower.ToString());
            AddPopupRow("攻撃間隔", item.attackInterval.ToString("F1") + "s");
            AddPopupRow("速度", item.moveSpeed.ToString("F1"));
            AddPopupRow("射程", item.attackRange.ToString("F1"));
            AddPopupRow("マッチ受容数", item.matchCapacity.ToString());
            AddPopupRow("ターゲット", item.targetingStrategy switch
        {
            Base_Item.TargetingStrategy.Nearest => "最も近い",
            Base_Item.TargetingStrategy.Farthest => "最も遠い",
            Base_Item.TargetingStrategy.LowestHp => "最も体力が低い",
            _ => "-"
        });
        }
        if(item is Item_Unit unit)
        {
            AddPopupRow("出撃コスト", unit.fuelCost.ToString());
            AddPopupRow("復活コスト", unit.reviveFuelCost.ToString());
            AddPopupRow("復活時間", unit.reviveDuration.ToString());
        }
        var origin = unitSelectScreen.WorldToLocal(anchor.worldBound.position);

        void Place(GeometryChangedEvent e)
        {
            statusPopup.UnregisterCallback<GeometryChangedEvent>(Place);
            float w = statusPopup.resolvedStyle.width;
            float h = statusPopup.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h)) return;

            float maxX = Mathf.Max(0f, unitSelectScreen.resolvedStyle.width - w);
            float maxY = Mathf.Max(0f, unitSelectScreen.resolvedStyle.height - h);
            statusPopup.style.left = Mathf.Clamp(origin.x - w, 0f, maxX);
            statusPopup.style.top = Mathf.Clamp(origin.y - h, 0f, maxY);
            statusPopup.style.visibility = Visibility.Visible;
        }
        statusPopup.style.visibility = Visibility.Hidden;
        statusPopup.RegisterCallback<GeometryChangedEvent>(Place);
        statusPopup.style.display = DisplayStyle.Flex;
    }

    private void HideStatusPopup()
    {
        if (statusPopup == null) return;
        statusPopup.style.display = DisplayStyle.None;
    }

    private void AddPopupRow(string caption,string value)
    {
        var row = new VisualElement();
        row.AddToClassList("us-popup-row");
        row.pickingMode = PickingMode.Ignore;

        var cap = new Label(caption);
        cap.AddToClassList("us-popup-caption");
        cap.pickingMode = PickingMode.Ignore;
        var val = new Label(value);
        val.AddToClassList("us-popup-value");
        val.pickingMode = PickingMode.Ignore;

        row.Add(cap);
        row.Add(val);
        statusPopup.Add(row);
    }

    private static string GetItemName(Base_Item item)
    {
        return item switch
        {
            Item_Unit u => u.displayname,
            Item_Train t => t.displayname,
            _ => item.name
        };
    }
#endregion

    #region FuelSet

    private VisualElement fuelSetScreen;
    private VisualElement fuelSetBarFill;
    private VisualElement fuelSetTrainImage;
    private Label fuelSetTapLabel;
    private IVisualElementScheduledItem fuelSetBlink;
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
    public void SetFuelSetTrain(Item_Train train)
    {
        SetIcon(fuelSetTrainImage, train != null ? train.icon : null);
    }
    public void SetFuelSetStatus(FuelSetStatus status)
    {
        if (fuelSetTapLabel == null) return;

        bool visible = status != FuelSetStatus.None;
        fuelSetTapLabel.text = status switch
        {
            FuelSetStatus.Charging => "燃料充填中...",
            FuelSetStatus.Paying => "乗務員構築中...",
            FuelSetStatus.Ready => "運行開始",
            _ => ""
        };
        fuelSetTapLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (visible) fuelSetBlink?.Resume();
        else fuelSetBlink?.Pause();
    }

    #endregion

#endregion

    #region OperationScreens
    private VisualElement fuelBarFill;

    #region RouteBar
    private VisualElement routeBar;
    private VisualElement routeTrain;
    private readonly List<VisualElement> routeMarkers = new();
    private int routePassedCount = -1;
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
    /// <summary>
    /// Operation突入時に、Sectionの種類に合わせ印を作りなおす
    /// </summary>
    public void BuildRouteBar(List<SectionType> sectionTypes)
    {
        foreach (var marker in routeMarkers) marker.RemoveFromHierarchy();
        routeMarkers.Clear();
        routePassedCount = -1;

        int count = sectionTypes != null ? sectionTypes.Count : 0;
        for (int i = 0; i < count; i++)
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
            marker.style.left = new Length((i + 1) * 100f / count, LengthUnit.Percent);

            routeBar.Add(marker);
            routeMarkers.Add(marker);
        }

        routeTrain.BringToFront();
        UpdateRouteProgress(0, count, 0f);
    }
    public void UpdateRouteProgress(int currentIndex, int sectionCount, float legProgress)
    {
        if (routeBar == null || sectionCount <= 0) return;

        routeTrain.style.left = new Length((currentIndex + legProgress) * 100f / sectionCount, LengthUnit.Percent);

        int passed = currentIndex + (legProgress >= 1f ? 1 : 0);
        if (passed == routePassedCount) return;

        routePassedCount = passed;
        for (int i = 0; i < routeMarkers.Count; i++)
        {
            routeMarkers[i].EnableInClassList("route-marker-passed", i < passed);
        }
    }
    public void SetRouteBarVisible(bool visible)
    {
        if (routeBar == null) return;
        routeBar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #region UnitIcons

    private Dictionary<string, VisualElement> unitIcons = new();
    private Dictionary<string, VisualElement> reviveFills = new();
    private Dictionary<string, Label> reviveLabels = new();
    private VisualElement unitContainer;
    public void BuildUnitIcons(List<Item_Unit> units)
    {
        unitContainer.Clear();
        unitIcons.Clear();
        reviveFills.Clear();
        reviveLabels.Clear();

        foreach (var unitData in units)
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
    public void HandleUnitDeployed(string itemId, GameObject unit)
    {
        if (unitIcons.TryGetValue(itemId, out var icon))
        {
            icon.AddToClassList("unit-icon-deployed");
        }
    }
    public void HandleUnitReturned(string itemId, GameObject unit)
    {
        if (unitIcons.TryGetValue(itemId, out var icon))
        {
            icon.RemoveFromClassList("unit-icon-deployed");
        }
    }
    public void HandleUnitDied(string itemId)
    {
        if (unitIcons.TryGetValue(itemId, out var icon))
        {
            icon.RemoveFromClassList("unit-icon-deployed");
            icon.AddToClassList("unit-icon-dead");
        }
    }
    public void HandleUnitRevived(string itemId)
    {
        if (unitIcons.TryGetValue(itemId, out var icon))
        {
            icon.RemoveFromClassList("unit-icon-dead");
        }
        SetReviveVisible(itemId, false);
    }
    public void HandleReviveStarted(string itemId)
    {
        SetReviveVisible(itemId, true);
    }
    public void HandleReviveProgress(string itemId, float remaining, float progress)
    {
        if (reviveFills.TryGetValue(itemId, out var fill))
        {
            fill.style.height = new Length(progress * 100f, LengthUnit.Percent);
        }
        if (reviveLabels.TryGetValue(itemId, out var label))
        {
            label.text = remaining > 0f ? remaining.ToString("F1") : "READY";
        }
    }
    private void SetReviveVisible(string itemId, bool visible)
    {
        var display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (reviveFills.TryGetValue(itemId, out var fill))
        {
            fill.style.display = display;
            if (visible) fill.style.height = new Length(0f, LengthUnit.Percent);
        }
        if (reviveLabels.TryGetValue(itemId, out var timeLabel))
        {
            timeLabel.style.display = display;
        }
    }
    public void ResetUnitIcons()
    {
        foreach (var icon in unitIcons.Values)
        {
            icon.RemoveFromClassList("unit-icon-deployed");
            icon.RemoveFromClassList("unit-icon-dead");
        }
        foreach (var itemId in reviveFills.Keys)
        {
            SetReviveVisible(itemId, false);
        }
        HideRevivePopup();
    }

    #endregion

    #region BattleControls
    private VisualElement alertOverlay;
    private IVisualElementScheduledItem alertBlink;
    private VisualElement battleButtons;
    private void SetupBattleControls()
    {
        alertOverlay = new VisualElement();
        alertOverlay.AddToClassList("alert-overlay");
        alertOverlay.pickingMode = PickingMode.Ignore;
        alertOverlay.style.display = DisplayStyle.None;
        root.Add(alertOverlay);

        alertBlink = alertOverlay.schedule
            .Execute(() => alertOverlay.ToggleInClassList("alert-overlay-dim"))
            .Every(250);
        alertBlink.Pause();

        battleButtons = new VisualElement();
        battleButtons.AddToClassList("battle-buttons");
        battleButtons.pickingMode = PickingMode.Ignore;

        var returnAll = new Button(() => OnReturnAllRequested?.Invoke()) { text = "RETURN" };
        returnAll.AddToClassList("battle-return-button");

        var ram = new Button(() => OnRamRequested?.Invoke()) { text = "RAM" };
        ram.AddToClassList("battle-ram-button");

        battleButtons.Add(returnAll);
        battleButtons.Add(ram);
        battleButtons.style.display = DisplayStyle.None;
        root.Add(battleButtons);
    }
    public void SetAlert(bool on)
    {
        if (alertOverlay == null) return;

        alertOverlay.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        if (on) alertBlink?.Resume();
        else alertBlink?.Pause();
    }
    public void SetBattleButtonsVisible(bool visible)
    {
        if (battleButtons == null) return;
        battleButtons.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    #endregion

    #region RevivePopup
    private VisualElement reviveOverlay;
    private Label reviveCostLabel;
    private VisualElement reviveIcon;
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

        buttonRow.Add(confirmButton);
        buttonRow.Add(cancelButton);

        panel.Add(reviveIcon);
        panel.Add(reviveCostLabel);
        panel.Add(buttonRow);
        reviveOverlay.Add(panel);

        reviveOverlay.style.display = DisplayStyle.None;
        root.Add(reviveOverlay);
    }
    public void ShowRevivePopup(Sprite icon, int fuelCost)
    {
        SetIcon(reviveIcon, icon);
        reviveCostLabel.text = $"必要燃料: {fuelCost}";
        reviveOverlay.style.display = DisplayStyle.Flex;
    }
    public void HideRevivePopup()
    {
        reviveOverlay.style.display = DisplayStyle.None;
    }

    #endregion
    #region EventPopup
    private VisualElement eventOverlay;
    private VisualElement eventImage;
    private Label eventTitleLabel;
    private Label eventBodyLabel;
    private VisualElement eventChoiceContainer;

    private void SetupEventPopup()
    {
        eventOverlay = new VisualElement();
        eventOverlay.AddToClassList("ev-overlay");

        var panel = new VisualElement();
        panel.AddToClassList("ev-panel");

        eventImage = new VisualElement();
        eventImage.AddToClassList("ev-image");

        eventTitleLabel = new Label();
        eventTitleLabel.AddToClassList("ev-title");
        eventBodyLabel = new Label();
        eventBodyLabel.AddToClassList("ev-body");

        eventChoiceContainer = new VisualElement();
        eventChoiceContainer.AddToClassList("ev-choices");

        panel.Add(eventImage);
        panel.Add(eventTitleLabel);
        panel.Add(eventBodyLabel);
        panel.Add(eventChoiceContainer);
        eventOverlay.Add(panel);

        eventOverlay.style.display = DisplayStyle.None;
        root.Add(eventOverlay);
    }

    public void ShowEventPopup(EventData data)
    {
        if (data == null) return;
        SetIcon(eventImage, data.image);
        eventImage.style.display = data.image != null ? DisplayStyle.Flex : DisplayStyle.None;
        eventTitleLabel.text = data.title;
        eventBodyLabel.text = data.body;

        eventChoiceContainer.Clear();
        for(int i = 0;i < data.choices.Count;i++)
        {
            int index = i;
            var choice = data.choices[i];

            var button = new Button(() => OnEventChoiceChosen?.Invoke(index));
            button.AddToClassList("ev-choice-button");

            var label = new Label(choice.label);
            label.AddToClassList("ev-choice-label");
            label.pickingMode = PickingMode.Ignore;
            button.Add(label);

            if(!string.IsNullOrEmpty(choice.description))
            {
                var desc = new Label(choice.description);
                desc.AddToClassList("ev-choice-desc");
                desc.pickingMode = PickingMode.Ignore;
                button.Add(desc);
            }
            eventChoiceContainer.Add(button);
        }
        eventOverlay.style.display = DisplayStyle.Flex;
    }

    public void HideEventPopup()
    {
        if (eventOverlay != null) eventOverlay.style.display = DisplayStyle.None;
    }
    #endregion

    #region ServantResultScreen

    private VisualElement servantResultScreen;
    private VisualElement srMvpImage;
    private Label srFuelLabel;
    private VisualElement srRowContainer;

    public event System.Action OnServantResultTapped;

    private void SetupServantResultScreen()
    {
        servantResultScreen = new VisualElement();
        servantResultScreen.AddToClassList("sr-screen");

        var mvpFrame = new VisualElement();
        mvpFrame.AddToClassList("sr-mvp-frame");
        srMvpImage = new VisualElement();
        srMvpImage.AddToClassList("sr-mvp-image");
        mvpFrame.Add(srMvpImage);
        servantResultScreen.Add(mvpFrame);

        var right = new VisualElement();
        right.AddToClassList("sr-right");

        srFuelLabel = new Label();
        srFuelLabel.AddToClassList("sr-fuel-label");
        right.Add(srFuelLabel);

        srRowContainer = new VisualElement();
        srRowContainer.AddToClassList("sr-row-container");
        right.Add(srRowContainer);

        servantResultScreen.Add(right);

        servantResultScreen.RegisterCallback<PointerDownEvent>(_ => OnServantResultTapped?.Invoke());
        servantResultScreen.style.display = DisplayStyle.None;
        root.Add(servantResultScreen);
    }

    public void ShowServantResult(int fuelDelta,List<UnitManager.BattleDamageEntry> ranking)
    {
        srFuelLabel.text = fuelDelta >= 0 ? $"FUEL +{fuelDelta}" : $"FUEL {fuelDelta}";
        srFuelLabel.EnableInClassList("sr-fuel-negative", fuelDelta < 0);

        bool hasDamage = ranking.Count > 0 && ranking[0].damage > 0;
        SetIcon(srMvpImage, hasDamage ? ranking[0].icon : null);

        srRowContainer.Clear();
        int maxDamage = hasDamage ? ranking[0].damage : 1;
        foreach(var entry in ranking)
        {
            var row = new VisualElement();
            row.AddToClassList("sr-row");

            var icon = new VisualElement();
            icon.AddToClassList("sr-row-icon");
            SetIcon(icon, entry.icon);

            var track = new VisualElement();
            track.AddToClassList("sr-bar-track");
            var fill = new VisualElement();
            fill.AddToClassList("sr-bar-fill");
            fill.style.width = new Length(entry.damage * 100f / maxDamage, LengthUnit.Percent);
            track.Add(fill);

            var value = new Label(entry.damage.ToString());
            value.AddToClassList("sr-row-value");

            row.Add(icon);
            row.Add(track);
            row.Add(value);
            srRowContainer.Add(row);
        }

        servantResultScreen.style.display = DisplayStyle.Flex;
    }

    public void HideServantResult()
    {
        if (servantResultScreen == null) return;
        servantResultScreen.style.display = DisplayStyle.None;
    }
    #endregion

    #endregion
    
    #region ResultScreen

    public struct UnitResultRow
    {
        public Sprite icon;
        public int dealt;
        public int taken;
        public int revives;
        public int deaths;
    }

    public struct TrainResultData
    {
        public Sprite trainIcon;
        public float totalDistance;
        public int sectionsPassed;
        public int sectionCount;
        public int fuel;
        public int maxFuel;
        public int crushedUnits;
        public int crushedEnemies;
    }

    private void SetupResultScreens()
    {
        unitResultScreen = BuildResultScreen(out urImage, out urHeader, out var urRight);
        urRowContainer = new VisualElement();
        urRowContainer.AddToClassList("rs-row-container");
        urRight.Add(urRowContainer);

        trainResultScreen = BuildResultScreen(out trImage, out trHeader, out var trRight);
        trDistanceLabel = AddStatLine(trRight, "総移動距離");
        trSectionLabel = AddStatLine(trRight, "突破したセクション");
        trFuelLabel = AddStatLine(trRight, "Fuel残量");
        trCrushedUnitLabel = AddStatLine(trRight, "轢いたUnit数");
        trCrushedEnemyLabel = AddStatLine(trRight, "轢いたEnemy数");
    }

    private VisualElement BuildResultScreen(out VisualElement image,out Label header,out VisualElement right)
    {
        var screen = new VisualElement();
        screen.AddToClassList("rs-screen");

        var frame = new VisualElement();
        frame.AddToClassList("rs-frame");

        image = new VisualElement();
        image.AddToClassList("rs-image");
        image.pickingMode = PickingMode.Ignore;
        frame.Add(image);

        header = new Label();
        header.AddToClassList("rs-header");
        header.pickingMode = PickingMode.Ignore;
        frame.Add(header);
        screen.Add(frame);

        right = new VisualElement();
        right.AddToClassList("rs-right");
        screen.Add(right);

        screen.style.display = DisplayStyle.None;
        root.Add(screen);
        return screen;
    }

    private static Label AddStatLine(VisualElement parsent,string caption)
    {
        var row = new VisualElement();
        row.AddToClassList("rs-stat-row");

        var cap = new Label(caption);
        cap.AddToClassList("rs-stat-caption");
        var value = new Label();
        value.AddToClassList("rs-stat-value");

        row.Add(cap);
        row.Add(value);
        parsent.Add(row);
        return value;
    }

    private static void ApplyResultHeader(Label header,RunOutcome outcome)
    {
        bool cleared = outcome == RunOutcome.Cleared;
        header.text = cleared ? "complete" : "failed";
        header.EnableInClassList("rs-header-cleared", cleared);
        header.EnableInClassList("rs-header-failed", !cleared);
    }

    private VisualElement BuildBarLine(int value,int max,string fillClass)
    {
        var line = new VisualElement();
        line.AddToClassList("rs-bar-line");

        var track = new VisualElement();
        track.AddToClassList("rs-bar-track");
        var fill = new VisualElement();
        fill.AddToClassList("rs-bar-fill");
        fill.AddToClassList(fillClass);
        fill.style.width = new Length(max > 0 ? value * 100f / max : 0f, LengthUnit.Percent);
        track.Add(fill);

        var label = new Label(value.ToString());
        label.AddToClassList("rs-bar-value");

        line.Add(track);
        line.Add(label);
        return line;
    }

    #region UnitResult
    private VisualElement unitResultScreen;
    private VisualElement urImage;
    private Label urHeader;
    private VisualElement urRowContainer;

    public void ShowUnitResult(RunOutcome outcome,List<UnitResultRow>rows)
    {
        ApplyResultHeader(urHeader, outcome);

        int mvp = -1, maxDealt = 0, maxAny = 1;
        for(int i = 0; i<rows.Count; i++)
        {
            if (rows[i].dealt > maxDealt) { maxDealt = rows[i].dealt;mvp = i; }
            maxAny = Mathf.Max(maxAny, Mathf.Max(rows[i].dealt, rows[i].taken));
        }
        SetIcon(urImage, mvp >= 0 ? rows[mvp].icon : null);


        urRowContainer.Clear();
        foreach(var row in rows)
        {
            var rowElement = new VisualElement();
            rowElement.AddToClassList("rs-row");

            var icon = new VisualElement();
            icon.AddToClassList("rs-row-icon");
            SetIcon(icon, row.icon);

            var bars = new VisualElement();
            bars.AddToClassList("rs-bars");
            bars.Add(BuildBarLine(row.dealt, maxAny, "rs-bar-fill-dealt"));
            bars.Add(BuildBarLine(row.taken, maxAny, "rs-bar-fill-taken"));


            var counts = new VisualElement();
            counts.AddToClassList("rs-row-counts");

            var reviveLabel = new Label($"復活{row.revives}");
            reviveLabel.AddToClassList("rs-row-revive");
            var deathLabel = new Label($"死亡{row.deaths}");
            deathLabel.AddToClassList("rs-row-death");
            counts.Add(reviveLabel);
            counts.Add(deathLabel);


            rowElement.Add(icon);
            rowElement.Add(counts);
            rowElement.Add(bars);
            urRowContainer.Add(rowElement);
        }
        trainResultScreen.style.display = DisplayStyle.None;
        unitResultScreen.style.display = DisplayStyle.Flex;
    }
    #endregion

    #region TrainResult
    private VisualElement trainResultScreen;
    private VisualElement trImage;
    private Label trHeader;
    private Label trDistanceLabel;
    private Label trSectionLabel;
    private Label trFuelLabel;
    private Label trCrushedUnitLabel;
    private Label trCrushedEnemyLabel;

    public void ShowTrainResult(RunOutcome outcome,TrainResultData data)
    {
        ApplyResultHeader(trHeader, outcome);
        SetIcon(trImage, data.trainIcon);

        trDistanceLabel.text = data.totalDistance.ToString("F0");
        trSectionLabel.text = $"{data.sectionsPassed} / {data.sectionCount}";
        trFuelLabel.text = $"{data.fuel} / {data.maxFuel}";
        trCrushedUnitLabel.text = data.crushedUnits.ToString();
        trCrushedEnemyLabel.text = data.crushedEnemies.ToString();

        unitResultScreen.style.display = DisplayStyle.None;
        trainResultScreen.style.display = DisplayStyle.Flex;
    }

    #endregion

    public void HideResult()
    {
        if (unitResultScreen != null) unitResultScreen.style.display = DisplayStyle.None;
        if (trainResultScreen != null) trainResultScreen.style.display = DisplayStyle.None;
    }
    #endregion
}