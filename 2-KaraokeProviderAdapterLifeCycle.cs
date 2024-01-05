using UnityEngine;
using UniInject;
using UnityEngine.UIElements;
using UniRx;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Linq;
using System.IO;
using System.Text;

// Open the mod folder with Visual Studio Code and installed C# Dev Kit for IDE features such as
// code completion, error markers, parameter hints, go to definition, etc.
// ---
// Mods must implement subtypes of special mod interfaces.
// Available interfaces can be found by executing 'mod.interfaces' in the game's console.
public class KaraokeProviderAdapterLifeCycle : IOnLoadMod, IOnDisableMod, ISongRepository
{
    // Get common objects from the app environment via Inject attribute.
    [Inject]
    private AudioManager audioManager;

    [Inject]
    private KaraokeProviderAdapterModSettings kpAdapterSettings;

    private static bool isKaraokeProviderRequestDone;

    private static readonly Dictionary<string, SongRepositorySearchResultEntry> songIdToSearchResultCache = new Dictionary<string, SongRepositorySearchResultEntry>();
    private static List<KaraokeProviderSongMetaDto> webSongMetas;

    [Serializable]
    class KaraokeProviderSongMetaDto
    {
        public string songId { get; set; }
        public string audioLink { get; set; }
        public string videoLink { get; set; }
        public string coverLink { get; set; }
        public string backgroundLink { get; set; }
        public string textLink { get; set; }
        public string artist { get; set; }
        public string title { get; set; }
    }

    public void OnLoadMod()
    {
        // You can do anything here, for example ...

        // ... change audio clips
        // audioManager.defaultButtonSound = AudioManager.LoadAudioClipFromUriImmediately($"{modContext.ModFolder}/sounds/cartoon-jump-6462.mp3");

        Debug.Log($"{nameof(KaraokeProviderAdapterLifeCycle)}.OnLoadMod");

        Debug.Log($"Getting KaraokeProvider songs from {kpAdapterSettings.KaraokeProviderApiUrl}");
        UnityWebRequest request = UnityWebRequest.Get(kpAdapterSettings.KaraokeProviderApiUrl);

        request.SendWebRequest()
            .AsAsyncOperationObservable()
            .Subscribe(_ => LoadKaraokeProviderSongs(request),
                exception => Debug.LogException(exception),
                () => LoadKaraokeProviderSongs(request));
    }

    private void LoadKaraokeProviderSongs(UnityWebRequest webRequest)
    {
        if (isKaraokeProviderRequestDone
            || !webRequest.isDone)
        {
            return;
        }

        isKaraokeProviderRequestDone = true;
        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Getting song metas from '{webRequest.url}'"
                           + $" has result {webRequest.result}.\n{webRequest.error}");
            return;
        }

        string body = webRequest.downloadHandler.text;
        webSongMetas = JsonConverter.FromJson<List<KaraokeProviderSongMetaDto>>(body);
        Debug.Log($"{webSongMetas.Count} songs found");
    }


    public void OnDisableMod()
    {
        Debug.Log($"{nameof(KaraokeProviderAdapterLifeCycle)}.OnDisableMod");
    }

    public IObservable<SongRepositorySearchResultEntry> SearchSongs(SongRepositorySearchParameters searchParameters)
    {
        if(!isKaraokeProviderRequestDone)
        {
            Debug.Log("songs not loaded yet");
            return Observable.Empty<SongRepositorySearchResultEntry>();
        }

        if (searchParameters == null
            || searchParameters.SearchText.IsNullOrEmpty())
        {
            Debug.Log("no search text given");
            return Observable.Empty<SongRepositorySearchResultEntry>();
        }

        Debug.Log($"searching song with search text: '{searchParameters.SearchText}'");

        return ObservableUtils.RunOnNewTaskAsObservableElements(() => SearchSongList(searchParameters), Disposable.Empty);
    }

    public List<SongRepositorySearchResultEntry> SearchSongList(SongRepositorySearchParameters searchParameters)
    {
        string searchText = searchParameters.SearchText;
        if (searchText.IsNullOrEmpty())
        {
            return new List<SongRepositorySearchResultEntry>();
        }

        string searchTextLower = searchText.ToLower();
        List<SongRepositorySearchResultEntry> resultEntries = webSongMetas
            .Where(songMeta =>
            {
                return songMeta.title.Contains(searchTextLower) || songMeta.artist.Contains(searchTextLower);
            })
            .Select(LoadUltraStarSongFromProvider)
            .Where(it => it != null)
            .ToList();
        Debug.Log($"{nameof(KaraokeProviderAdapterLifeCycle)} - Found {resultEntries.Count} songs matching search '{searchText}'");
        return resultEntries;
    }

    private SongRepositorySearchResultEntry LoadUltraStarSongFromProvider(KaraokeProviderSongMetaDto kpSongMeta)
    {
        if (songIdToSearchResultCache.TryGetValue(kpSongMeta.songId, out SongRepositorySearchResultEntry cachedResultEntry))
        {
            return cachedResultEntry;
        }


    }

    private void fetchTextFileAndAddToSongMeta(KaraokeProviderSongMetaDto source)
    {
        UnityWebRequest request = UnityWebRequest.Get(kpAdapterSettings.KaraokeProviderApiUrl + source.textLink);
        request.SendWebRequest()
            .AsAsyncOperationObservable()
            .Subscribe(_ => SaveKaraokeProviderSongAndParse(request, source),
                exception => Debug.LogException(exception));
    }

    private SongRepositorySearchResultEntry SaveKaraokeProviderSongAndParse(UnityWebRequest webRequest, KaraokeProviderSongMetaDto source)
    {
        if (!webRequest.isDone)
        {
            Log.Debug($"WebRequest for song '{source.artist} - {source.title}' not completed yet");
            return null;
        }

        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Getting song text from '{webRequest.url}' for song '{source.artist} - {source.title}'"
                           + $" has result {webRequest.result}.\n{webRequest.error}");
            return null;
        }

        string folderName = Application.persistentDataPath + "/KaraokeProviderSongs/" + source.artist + "/" + source.title + "/";
        string fileName = source.artist + " - " + source.title + ".txt";

        Directory.CreateDirectory(folderName);

        string fullPath = folderName + fileName;
        File.WriteAllText(fullPath, webRequest.downloadHandler.text);

        try
        {
            SongMeta newSongMeta = UltraStarSongParser.ParseFile(fullPath, out List<SongIssue> newSongIssues, Encoding.UTF8);
            SongRepositorySearchResultEntry resultEntry = new SongRepositorySearchResultEntry(newSongMeta, newSongIssues);
            newSongMeta.RemoteSource = nameof(KaraokeProviderAdapterLifeCycle);
            newSongMeta.Audio = appendUrlPrefix(source.audioLink);
            newSongMeta.Video = appendUrlPrefix(source.videoLink);
            newSongMeta.Background = appendUrlPrefix(source.backgroundLink);
            newSongMeta.Cover = appendUrlPrefix(source.coverLink);

            songIdToSearchResultCache[source.songId] = resultEntry;

            Debug.Log($"'{newSongMeta.Artist} - {newSongMeta.Title}' added");
            return resultEntry;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Debug.LogError($"Failed to load {fullPath}");
            return null;
        }
    }

    private string appendUrlPrefix(string value)
    {
        if (!String.IsNullOrEmpty(value))
        {
            return kpAdapterSettings.KaraokeProviderApiUrl + value;
        }
        return value;
    }
}
