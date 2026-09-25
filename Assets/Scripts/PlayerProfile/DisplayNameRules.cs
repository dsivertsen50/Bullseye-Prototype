using System.Text;

/// <summary>
/// Display-name checks shared by the profile screen and PlayerIdentity.
/// Letters, digits, and spaces only. PlayerProfileId is never derived from this.
/// </summary>
public static class DisplayNameRules
{
    public static bool IsAllowed(char character)
    {
        return character == ' ' || char.IsLetterOrDigit(character);
    }

    public static char ValidateInput(string text, int charIndex, char addedChar)
    {
        if (!IsAllowed(addedChar))
            return '\0';

        if (text != null && text.Length >= PlayerProfileConstants.MaxDisplayNameLength)
            return '\0';

        return addedChar;
    }

    public static bool TryNormalize(string raw, out string normalized, out string error)
    {
        normalized = string.Empty;
        string trimmed = string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();
        if (trimmed.Length == 0)
        {
            error = "Enter a display name.";
            return false;
        }

        if (trimmed.Length > PlayerProfileConstants.MaxDisplayNameLength)
        {
            error = "Display name is too long.";
            return false;
        }

        var builder = new StringBuilder(trimmed.Length);
        bool pendingSpace = false;
        for (int i = 0; i < trimmed.Length; i++)
        {
            char character = trimmed[i];
            if (character == ' ')
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (!char.IsLetterOrDigit(character))
            {
                error = "Use letters, numbers, and spaces.";
                return false;
            }

            if (pendingSpace)
                builder.Append(' ');
            builder.Append(character);
            pendingSpace = false;
        }

        normalized = builder.ToString();
        if (normalized.Length == 0)
        {
            error = "Enter a display name.";
            return false;
        }

        error = null;
        return true;
    }
}
