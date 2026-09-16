using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable Profile label/value row. New career stats can be added by
/// instantiating another row instead of redesigning the page.
/// </summary>
public class ProfileStatRow : MonoBehaviour
{
    public Text Label { get; private set; }
    public Text Value { get; private set; }

    public void Bind(Text label, Text value)
    {
        Label = label;
        Value = value;
    }

    public void Set(string label, string value)
    {
        if (Label != null)
            Label.text = label ?? "";
        if (Value != null)
            Value.text = value ?? ProfileStatFormatter.EmDash;
    }

    public void SetValue(string value)
    {
        if (Value != null)
            Value.text = value ?? ProfileStatFormatter.EmDash;
    }
}
