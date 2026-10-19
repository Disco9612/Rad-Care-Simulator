using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EvaluationSoundManager : MonoBehaviour
{
    public static EvaluationSoundManager Instance;

    [Header("Audio Sources")]
    public AudioSource sfxSource;
    public AudioSource NPCSource;

    [Header("SFX")]
    public AudioClip scanSound;
    public AudioClip warningSound;
    public AudioClip waterSound;
    public AudioClip trashSound;
    public AudioClip itemSound;

    [Header("NPC")]
    public AudioClip NPC1;
    public AudioClip NPC2;
    public AudioClip NPC3;
    public AudioClip NPC4;
    public AudioClip NPC5;
    public AudioClip NPC6;
    public AudioClip NPC7;
    public AudioClip NPC8;
    public AudioClip NPC9;
    public AudioClip NPC10;
    public AudioClip NPC11;
    public AudioClip NPC12;
    public AudioClip NPC13;

    private Queue<NPCMessage> npcQueue = new Queue<NPCMessage>();
    private bool isPlayingNPC = false;
    private float currentSfxVolumeMultiplier = 1.0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void PlaySFX(AudioClip clip, float volume = 1.0f)
    {
        if (clip == null) return;
        float finalVolume = volume * currentSfxVolumeMultiplier;

        if (sfxSource.isPlaying && sfxSource.clip == clip)
        {
            sfxSource.volume = finalVolume;
            return;
        }

        sfxSource.clip = clip;
        sfxSource.loop = true;
        sfxSource.volume = finalVolume;
        sfxSource.Play();
    }
    public void PlaySFXOneShot(AudioClip clip, float volume = 1.0f)
    {
        if (clip == null) return;
        float finalVolume = volume * currentSfxVolumeMultiplier;
        sfxSource.PlayOneShot(clip, finalVolume);
    }

    public void StopSFX()
    {
        sfxSource.Stop();
        sfxSource.loop = false;
        sfxSource.clip = null;
    }

    public void PlayNPC(AudioClip clip, float volume = 2.0f)
    {
        if (clip == null) return;
        npcQueue.Enqueue(new NPCMessage(clip, volume));
        if (!isPlayingNPC)
        {
            StartCoroutine(ProcessNPCQueue());
        }
    }

    private IEnumerator ProcessNPCQueue()
    {
        isPlayingNPC = true;
        currentSfxVolumeMultiplier = 0.2f;
        ApplySfxVolumeDucking();

        while (npcQueue.Count > 0)
        {
            NPCMessage message = npcQueue.Dequeue();

            Debug.Log($"현재 재생 소리: {message.clip.name}");
            NPCSource.clip = message.clip;
            NPCSource.volume = message.volume;
            NPCSource.Play();
            yield return new WaitForSeconds(message.clip.length);
        }
        currentSfxVolumeMultiplier = 1.0f;
        ApplySfxVolumeDucking();

        isPlayingNPC = false;
    }
    private void ApplySfxVolumeDucking()
    {
        if (sfxSource.isPlaying)
        {
            sfxSource.volume = 1.0f * currentSfxVolumeMultiplier;
        }
    }

    private struct NPCMessage
    {
        public AudioClip clip;
        public float volume;

        public NPCMessage(AudioClip clip, float volume)
        {
            this.clip = clip;
            this.volume = volume;
        }
    }
    public void ResetPlayback()
    {
        StopAllCoroutines();
        npcQueue.Clear();
        isPlayingNPC = false;
        currentSfxVolumeMultiplier = 1f;
        StopSFX();
        NPCSource.Stop();
        NPCSource.clip = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
