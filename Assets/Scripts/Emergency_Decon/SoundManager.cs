using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

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

    // SFX 원래 볼륨 기억용 변수
    private float currentSfxVolumeMultiplier = 1.0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 효과음 재생 (루프가 필요한 지속음용)
    public void PlaySFX(AudioClip clip, float volume = 1.0f)
    {
        if (clip == null) return;

        // NPC가 재생 중이면 요청된 볼륨의 절반만 적용
        float finalVolume = volume * currentSfxVolumeMultiplier;

        if (sfxSource.isPlaying && sfxSource.clip == clip)
        {
            // 재생 중일 때는 볼륨만 실시간 반영
            sfxSource.volume = finalVolume;
            return;
        }

        sfxSource.clip = clip;
        sfxSource.loop = true;
        sfxSource.volume = finalVolume;
        sfxSource.Play();
    }

    // 짧은 효과음(버튼 클릭, 아이템 획득 등)을 겹쳐서 재생할 때용
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

        // 대기열에 추가
        npcQueue.Enqueue(new NPCMessage(clip, volume));

        // 현재 재생 중이 아니라면 코루틴 시작
        if (!isPlayingNPC)
        {
            StartCoroutine(ProcessNPCQueue());
        }
    }

    private IEnumerator ProcessNPCQueue()
    {
        isPlayingNPC = true;

        // 1. NPC 대사가 시작되므로 SFX 볼륨을 절반(0.5배)으로 낮춤
        currentSfxVolumeMultiplier = 0.2f;
        ApplySfxVolumeDucking();

        while (npcQueue.Count > 0)
        {
            NPCMessage message = npcQueue.Dequeue();

            Debug.Log($"현재 재생 소리: {message.clip.name}");

            // 일반 Play로 재생하여 오디오 소스의 길이를 정확히 측정할 수 있게 함
            NPCSource.clip = message.clip;
            NPCSource.volume = message.volume;
            NPCSource.Play();

            // 현재 클립의 길이만큼 대기 (음성 재생이 끝날 때까지 기다림)
            yield return new WaitForSeconds(message.clip.length);
        }

        // 2. 모든 NPC 대사가 끝나면 SFX 볼륨을 원래대로(1.0배) 복구
        currentSfxVolumeMultiplier = 1.0f;
        ApplySfxVolumeDucking();

        isPlayingNPC = false;
    }

    // 현재 재생 중인 SFX의 볼륨을 배율에 맞춰 실시간 조절해 주는 보조 함수
    private void ApplySfxVolumeDucking()
    {
        if (sfxSource.isPlaying)
        {
            // 기본 볼륨 1.0 기준으로 배율 적용 (필요에 따라 기존 볼륨과의 비례식으로 확장 가능)
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
}