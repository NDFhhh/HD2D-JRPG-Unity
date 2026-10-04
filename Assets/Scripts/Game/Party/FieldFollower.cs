
public class FieldFollower : MonoBehaviour
{
     [SerializeField] private CharacterController characterController;
     [SerializeField] private Animator animator;
     
     [Header("Animator Params")]
     private static readonly int IsMoving = Animator.StringToHash("isMoving");
     private static readonly int MoveX = Animator.StringToHash("moveX");
     private static readonly int MoveY = Animator.StringToHash("moveY");

     [Header("最小移动阈值")] 
     [SerializeField] private float movementThreshold = 0.001f;
     
     [Header("Gravity")]
     [SerializeField] private float gravity = -25f;
     [SerializeField] private float groundSnapForce = -2f;
     [SerializeField] private float maxVelocity = -40f;
     private float verticalVelocity;

     #region 对外接口

     public void SetupFollower(CharacterDefinition_SO definition)
     {
          animator.runtimeAnimatorController = definition.fieldAnimator;
     }

     /// <summary>
     /// 将角色移动到目标位置
     /// </summary>
     /// <param name="targetPosition">目标位置</param>
     /// <param name="speed">移动速度</param>
     public void MoveTo(Vector3 targetPosition, float speed)
     {
          Vector3 toTarget = targetPosition - transform.position;
          toTarget.y = 0;

          // 计算水平方向上的移动步长，使用ClampMagnitude限制最大移动距离
          Vector3 horizontalStep = Vector3.ClampMagnitude(toTarget, Mathf.Max(0, speed) * Time.deltaTime);
          
          bool isGrounded = characterController.isGrounded;
          
          if (isGrounded && verticalVelocity < 0)
          {
               verticalVelocity = groundSnapForce;
          }
          else
          {
               verticalVelocity += gravity * Time.deltaTime;

               if (verticalVelocity < maxVelocity)
                    verticalVelocity = maxVelocity;
          }
          
          Vector3 movement = horizontalStep;
          movement.y = verticalVelocity * Time.deltaTime;
          
          characterController.Move(movement);
          UpdateAnimation(horizontalStep);    //更新动画
     }

     #endregion

     private void UpdateAnimation(Vector3 step)
     {
          bool isMoving = step.sqrMagnitude > movementThreshold * movementThreshold;
          animator.SetBool(IsMoving, isMoving);
          
          if (isMoving)
          {
               animator.SetFloat(MoveX, step.x);
               animator.SetFloat(MoveY, step.z);
          }
     }

     public void snapTo(Vector3 position)
     {
          bool enable = characterController.enabled;
          characterController.enabled = false;
          transform.position = position;
          characterController.enabled = enable;

          verticalVelocity = 0f;
     }

}
