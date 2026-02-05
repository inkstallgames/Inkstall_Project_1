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