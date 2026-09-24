using UnityEngine;

[CreateAssetMenu(fileName = "Item_Enemy", menuName = "Scriptable Objects/Item/Item_Enemy")]
public class Item_Enemy : Base_Item
{
    [SerializeField] private string _id; public string id => _id;
    [SerializeField] private string _displayname;public string displayname => _displayname;
    [SerializeField] private Sprite _icon; public Sprite icon => _icon;
    [SerializeField] private GameObject _prefab; public GameObject prefab => _prefab;
}
