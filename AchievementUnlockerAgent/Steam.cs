using Serilog;

namespace AchievementUnlockerAgent;

using Steamworks;

internal class Steam : IDisposable
{
    private readonly string _appIdFile = "steam_appid.txt";
    private readonly string _delimiter = string.Concat(Enumerable.Repeat("-", 20));

    public void Dispose()
    {
        SteamAPI.Shutdown();
        SteamAPI.ReleaseCurrentThreadMemory();
        File.Delete(_appIdFile);
    }

    public ushort Init(string gameName, string appId)
    {
        if (!Connect(appId)) return 1;
        SteamUserStats.RequestCurrentStats();
        Log.Information("Game: {GameName}", gameName);
        Log.Information("App: {AppId}", appId);
        var achievements = ListAchievements();
        if (achievements.Count != 0)
            UnlockAchievements(achievements);
        Log.Information("{Delimiter}", _delimiter);
        Dispose();
        return 0;
    }

    private bool Connect(string appId)
    {
        File.WriteAllText(_appIdFile, appId);
        try
        {
            return SteamAPI.Init();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SteamAPI failed to connect, make sure Steam is running");
        }
        return false;
    }

    private List<string> ListAchievements()
    {
        uint achievementCount = SteamUserStats.GetNumAchievements();
        Log.Information("Achievements: {NumOfAchievements}", achievementCount);
        
        var achievements = new List<string>();
        for (uint i = 0; i < achievementCount; i++)
        {
            var name = SteamUserStats.GetAchievementName(i);
            if (string.IsNullOrEmpty(name)) break;
            achievements.Add(name);
        }
        return achievements;
    }
    
    private void UnlockAchievements(List<string> achievements)
    {
        Log.Information("{Delimiter}", _delimiter);
        uint maxAttempts = 3;
        foreach (var achievement in achievements)
        {
            ushort attempt = 0;
            bool unlocked;
            bool alreadyDone;

            do
            {
                unlocked = Loop(achievement, out alreadyDone);
                if (unlocked || alreadyDone) break;
                attempt++;
            } while (attempt < maxAttempts);

            if (alreadyDone)
                Log.Information("Done: {Achievement}", achievement);
            else if (unlocked)
                Log.Information("Unlocked: {Achievement}", achievement);
            else
                Log.Error("Failed: {Achievement}", achievement);
        }
    }
    
    private bool Loop(string achievement, out bool alreadyDone)
    {
        try
        {
            if (!SteamUserStats.GetAchievement(achievement, out bool done)) { alreadyDone = false; return false; }
            if (done) { alreadyDone = true; return true; }
            if (!SteamUserStats.SetAchievement(achievement)) { alreadyDone = false; return false; }
            if (!SteamUserStats.GetAchievement(achievement, out bool done2)) { alreadyDone = false; return false; }
            if (done2) { alreadyDone = false; return false; }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception: {Achievement}", achievement);
        }
        { alreadyDone = false; return false; }
    }
}