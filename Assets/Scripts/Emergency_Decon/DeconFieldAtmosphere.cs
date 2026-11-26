using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DeconFieldAtmosphere : MonoBehaviour
{
    AudioSource ambience;
    float fade;
    Material paper, gauze, teal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
        Loaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Emergency_Decon_Evaluation" && scene.name != "Emergency_Decon_Tutorial"
            && scene.name != "Emergency_Decon_Tutorial_Hand") return;
        if (scene.GetRootGameObjects().Any(g => g.name == "Decon Field Atmosphere")) return;
        var root = new GameObject("Decon Field Atmosphere");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<DeconFieldAtmosphere>();
    }

    void Start()
    {
        paper = MakeMaterial("Opened sterile packaging", new Color(.76f,.79f,.72f));
        gauze = MakeMaterial("Unwrapped gauze", new Color(.88f,.88f,.79f));
        teal = MakeMaterial("Packaging print", new Color(.08f,.29f,.30f));
        var all = gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        var table = all.FirstOrDefault(t => t.name == "SM_Folding_Table_01a_LOD0");
        if (table != null && table.TryGetComponent<Renderer>(out var renderer))
        {
            Bounds b = renderer.bounds;
            Vector3 corner = new Vector3(b.min.x + .48f, b.max.y + .004f, b.min.z + .15f);
            for (int i=0;i<2;i++)
            {
                var packet = new GameObject("Opened gauze packet " + (i+1));
                packet.transform.SetParent(transform);
                packet.transform.position = corner + new Vector3(i*.15f,0,i*.04f);
                packet.transform.rotation = Quaternion.Euler(0,i==0?12:-17,0);
                Piece(packet.transform,"Backing",Vector3.zero,new Vector3(.11f,.001f,.14f),paper);
                var flap = Piece(packet.transform,"Peeled wrapper",new Vector3(0,.023f,.087f),new Vector3(.11f,.001f,.075f),paper);
                flap.localRotation = Quaternion.Euler(-35,0,0);
                Piece(packet.transform,"Seal stripe",new Vector3(0,.001f,-.06f),new Vector3(.10f,.001f,.009f),teal);
                for(int layer=0;layer<3;layer++)
                    Piece(packet.transform,"Gauze fold",new Vector3(.007f,.003f+layer*.0015f,0),new Vector3(.063f,.0012f,.065f),gauze);
            }
        }
        // Reduce broad fill slightly while retaining the existing clinical key lights.
        foreach(var light in all.SelectMany(t => t.GetComponents<Light>()))
            if(light.name == "Mixed_Task_Fill") light.intensity *= .85f;
        var wound = all.FirstOrDefault(t=>t.name=="Blood");
        if(wound != null)
        {
            var lightObject = new GameObject("Focused treatment light");
            lightObject.transform.SetParent(transform);
            lightObject.transform.position = wound.position + new Vector3(.15f,1.3f,.25f);
            lightObject.transform.LookAt(wound.position);
            var light = lightObject.AddComponent<Light>();
            light.type=LightType.Spot; light.range=2.4f; light.spotAngle=65; light.innerSpotAngle=35;
            light.intensity=.55f; light.color=new Color(.88f,.94f,1f);
            light.shadows=LightShadows.Soft; light.shadowStrength=.65f; light.shadowBias=.025f;
        }
        ambience=gameObject.AddComponent<AudioSource>();
        ambience.clip=Resources.Load<AudioClip>("DeconFieldAmbience");
        ambience.loop=true; ambience.spatialBlend=0; ambience.volume=0; ambience.priority=240;
        if(ambience.clip!=null) ambience.Play();
    }

    void Update()
    {
        if(ambience==null) return;
        bool speaking = EvaluationSoundManager.Instance != null && EvaluationSoundManager.Instance.NPCSource != null
            && EvaluationSoundManager.Instance.NPCSource.isPlaying;
        speaking |= SoundManager.Instance != null && SoundManager.Instance.NPCSource != null
            && SoundManager.Instance.NPCSource.isPlaying;
        // Slowly fade in; the master ambience remains quieter than tool feedback.
        fade=Mathf.MoveTowards(fade,1,Time.unscaledDeltaTime*.25f);
        bool audible = Application.isEditor || TrainingInputFocus.Available;
        float target=audible ? (speaking ? .018f : .045f)*fade : 0;
        ambience.volume=Mathf.MoveTowards(ambience.volume,target,Time.unscaledDeltaTime*.06f);
    }

    Material MakeMaterial(string label,Color color)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=label};
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.12f);return m;
    }
    Transform Piece(Transform parent,string label,Vector3 position,Vector3 scale,Material material)
    {
        var piece=GameObject.CreatePrimitive(PrimitiveType.Cube);piece.name=label;
        piece.transform.SetParent(parent,false);piece.transform.localPosition=position;piece.transform.localScale=scale;
        var collider=piece.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        piece.GetComponent<Renderer>().sharedMaterial=material;return piece.transform;
    }
    void OnDestroy(){if(paper!=null)Destroy(paper);if(gauze!=null)Destroy(gauze);if(teal!=null)Destroy(teal);}
}
