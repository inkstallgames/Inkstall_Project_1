using UnityEngine;

namespace StarterAssets
{
    public class UICanvasControllerInput : MonoBehaviour
    {

        [Header("Output")]
        public StarterAssetsInputs starterAssetsInputs;

        public void VirtualMoveInput(Vector2 virtualMoveDirection)
        {
            starterAssetsInputs.virtualMoveInput = virtualMoveDirection;
        }

        public void VirtualLookInput(Vector2 virtualLookDirection)
        {
            starterAssetsInputs.virtualLookInput = virtualLookDirection;
        }

        public void VirtualJumpInput(bool virtualJumpState)
        {
            starterAssetsInputs.virtualJumpInput = virtualJumpState;
        }

        public void VirtualSprintInput(bool virtualSprintState)
        {
            starterAssetsInputs.virtualSprintInput = virtualSprintState;
        }
        
    }

}
