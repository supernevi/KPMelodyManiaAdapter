using System.Collections.Generic;
using static System.Net.WebRequestMethods;

// Add settings to your mod by implementing IModSettings.
// IModSettings extends IAutoBoundMod,
// which makes an object of the type available in other scripts via Inject attribute.
// Mod settings are saved to file when the app is closed.
public class KaraokeProviderAdapterModSettings : IModSettings
{
    private string karaokeProviderApiUrl = "http://localhost:8080/songs";
    public string KaraokeProviderApiUrl { get { return karaokeProviderApiUrl;} }

    public List<IModSettingControl> GetModSettingControls()
    {
        return new List<IModSettingControl>()
        {
            new StringModSettingControl(() => karaokeProviderApiUrl, newValue => karaokeProviderApiUrl = newValue) { Label = "API URL to Karaoke Provider" },
        };
    }
}
