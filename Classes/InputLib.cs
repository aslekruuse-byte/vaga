using BepInEx;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Valve.VR;

#nullable disable
namespace Vaga.Menu
{
    public class InputLib : BaseUnityPlugin
    {
        private const float JoystickDeadzone = 0.15f;
        public static bool EmulatedA;
        public static bool EmulatedB;
        public static bool EmulatedX;
        public static bool EmulatedY;
        public static bool EmulatedLeftGrip;
        public static bool EmulatedRightGrip;
        public static bool EmulatedLeftTrigger;
        public static bool EmulatedRightTrigger;
        private object lcf6285;
        private object l072f5db;
        private static object Xml;
        private static object Linq;
        private static object Data;
        private float T6B2073A;
        private char JA03D5F9;
        private byte oeb9db5a;
        private char s9f51e53;
        private int ud2de2f;

        public static bool RightTrigger()
        {
            return ControllerInputPoller.instance.rightControllerTriggerButton || InputLib.EmulatedRightTrigger;
        }

        public static bool LeftTrigger()
        {
            return ControllerInputPoller.instance.leftControllerTriggerButton || InputLib.EmulatedLeftTrigger;
        }

        public static bool RightGrip()
        {
            return ControllerInputPoller.instance.rightGrab || InputLib.EmulatedRightGrip;
        }

        public static bool LeftGrip()
        {
            return ControllerInputPoller.instance.leftGrab || InputLib.EmulatedLeftGrip;
        }

        public static float RightTriggerFloat() => ControllerInputPoller.TriggerFloat((XRNode)5);

        public static float LeftTriggerFloat() => ControllerInputPoller.TriggerFloat((XRNode)4);

        public static float RightGripFloat() => ControllerInputPoller.GripFloat((XRNode)5);

        public static float LeftGripFloat() => ControllerInputPoller.GripFloat((XRNode)4);

        public static bool RightPrimaryButton()
        {
            return ControllerInputPoller.instance.rightControllerPrimaryButton || InputLib.EmulatedA;
        }

        public static bool LeftPrimaryButton()
        {
            return ControllerInputPoller.instance.leftControllerPrimaryButton || InputLib.EmulatedX;
        }

        public static bool RightSecondaryButton()
        {
            return ControllerInputPoller.instance.rightControllerSecondaryButton || InputLib.EmulatedB;
        }

        public static bool LeftSecondaryButton()
        {
            return ControllerInputPoller.instance.leftControllerSecondaryButton || InputLib.EmulatedY;
        }

        public static bool LeftJoystickClick()
        {
            return SteamVR_Actions.gorillaTag_LeftJoystickClick.GetState((SteamVR_Input_Sources)1);
        }

        public static bool RightJoystickClick()
        {
            return SteamVR_Actions.gorillaTag_LeftJoystickClick.GetState((SteamVR_Input_Sources)2);
        }

        public static bool AnyTrigger() => InputLib.RightTrigger() || InputLib.LeftTrigger();

        public static bool AnyMouseClick()
        {
            return InputLib.MouseLeft() || InputLib.MouseRight() || InputLib.MouseMiddle();
        }

        public static bool AnyGrip() => InputLib.RightGrip() || InputLib.LeftGrip();

        public static bool AnyPrimaryButton()
        {
            return InputLib.RightPrimaryButton() || InputLib.LeftPrimaryButton();
        }

        public static bool AnySecondaryButton()
        {
            return InputLib.RightSecondaryButton() || InputLib.LeftSecondaryButton();
        }

        public static bool MouseLeft() => Mouse.current != null && Mouse.current.leftButton.isPressed;

        public static bool MouseRight() => Mouse.current != null && Mouse.current.rightButton.isPressed;

        public static bool MouseMiddle() => Mouse.current != null && Mouse.current.middleButton.isPressed;

        public static bool MouseLeftDown()
        {
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        public static bool MouseRightDown()
        {
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        }

        public static bool MouseMiddleDown()
        {
            return Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;
        }

        public static Vector2 MouseDelta()
        {
            return Mouse.current == null ? Vector2.zero : ((InputControl<Vector2>)((Pointer)Mouse.current).delta).ReadValue();
        }

        public static bool RightTriggerOrMouse() => InputLib.RightTrigger() || InputLib.MouseLeft();

        public static bool RightGripOrMouseRight() => InputLib.RightGrip() || InputLib.MouseRight();

        public static bool LeftTriggerOrMouseMiddle() => InputLib.LeftTrigger() || InputLib.MouseMiddle();
    }

}

