using UnityEngine;

[CreateAssetMenu(fileName = "Item_Unit", menuName = "Scriptable Objects/Item/Item_Unit")]
public class Item_Unit : Base_Item
{
   
    [SerializeField]private string _id; public string id => _id;
    [SerializeField] private string _displayname; public string displayname => _displayname;
    [SerializeField] private Sprite _icon; public Sprite icon => _icon;

    [SerializeField] private GameObject _prefab; public GameObject prefab => _prefab;
    [SerializeField] private float _reviveDuration = 5f; public float reviveDuration => _reviveDuration;
    [SerializeField] private int _reviveFuelCost = 100;public int reviveFuelCost => _reviveFuelCost;
    [SerializeField] private int _fuelCost = 100; public int fuelCost => _fuelCost;
}
