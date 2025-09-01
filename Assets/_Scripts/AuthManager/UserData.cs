using System;

[Serializable]
public class UserCheckResponse
{
    public bool exists;
    public UserData userData;
}

[Serializable]
public class UserData
{
    public string userId;
    public string email;
    public int keys;
    public int points;
}
