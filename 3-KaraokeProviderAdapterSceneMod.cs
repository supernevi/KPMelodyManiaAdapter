using UniInject;
using UnityEngine;

// Mod interface to do something when a scene is loaded.
// Available scenes are found in the EScene enum.
public class KaraokeProviderAdapterSceneMod : ISceneMod
{
    // Get common objects from the app environment via Inject attribute.
    [Inject]
    private UIDocument uiDocument;

    [Inject]
    private SceneNavigator sceneNavigator;

    // Mod settings implement IAutoBoundMod, which makes an instance available via Inject attribute
    [Inject]
    private KaraokeProviderAdapterModSettings modSettings;

    private readonly List<IDisposable> disposables = new List<IDisposable>();

    public void OnSceneEntered(SceneEnteredContext sceneEnteredContext)
    {
        // You can do anything here, for example ...

        // ... show a message
        UiManager.CreateNotification($"Welcome to {sceneEnteredContext.Scene}!");

        // ... change UI elements
        // uiDocument.rootVisualElement.Query<VisualElement>().ForEach(element =>
        // {
        //     element.style.borderTopColor = new StyleColor(Color.red);
        //     element.style.borderTopWidth = 1;
        // });

        // ... create new Unity GameObjects with custom behaviour.
        GameObject gameObject = new GameObject();
        gameObject.name = nameof(KaraokeProviderAdapterMonoBehaviour);
        KaraokeProviderAdapterMonoBehaviour behaviour = gameObject.AddComponent<KaraokeProviderAdapterMonoBehaviour>();
        sceneEnteredContext.SceneInjector.Inject(behaviour);
    }
}

public class KaraokeProviderAdapterMonoBehaviour : MonoBehaviour, INeedInjection
{
    // Awake is called once after instantiation
    private void Awake()
    {
        Debug.Log($"{nameof(KaraokeProviderAdapterMonoBehaviour)}.Awake");
    }

    // Start is called once before Update
    private void Start()
    {
        Debug.Log($"{nameof(KaraokeProviderAdapterMonoBehaviour)}.Start");
    }

    // Update is called once per frame
    private void Update()
    {
    }

    private void OnDestroy()
    {
        Debug.Log($"{nameof(KaraokeProviderAdapterMonoBehaviour)}.OnDestroy");
        // GameObjects are destroyed before the next scene is loaded.
        // To persist a GameObject across scene changes, make it a child of DontDestroyOnLoadManager.
    }
}