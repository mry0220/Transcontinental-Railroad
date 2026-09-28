using System.Collections.Generic;
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

    private VisualElement reviveOverlay;
    private Label reviveCostLabel;

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
        SetupRevivePopup();
        uIInputHandler.InitializeInputHadler(root);
    }

    private void SetupRevivePopup()
    {
        reviveOverlay = new VisualElement();
        reviveOverlay.AddToClassList("revive-overlay");

        var panel = new VisualElement();
        panel.AddToClassList("revive-panel");

        reviveCostLabel = new Label();
        reviveCostLabel.AddToClassList("revive-cost-label");

        var confirmButton = new Button(() => OnReviveConfirmed?.Invoke()) { text = "Revive" };
        confirmButton.AddToClassList("revive-cofirm-button");

        var cancelButton = new Button(() => OnReviveCancelled?.Invoke()) { text = "Cancel" };
        cancelButton.AddToClassList("revive-cancel-button");

        panel.Add(reviveCostLabel);
        panel.Add(confirmButton);
        panel.Add(cancelButton);
        reviveOverlay.Add(panel);

        reviveOverlay.style.display = DisplayStyle.None;
        root.Add(reviveOverlay);
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

    

    public void ShowRevivePopup(int fuelCost)
    {
        reviveCostLabel.text = $"必要燃料: {fuelCost}";
        reviveOverlay.style.display = DisplayStyle.Flex;
    }

    public void HideRevivePopup()
    {
        reviveOverlay.style.display = DisplayStyle.None;
    }

    private void SetupUIUnit()
    {
        var container = root.Q<VisualElement>("unit-container");

        foreach (var unitData in unitDB.units)
        {
            Debug.Log($"setting up unit:id={unitData.id},icon = {unitData.icon}");

            var icon = new VisualElement();
            icon.AddToClassList("unit-icon");
            icon.style.backgroundImage = new StyleBackground(unitData.icon);
            icon.userData = unitData.id;

            container.Add(icon);
            unitIcons[unitData.id] = icon;
        }
    }

    private void OnDisable()
    {
        uIInputHandler.DeinitializeInputHandler();
    }

    public void UpdateFuelUI(int currentFuel,int maxFuel)
    {
        float percent = (float)currentFuel / maxFuel * 100f;
        fuelBarFill.style.width = new Length(percent, LengthUnit.Percent);
    }

}
