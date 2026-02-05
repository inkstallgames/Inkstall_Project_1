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
		public InputAction moveAction;
		public InputAction lookAction;
		public InputAction jumpAction;
		public InputAction sprintAction;
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

		// Input is now read directly in ThirdPersonController.OnInput() callback
		// This ensures input is sampled at the same rate as network ticks for proper sync

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