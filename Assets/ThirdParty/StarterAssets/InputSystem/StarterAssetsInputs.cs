using UnityEngine;

#if ENABLE_INPUT_SYSTEM

using UnityEngine.InputSystem;

#endif



namespace StarterAssets

{

	public class StarterAssetsInputs : MonoBehaviour

	{
		// Frame-skip counter: when > 0 we suppress look input to absorb cursor-lock delta spikes
		private int _skipLookFrames = 0;


		[Header("Character Input Values")]

		public Vector2 move;

		public Vector2 look;

		public bool jump;

		public bool sprint;



		[Header("Movement Settings")]

		public bool analogMovement;



		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = false;
		public bool cursorInputForLook = true;
		
		[Header("Touch Tracking")]
		[HideInInspector] public bool isTouchLook = false;

		[Header("Virtual Inputs")]
		[HideInInspector] public Vector2 virtualMoveInput;
		[HideInInspector] public Vector2 virtualLookInput;
		[HideInInspector] public bool virtualJumpInput;
		[HideInInspector] public bool virtualSprintInput;

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

			


#endif

		}

		

		private void OnDisable()

		{

#if ENABLE_INPUT_SYSTEM

			moveAction?.Disable();

			lookAction?.Disable();

			jumpAction?.Disable();

			sprintAction?.Disable();

			


#endif

		}

		

		private void Update()

		{

#if ENABLE_INPUT_SYSTEM

			if (!enabled) return;

			

			// Check for joystick input first (mobile/touch), then fall back to keyboard

			Vector2 newMove;

			bool usingJoystick = false;

			if (NetworkJoystickControl.Instance != null && NetworkJoystickControl.Instance.MovementInput.sqrMagnitude > 0.01f)

			{

				// Use joystick input when available and active

				Vector2 raw = NetworkJoystickControl.Instance.MovementInput;

				newMove = new Vector2(raw.x, raw.y);

				usingJoystick = true;

			}

			else
			{
				// Fall back to keyboard input or UI canvas virtual joystick
				Vector2 kbMove = moveAction.ReadValue<Vector2>();
				if (virtualMoveInput != Vector2.zero)
				{
					newMove = virtualMoveInput;
				}
				else
				{
					newMove = kbMove;
				}
			}
			
			MoveInput(newMove);

			if (newMove.sqrMagnitude > 0.01f && newMove != move)

			{

				// Debug.Log($"[StarterAssetsInputs] Direct input - move: {move} on GameObject: {gameObject.name}"); // DISABLED - Performance killer

			}

			

			// --- Menu guard: suppress look input while settings or the HUD editor is open ---
			bool settingsActive = NetworkUIManager.Instance != null && NetworkUIManager.Instance.IsGameplayInputBlocked;
			if (settingsActive)
			{
				// Zero-out look so the camera never rotates while the player is in settings
				LookInput(Vector2.zero);
				isTouchLook = false;
				// Schedule a skip so the first frame after closing absorbs any delta spike
				_skipLookFrames = 2;
			}
			else if (_skipLookFrames > 0)
			{
				// Absorb the cursor-lock / touch delta spike right after settings closes
				_skipLookFrames--;
				LookInput(Vector2.zero);
				isTouchLook = false;
			}
			else if (cursorInputForLook)
			{
				Vector2 newLook = Vector2.zero;
				isTouchLook = false;

				// PC: right-click drag to look
				var mouse = Mouse.current;
				if (mouse != null && mouse.rightButton.isPressed)
				{
					newLook = mouse.delta.ReadValue();
				}

				// Android / Mobile: touch drag on the RIGHT half of screen controls camera.
				// Left half is reserved for the movement joystick.
				// Uses Input System Touchscreen only (no legacy Input.touches / TouchPhase).
				var touchscreen = Touchscreen.current;
				if (touchscreen != null && newLook == Vector2.zero)
				{
					for (int i = 0; i < touchscreen.touches.Count; i++)
					{
						var touch = touchscreen.touches[i];
						if (!touch.isInProgress) continue;

						Vector2 startPos = touch.startPosition.ReadValue();
						if (startPos.x <= Screen.width * 0.5f) continue;

						newLook = touch.delta.ReadValue();
						isTouchLook = true;
						break;
					}
				}

				if (virtualLookInput != Vector2.zero)
				{
					newLook = virtualLookInput;
				}

				LookInput(newLook);

			}

			

			bool newJump = jumpAction.IsPressed();
			
			// Fallback to UI Button for mobile
			if (NetworkUIManager.Instance != null && NetworkUIManager.Instance.IsJumpHeld)
			{
				newJump = true;
			}
			if (virtualJumpInput)
			{
				newJump = true;
			}
			
			JumpInput(newJump);

			

			// Sprint: use joystick sprint state if joystick is active, otherwise use keyboard

			bool newSprint;

			if (usingJoystick)

			{

				newSprint = NetworkJoystickControl.Instance.IsSprinting;

			}

			else

			{
				newSprint = sprintAction.IsPressed() || virtualSprintInput;
			}
			SprintInput(newSprint);

#endif

		}



#if ENABLE_INPUT_SYSTEM

		// Keep these for backward compatibility with PlayerInput if it exists

		public void OnMove(InputValue value)

		{

			// Only process input if this component is enabled

			if (!enabled) return;

			

			MoveInput(value.Get<Vector2>());

			// Debug.Log($"[StarterAssetsInputs] OnMove: {move} on GameObject: {gameObject.name}, enabled: {enabled}");

		}



		public void OnLook(InputValue value)

		{

			// Only process input if this component is enabled

			if (!enabled) return;

			

			if (cursorInputForLook)

			{

				// Only rotate camera while right mouse button is held (PC path)
				var mouse = Mouse.current;
				if (mouse != null && mouse.rightButton.isPressed)
				{
					LookInput(value.Get<Vector2>());
				}
				else
				{
					LookInput(Vector2.zero);
				}

			}

		}



		public void OnJump(InputValue value)

		{

			// Only process input if this component is enabled

			if (!enabled) return;

			

			JumpInput(value.isPressed);

			// Debug.Log($"[StarterAssetsInputs] OnJump: {jump}"); // DISABLED - Performance killer

		}



		public void OnSprint(InputValue value)

		{

			// Only process input if this component is enabled

			if (!enabled) return;

			

			SprintInput(value.isPressed);

			// Debug.Log($"[StarterAssetsInputs] OnSprint: {sprint}"); // DISABLED - Performance killer

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

			// Keep cursor unlocked for UI interaction (throw button, settings, etc.)

			SetCursorState(false);

		}



		private void SetCursorState(bool newState)

		{

			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;

			Cursor.visible = !newState; // Show cursor when unlocked, hide when locked

		}

	}

	

}