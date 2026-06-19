using System;
using TMPro;
using UnityEngine;

public class ViewFPS : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fpsText;

    private static ViewFPS instance;
    private int frameCount;
    private float elapsedTime;
    private float updateInterval = 1f; // 1초마다 갱신

    private void Awake()
    {
// #if !DEVELOPMENT_BUILD && !UNITY_EDITOR
//         Destroy(gameObject);
//         return;
// #endif
        
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        frameCount++;
        elapsedTime += Time.unscaledDeltaTime; // 타임스케일 무시하고 측정

        if (elapsedTime >= updateInterval)
        {
            // FPS 계산
            float fps = frameCount / elapsedTime;

            // 텍스트 갱신
            fpsText.text = $"{fps:F1}fps";

            // 초기화
            frameCount = 0;
            elapsedTime = 0f;
        }
    }
}