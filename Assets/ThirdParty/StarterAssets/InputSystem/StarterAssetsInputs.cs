using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
		private InputAction moveAction;
		private InputAction lookAction;
		private InputAction jumpAction;
		private InputAction sprintAction;
#endif

		private void OnEnable()
		{
#if ENABLE_INPUT_SYSTEM
			// Read input directly from the default keyboard/mouse
			if (moveAction == null)
			{
				moveAction = new InputAction("Move", InputActionType.Value);
				moveAction.AddCompositeBinding("2DVector")
					.With("Up", "<Keyboard>/w")
					.With("Down", "<Keyboard>/s")
					.With("Left", "<Keyboard>/a")
					.With("Right", "<Keyboard>/d");
			}
			
			if (lookAction == null)
			{
				lookAction = new InputAction("Look", InputActionType.Value);
				lookAction.AddBinding("<Mouse>/delta");
			}
			
			if (jumpAction == null)
			{
				jumpAction = new InputAction("Jump", InputActionType.Button);
				jumpAction.AddBinding("<Keyboard>/space");
			}
			
			if (sprintAction == null)
			{
				sprintAction = new InputAction("Sprint", InputActionType.Button);
				sprintAction.AddBinding("<Keyboard>/leftShift");
			}
			
			moveAction.Enable();
			lookAction.Enable();
			jumpAction.Enable();
			sprintAction.Enable();
			
			Debug.Log($"[StarterAssetsInputs] Input actions enabled for {gameObject.name}");
#endif
		}
		
		private void OnDisable()
		{
#if ENABLE_INPUT_SYSTEM
			moveAction?.Disable();
			lookAction?.Disable();
			jumpAction?.Disable();
			sprintAction?.Disable();
			
			Debug.Log($"[StarterAssetsInputs] Input actions disabled for {gameObject.name}");
#endif
		}
		
		private void Update()
		{
#if ENABLE_INPUT_SYSTEM
			if (!enabled) return;
			
			// Read input directly from actions
			Vector2 newMove = moveAction.ReadValue<Vector2>();
			if (newMove != move)
			{
				MoveInput(newMove);
				if (newMove.sqrMagnitude > 0.01f)
				{
					Debug.Log($"[StarterAssetsInputs] Direct input - move: {move} on GameObject: {gameObject.name}");
				}
			}
			
			if (cursorInputForLook)
			{
				Vector2 newLook = lookAction.ReadValue<Vector2>();
				LookInput(newLook);
			}
			
			bool newJump = jumpAction.IsPressed();
			if (newJump != jump)
			{
				JumpInput(newJump);
			}
			
			bool newSprint = sprintAction.IsPressed();
			if (newSprint != sprint)
			{
				SprintInput(newSprint);
			}
#endif
		}

#if ENABLE_INPUT_SYSTEM
		// Keep these for backward compatibility with PlayerInput if it exists
		public void OnMove(InputValue value)
		{
			// Only process input if this component is enabled
			if (!enabled) return;
			
			MoveInput(value.Get<Vector2>());
			Debug.Log($"[StarterAssetsInputs] OnMove: {move} on GameObject: {gameObject.name}, enabled: {enabled}");
		}

		public void OnLook(InputValue value)
		{
			// Only process input if this component is enabled
			if (!enabled) return;
			
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			// Only process input if this component is enabled
			if (!enabled) return;
			
			JumpInput(value.isPressed);
			Debug.Log($"[StarterAssetsInputs] OnJump: {jump}");
		}

		public void OnSprint(InputValue value)
		{
			// Only process input if this component is enabled
			if (!enabled) return;
			
			SprintInput(value.isPressed);
			Debug.Log($"[StarterAssetsInputs] OnSprint: {sprint}");
		}
#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}

		private void OnDestroy()
		{
#if ENABLE_INPUT_SYSTEM
			moveAction?.Dispose();
			lookAction?.Dispose();
			jumpAction?.Dispose();
			sprintAction?.Dispose();
#endif
		}
		
		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}
	}
	
}