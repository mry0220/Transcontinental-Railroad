using UnityEngine;

[CreateAssetMenu(fileName = "Item_Train", menuName = "Scriptable Objects/Item/Item_Train")]
public class Item_Train : Base_Item
{
    [SerializeField] private string _id;public string id => _id;
    [SerializeField] private string _displayname;public string displayname => _displayname;
    [SerializeField] private Sprite _icon;public Sprite icon => _icon;
    [SerializeField] private GameObject _prefab;public GameObject prefab => _prefab;
}
