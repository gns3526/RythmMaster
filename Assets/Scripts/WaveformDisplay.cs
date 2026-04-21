using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 오디오 파형(Waveform)을 시각적으로 표시하는 컴포넌트
/// 우측 패널에 세로로 표시되며, 클릭 시 해당 시간으로 점프합니다.
/// 
/// Unity 설정:
/// 1. Canvas에 RawImage 오브젝트 생성 (이름: "WaveformDisplay")
/// 2. 이 스크립트를 해당 오브젝트에 추가
/// 3. RawImage 컴포넌트가 자동으로 연결됨
/// 4. positionIndicator에 현재 위치 표시용 Image 할당
/// </summary>
public class WaveformDisplay : MonoBehaviour, IPointerClickHandler
{
    static WaveformDisplay instance;
    public static WaveformDisplay Instance => instance;

    [Header("Settings")]
    public int textureWidth = 128;
    public int textureHeight = 2048;
    public Color waveformColor = new Color(0.3f, 0.8f, 1f, 1f);
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f);
    public Color positionColor = Color.red;
    public Color beatLineColor = new Color(1f, 1f, 1f, 0.3f);

    [Header("References")]
    public RectTransform positionIndicator;

    RawImage rawImage;
    Texture2D waveformTexture;
    RectTransform rectTransform;

    float[] samples;
    float audioLength;
    bool isGenerated = false;

    void Awake()
    {
        if (instance == null)
            instance = this;

        rawImage = GetComponent<RawImage>();
        rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// 파형 생성 초기화. 에디터 진입 시 호출.
    /// </summary>
    public void Init()
    {
        AudioClip clip = AudioManager.Instance.audioSource.clip;
        if (clip == null)
        {
            Debug.LogWarning("WaveformDisplay: AudioClip이 없습니다.");
            return;
        }

        audioLength = clip.length;

        // 샘플 데이터 추출
        samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        GenerateWaveform(clip.channels);
        isGenerated = true;
    }

    /// <summary>
    /// 파형 텍스처 생성
    /// </summary>
    void GenerateWaveform(int channels)
    {
        waveformTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        waveformTexture.filterMode = FilterMode.Bilinear;

        // 배경 채우기
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = backgroundColor;

        int samplesPerPixel = samples.Length / textureHeight;
        int halfWidth = textureWidth / 2;

        // BPM 기반 비트라인 계산
        Sheet sheet = null;
        if (GameManager.Instance.sheets.ContainsKey(GameManager.Instance.title))
            sheet = GameManager.Instance.sheets[GameManager.Instance.title];

        for (int y = 0; y < textureHeight; y++)
        {
            // 해당 y 위치의 시간 계산
            float timeAtY = (float)y / textureHeight * audioLength;

            // 비트라인 표시
            if (sheet != null)
            {
                float barDuration = sheet.BarPerSec;
                if (barDuration > 0)
                {
                    float barPosition = timeAtY / barDuration;
                    float barFraction = barPosition - Mathf.Floor(barPosition);
                    if (barFraction < 0.01f) // 마디 시작점
                    {
                        for (int x = 0; x < textureWidth; x++)
                            pixels[y * textureWidth + x] = beatLineColor;
                        continue;
                    }
                }
            }

            // 파형 그리기 - 해당 구간의 최대 진폭 계산
            int startSample = y * samplesPerPixel;
            int endSample = Mathf.Min(startSample + samplesPerPixel, samples.Length);

            float maxAmplitude = 0f;
            for (int i = startSample; i < endSample; i += channels)
            {
                float abs = Mathf.Abs(samples[i]);
                if (abs > maxAmplitude)
                    maxAmplitude = abs;
            }

            // 진폭을 픽셀 너비로 변환
            int waveWidth = Mathf.CeilToInt(maxAmplitude * halfWidth);

            for (int x = halfWidth - waveWidth; x < halfWidth + waveWidth; x++)
            {
                if (x >= 0 && x < textureWidth)
                {
                    // 중앙에서 멀수록 투명해지는 그라데이션
                    float dist = Mathf.Abs(x - halfWidth) / (float)waveWidth;
                    Color col = Color.Lerp(waveformColor, waveformColor * 0.5f, dist);
                    pixels[y * textureWidth + x] = col;
                }
            }
        }

        waveformTexture.SetPixels(pixels);
        waveformTexture.Apply();

        rawImage.texture = waveformTexture;
    }

    void Update()
    {
        if (!isGenerated || audioLength <= 0f) return;

        // 현재 재생 위치 인디케이터 업데이트
        if (positionIndicator != null)
        {
            float progress = AudioManager.Instance.progressTime / audioLength;
            float height = rectTransform.rect.height;
            float yPos = -height * 0.5f + height * progress;
            positionIndicator.anchoredPosition = new Vector2(0f, yPos);
        }
    }

    /// <summary>
    /// 파형 클릭 시 해당 시간으로 점프
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isGenerated || audioLength <= 0f) return;

        // 클릭 위치를 로컬 좌표로 변환
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, eventData.position, eventData.pressEventCamera, out localPoint);

        // 로컬 좌표를 0~1 비율로 변환 (아래가 0, 위가 1)
        float height = rectTransform.rect.height;
        float progress = (localPoint.y + height * 0.5f) / height;
        progress = Mathf.Clamp01(progress);

        // 해당 시간으로 점프
        float targetTime = progress * audioLength;
        AudioManager.Instance.progressTime = targetTime;

        // 에디터 오브젝트 위치도 동기화
        if (Editor.Instance != null)
        {
            Editor.Instance.CalculateCurrentBar();

            float barPerTime = GameManager.Instance.sheets[GameManager.Instance.title].BarPerSec;
            float pos = targetTime / barPerTime * 16;
            Editor.Instance.objects.transform.position = new Vector3(0f, -pos + Editor.Instance.offsetPosition, 0f);
        }
    }

    /// <summary>
    /// 정리
    /// </summary>
    public void Cleanup()
    {
        if (waveformTexture != null)
        {
            Destroy(waveformTexture);
            waveformTexture = null;
        }
        isGenerated = false;
        samples = null;
    }

    void OnDestroy()
    {
        Cleanup();
    }
}
