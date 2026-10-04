
using MFramework.Event;

public class PlayerController : MonoBehaviour,
    IEventReceiver<ChooseCharacterButtonOnClickEvent>
{
    private CharacterController characterController;
    private Animator animator;
    
    private CharacterIdentity characterIdentity;
    private CharacterDefinition_SO characterDefinition;
    
    [SerializeField] private float speed = 5f;
    private float verticalVelocity;

    [Header("Footstep Audio")]
    [Tooltip("脚步音效触发间隔（秒），按移动节奏调节")]
    [SerializeField] private float footstepInterval = 0.2f;
    private float _footstepTimer;
    
    [Header("Gravity")]
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float groundSnapForce = -2f;
    [SerializeField] private float maxVelocity = -40f;
    
    private Vector2 movementInput;
    
    private static readonly int IsMoving = Animator.StringToHash("isMoving");
    private static readonly int MoveX = Animator.StringToHash("moveX");
    private static readonly int MoveY = Animator.StringToHash("moveY");
    
    private bool isMoving;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<ChooseCharacterButtonOnClickEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ChooseCharacterButtonOnClickEvent>(this);
    }

    public void OnEvent(ChooseCharacterButtonOnClickEvent evt)
    {
        characterIdentity =  evt.CharacterIdentity;
        characterDefinition = evt.CharacterIdentity.CharacterDefinition;
        
        animator = GetComponentInChildren<Animator>();
        animator.runtimeAnimatorController = characterDefinition.fieldAnimator;
    }
    
    private void Update()
    {
        Movement();
        FootstepTick();
        SetAnimation();
    }

    /// <summary>
    /// 脚步节拍器：地面移动时按间隔广播 FootstepEvent，音频系统订阅播放脚步声
    /// </summary>
    private void FootstepTick()
    {
        bool moving = movementInput.sqrMagnitude > 0.1f;

        if (!moving || !characterController.isGrounded)
        {
            _footstepTimer = 0f;
            return;
        }

        _footstepTimer += Time.deltaTime;
        if (_footstepTimer >= footstepInterval)
        {
            _footstepTimer = 0f;
            EventBus.Publish(new FootstepEvent(transform.position));
        }
    }
    
    private void Movement()
    {
        InputSystemController input = InputSystemController.Instance;
        
        if (input is null) return;

        movementInput = input.GetMovementInput();
        
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

        Vector3 velocity = new Vector3(movementInput.x, 0, movementInput.y) * speed;
        velocity.y = verticalVelocity;
        
        characterController.Move(velocity * Time.deltaTime);
    }

    private void SetAnimation()
    {
        if (animator is null) return;

        isMoving = movementInput.magnitude > 0.1f;
        animator.SetBool(IsMoving, isMoving);

        if (isMoving)
        {
            animator.SetFloat(MoveX, movementInput.x);
            animator.SetFloat(MoveY, movementInput.y);
        }
    }
}
