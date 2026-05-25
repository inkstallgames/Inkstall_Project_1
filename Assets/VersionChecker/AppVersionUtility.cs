using System;

public static class AppVersionUtility
{
    /// <summary>
    /// Returns negative if current is older, positive if newer, 0 if equal.
    /// Supports dotted numeric versions such as 2.1.3.
    /// </summary>
    public static int CompareVersions(string current, string remote)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            current = "0";
        }

        if (string.IsNullOrWhiteSpace(remote))
        {
            remote = "0";
        }

        var currentParts = current.Split('.');
        var remoteParts = remote.Split('.');
        int partCount = Math.Max(currentParts.Length, remoteParts.Length);

        for (int i = 0; i < partCount; i++)
        {
            int currentValue = i < currentParts.Length && int.TryParse(currentParts[i], out int parsedCurrent)
                ? parsedCurrent
                : 0;
            int remoteValue = i < remoteParts.Length && int.TryParse(remoteParts[i], out int parsedRemote)
                ? parsedRemote
                : 0;

            if (currentValue != remoteValue)
            {
                return currentValue.CompareTo(remoteValue);
            }
        }

        return 0;
    }

    public static bool IsUpdateAvailable(string current, string latest)
    {
        return CompareVersions(current, latest) < 0;
    }

    public static bool IsBelowMinimum(string current, string minimum)
    {
        return CompareVersions(current, minimum) < 0;
    }
}
