using System;
using UnityEngine;

public class DontDestroyOnLoad : MonoBehaviour
{
   private static DontDestroyOnLoad instance;
   private void Awake()
   {
      if (instance)
      {
         // DebugUtil.DevelopmentLog($"[Singleton] {gameObject.name} is already exist. Destroyed.");
         Destroy(gameObject);
         return;
      }

      instance = this;
      DontDestroyOnLoad(gameObject);
   }
}
