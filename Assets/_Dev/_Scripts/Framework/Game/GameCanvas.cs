using System;
using UnityEngine;
using UnityEngine.UI;

public class GameCanvas : MonoBehaviour
{
    [SerializeField] private bool isGlobalCanvas;
    private void Start()
    {
        CanvasScaler canvasScaler = GetComponent<CanvasScaler>();
        if(canvasScaler)
            GameManager.Instance.ResolutionSystem.RegisterCanvasScaler(canvasScaler, isGlobalCanvas);
    }
}
