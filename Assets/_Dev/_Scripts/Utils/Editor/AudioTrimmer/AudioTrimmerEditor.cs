using UnityEngine;
using UnityEditor;
using System.IO;

public class AudioTrimmerEditor : EditorWindow
{
    private AudioClip selectedClip;
    private float startTime = 0f;
    private float endTime = 1f;
    private bool showWaveform = true;
    private bool isPlaying = false;
    private float playbackPosition = 0f;

    [MenuItem("Tools/SD/Audio Trimmer Tool")]
    public static void ShowWindow()
    {
        GetWindow<AudioTrimmerEditor>("Audio Trimmer");
    }


    void OnGUI()
    {
        GUILayout.Label("Audio Trimmer Tool", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        // 오디오 클립 선택 필드
        selectedClip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", selectedClip, typeof(AudioClip), false);

        if (selectedClip != null)
        {
            // 트리밍 범위 설정
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Trim Range");
            startTime = EditorGUILayout.Slider(startTime, 0f, selectedClip.length);
            endTime = EditorGUILayout.Slider(endTime, startTime, selectedClip.length);
            EditorGUILayout.EndHorizontal();

            // 파형 표시 옵션
            showWaveform = EditorGUILayout.Toggle("Show Waveform", showWaveform);

            // 파형 렌더링 (if showWaveform is true)
            if (showWaveform)
            {
                DrawWaveform();
            }

            // 재생 버튼들
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Play Original"))
            {
                PlayOriginalClip();
            }

            if (GUILayout.Button("Play Trimmed"))
            {
                PlayTrimmedClip();
            }

            if (GUILayout.Button("Stop"))
            {
                StopPlayback();
            }

            EditorGUILayout.EndHorizontal();

            // 저장 버튼
            if (GUILayout.Button("Save Trimmed Clip"))
            {
                SaveTrimmedClip();
            }
        }
    }

    private void DrawWaveform()
    {
        if (selectedClip == null) return;

        float[] samples = new float[selectedClip.samples * selectedClip.channels];
        selectedClip.GetData(samples, 0);

        // 파형을 그릴 영역 설정
        Rect waveformRect = EditorGUILayout.GetControlRect(GUILayout.Height(100));

        // 배경 그리기
        EditorGUI.DrawRect(waveformRect, new Color(0.2f, 0.2f, 0.2f));

        // 트림 범위 하이라이트 영역 그리기
        Rect trimRect = new Rect(
            waveformRect.x + (startTime / selectedClip.length) * waveformRect.width,
            waveformRect.y,
            ((endTime - startTime) / selectedClip.length) * waveformRect.width,
            waveformRect.height
        );
        EditorGUI.DrawRect(trimRect, new Color(0.4f, 0.4f, 0.8f, 0.3f));

        // 파형 그리기
        int sampleStep = Mathf.Max(1, samples.Length / (int)waveformRect.width);
        Color waveColor = new Color(1f, 1f, 1f, 0.8f);

        for (int i = 0; i < waveformRect.width; i++)
        {
            int sampleIndex = (int)(i * sampleStep);
            if (sampleIndex >= samples.Length) break;

            float sampleValue = Mathf.Abs(samples[sampleIndex]);

            // 최대값 계산 (작은 범위의 샘플에서)
            for (int j = 0; j < sampleStep && sampleIndex + j < samples.Length; j++)
            {
                sampleValue = Mathf.Max(sampleValue, Mathf.Abs(samples[sampleIndex + j]));
            }

            float height = sampleValue * waveformRect.height * 0.5f;

            // 파형 라인 그리기
            Rect lineRect = new Rect(
                waveformRect.x + i,
                waveformRect.y + (waveformRect.height / 2) - height,
                1,
                height * 2
            );

            EditorGUI.DrawRect(lineRect, waveColor);
        }

        // 재생 위치 그리기
        if (isPlaying)
        {
            float playheadX = waveformRect.x + (playbackPosition / selectedClip.length) * waveformRect.width;
            Rect playheadRect = new Rect(playheadX, waveformRect.y, 2, waveformRect.height);
            EditorGUI.DrawRect(playheadRect, Color.red);

            // 재생 위치 업데이트 (에디터 창 갱신을 위해)
            Repaint();
        }
    }

    private AudioSource previewAudioSource;

    private void PlayOriginalClip()
    {
        if (selectedClip == null) return;

        InitAudioSource();
        previewAudioSource.clip = selectedClip;
        previewAudioSource.time = 0;
        previewAudioSource.Play();
        isPlaying = true;

        EditorApplication.update += UpdatePlayback;
    }

    private void PlayTrimmedClip()
    {
        if (selectedClip == null) return;

        InitAudioSource();
        previewAudioSource.clip = selectedClip;
        previewAudioSource.time = startTime;
        previewAudioSource.Play();
        isPlaying = true;
        playbackPosition = startTime;

        EditorApplication.update += UpdatePlayback;
    }

    private void StopPlayback()
    {
        if (previewAudioSource != null && previewAudioSource.isPlaying)
        {
            previewAudioSource.Stop();
        }

        isPlaying = false;
        EditorApplication.update -= UpdatePlayback;
    }

    private void UpdatePlayback()
    {
        if (previewAudioSource == null || !previewAudioSource.isPlaying)
        {
            isPlaying = false;
            EditorApplication.update -= UpdatePlayback;
            return;
        }

        playbackPosition = previewAudioSource.time;

        // 트림된 영역 재생 중일 때 endTime에 도달하면 재생 중지
        if (playbackPosition >= endTime)
        {
            StopPlayback();
        }

        Repaint();
    }

    private void InitAudioSource()
    {
        if (previewAudioSource == null)
        {
            // 임시 게임 오브젝트 생성
            GameObject go = new GameObject("AudioPreview");
            go.hideFlags = HideFlags.HideAndDontSave;
            previewAudioSource = go.AddComponent<AudioSource>();
        }
    }

    private void OnDestroy()
    {
        StopPlayback();

        if (previewAudioSource != null)
        {
            DestroyImmediate(previewAudioSource.gameObject);
        }
    }


private void SaveTrimmedClip()
{
    if (selectedClip == null) return;
    
    // 원본 오디오 데이터 가져오기
    float[] origSamples = new float[selectedClip.samples * selectedClip.channels];
    selectedClip.GetData(origSamples, 0);
    
    // 트리밍된 부분의 시작/끝 샘플 인덱스 계산
    int startSample = Mathf.FloorToInt(startTime * selectedClip.frequency) * selectedClip.channels;
    int endSample = Mathf.CeilToInt(endTime * selectedClip.frequency) * selectedClip.channels;
    int trimmedSampleCount = endSample - startSample;
    
    // 트리밍된 오디오 데이터 복사
    float[] trimmedSamples = new float[trimmedSampleCount];
    System.Array.Copy(origSamples, startSample, trimmedSamples, 0, trimmedSampleCount);
    
    // 새 AudioClip 생성
    AudioClip trimmedClip = AudioClip.Create(
        selectedClip.name + "_trimmed",
        trimmedSampleCount / selectedClip.channels,
        selectedClip.channels,
        selectedClip.frequency,
        false
    );
    
    trimmedClip.SetData(trimmedSamples, 0);
    
    // 원본 파일의 경로와 확장자 가져오기
    string originalPath = AssetDatabase.GetAssetPath(selectedClip);
    string originalExtension = Path.GetExtension(originalPath).ToLower();
    
    // 저장 경로 요청 (원본과 동일한 확장자 사용)
    string defaultFileName = selectedClip.name + "_trimmed" + originalExtension;
    string path = EditorUtility.SaveFilePanelInProject(
        "Save Trimmed Audio",
        defaultFileName,
        originalExtension.Replace(".", ""), // 점(.) 제거
        "Save trimmed audio clip as..."
    );
    
    if (string.IsNullOrEmpty(path)) return;
    
    // 확장자 일치 여부 확인
    if (Path.GetExtension(path).ToLower() != originalExtension)
    {
        path = Path.ChangeExtension(path, originalExtension);
    }
    
    // WAV로 먼저 저장 (Unity는 내부적으로 이 과정이 필요함)
    SaveWav.Save(path, trimmedClip);
    
    AssetDatabase.ImportAsset(path);
    AssetDatabase.Refresh();
    
    // 저장된 에셋 선택
    AudioClip savedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    Selection.activeObject = savedClip;
    EditorGUIUtility.PingObject(savedClip);
    
    Debug.Log("Audio clip successfully trimmed and saved to: " + path);
}

}