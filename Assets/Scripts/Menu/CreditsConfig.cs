using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CreditsConfig", menuName = "Bullseye/Credits Config")]
public class CreditsConfig : ScriptableObject
{
    [Serializable]
    public class CreditCategory
    {
        public string heading = "Game Directors";
        public string[] names = { "[Director Name]", "[Director Name]" };
    }

    public string gameTitle = "BULLSEYE";

    [Tooltip("Only Game Directors is required for REQ-030. Additional categories can be added later.")]
    public CreditCategory[] categories =
    {
        new CreditCategory
        {
            heading = "Game Directors",
            names = new[] { "[Director Name]", "[Director Name]" }
        }
    };

    public IEnumerable<CreditCategory> GetCategories()
    {
        if (categories == null || categories.Length == 0)
        {
            yield return new CreditCategory();
            yield break;
        }

        for (int i = 0; i < categories.Length; i++)
        {
            if (categories[i] != null)
                yield return categories[i];
        }
    }
}
