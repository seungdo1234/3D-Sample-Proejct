using UnityEngine;

public class GameManager : Singleton<GameManager>
{
   public GameResolutionSystem ResolutionSystem { get; private set; }
   
   protected override void OnSingletonInitialized()
   {
      base.OnSingletonInitialized();
      
      ResolutionSystem = GetComponent<GameResolutionSystem>();
   }
}
