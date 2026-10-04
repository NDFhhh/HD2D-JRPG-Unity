using UnityEngine.Rendering;

public class BattleHitPostFX : MonoBehaviour
{
     #region 序列化参数与运行时状态

     [Header("Volume")]
     [SerializeField] private Volume breakVolume;
     [SerializeField] private bool disableWhenIdle = true;

     [Header("Flash")] [Range(0f, 1f)] 
     [SerializeField] private float flashWeight = 1f;
     [Min(0f)]
     [SerializeField] private float flashDuration = 0.06f;
     
     // 当前正在播放的短闪流程
     // 这样连续破盾时，可以先停止上一条再重新闪一次
     private Coroutine _playRoutine;

     #endregion

     private void Awake()
     {
          ResetVolume();
     }

     public void Play()
     {
          // 1. 先停掉上一段，避免连续破盾时把停留时间拖长
          if (_playRoutine != null)
          {
               StopCoroutine(_playRoutine);
               _playRoutine = null;
          }
          ResetVolume();
          
          // 2. 直接把 Break Volume 顶到目标权重
          breakVolume.enabled = true;
          breakVolume.weight = flashWeight;
          
          // 3. 保持一个极短时间后立刻关掉，画面上就是 “闪一下”
          if (flashDuration <= 0f)
          {
               ResetVolume();
               return;
          }
          
          _playRoutine = StartCoroutine(FlashOnce());
     }

     private void ResetVolume()
     {
          breakVolume.weight = 0f;
          if (disableWhenIdle)
               breakVolume.enabled = false;
     }

     private IEnumerator FlashOnce()
     {
          yield return new WaitForSecondsRealtime(flashDuration);
          ResetVolume();
          _playRoutine = null;
     }
}
