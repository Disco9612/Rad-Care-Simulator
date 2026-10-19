using System;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Rad Care/Training Catalog")]
public class TrainingCatalog : ScriptableObject
{
    public string lobbyScene = "Assets/Scenes/MainLobby.unity";
    public TMP_FontAsset font;
    public Scenario[] scenarios = Array.Empty<Scenario>();

    [Serializable]
    public class Scenario
    {
        public string title;
        [TextArea] public string description;
        public string tutorialScene;
        public string evaluationScene;
    }

    public bool ContainsScene(string path)
    {
        foreach (Scenario scenario in scenarios)
        {
            if (scenario != null && (scenario.tutorialScene == path || scenario.evaluationScene == path))
            {
                return true;
            }
        }
        return false;
    }
}
