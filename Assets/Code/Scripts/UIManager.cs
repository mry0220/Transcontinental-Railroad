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

        uIInputHandler.InitializeInputHadler(root);
    }
    private void SetupUI()
    {
        SetupUIFuel();
        SetupUIUnit();
    }
    void SetupUIFuel()
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
    void SetupUIUnit()
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
