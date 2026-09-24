using UnityEngine;
using static Base_Item;

[CreateAssetMenu(fileName = "Data_UI", menuName = "Scriptable Objects/UI/Data_UI")]
public class Data_UI : ScriptableObject
{
    
    [SerializeField] private string _id; public string id => _id;
    [SerializeField] private string _displayname; public string displayname => _displayname;
    [SerializeField] private Sprite _icon; public Sprite icon => _icon;


}
