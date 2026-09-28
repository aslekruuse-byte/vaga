using BepInEx;
using ExitGames.Client.Photon;
using GorillaLocomotion;
using GorillaNetworking;
using HarmonyLib;
using Oculus.Platform;
using Photon.Pun;
using Photon.Realtime;
using PlayFab.DataModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Technie.PhysicsCreator;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Windows;
using UnityEngine.XR;
using Valve.VR;
using Vaga.Classes;
using Vaga.Menu;
using Vaga.Notifications;
using static Unity.Burst.Intrinsics.X86.Avx;
using Application = UnityEngine.Application;
using Image = UnityEngine.UI.Image;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using Text = UnityEngine.UI.Text;

namespace Vaga.Mods
{
    public class Movement
    {
        public static Vector3 OldMousePosition;
        private static Vector3 pos = Vector3.zero;
        public static float flySpeed = 15f;
        public static void Fly()
        {
            if (ControllerInputPoller.instance.rightControllerPrimaryButton)
            {
                GTPlayer.Instance.transform.position += GorillaTagger.Instance.headCollider.transform.forward * Time.deltaTime * flySpeed;
                GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;
            }
        }
        public static GameObject platL, platR;
        public static int platMode = 1;
        public static int platInput = 0;

        public static void VagaFly()
        {
            if (ControllerInputPoller.instance.rightGrab || ControllerInputPoller.instance.leftGrab)
            {
                GTPlayer.Instance.transform.position += GorillaTagger.Instance.headCollider.transform.forward * Time.deltaTime * 15f;
                GTPlayer.Instance.AddComponent<Rigidbody>().linearVelocity = Vector3.zero;
            }
            if (ControllerInputPoller.instance.rightGrab && ControllerInputPoller.instance.leftGrab)
            {
                GTPlayer.Instance.transform.position += GorillaTagger.Instance.headCollider.transform.forward * Time.deltaTime * 40f;
                GTPlayer.Instance.AddComponent<Rigidbody>().linearVelocity = Vector3.zero;
            }
        }

        public static void Platforms()
        {
            bool leftInput = (platInput == 0) ? ControllerInputPoller.instance.leftGrab : ControllerInputPoller.instance.leftControllerTriggerButton;
            bool rightInput = (platInput == 0) ? ControllerInputPoller.instance.rightGrab : ControllerInputPoller.instance.rightControllerTriggerButton;
            if (leftInput)
            {
                if (platL == null)
                {
                    platL = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    platL.transform.localScale = new Vector3(0.025f, 0.3f, 0.4f);
                    platL.transform.position = GorillaTagger.Instance.leftHandTransform.position;
                    platL.transform.rotation = GorillaTagger.Instance.leftHandTransform.rotation;
                    platL.GetComponent<Renderer>().material.shader = Shader.Find("Sprites/Default");
                }
                Renderer rendL = platL.GetComponent<Renderer>();
                if (platMode == 0) rendL.enabled = false;
                else
                {
                    rendL.enabled = true;
                    rendL.material.color = GunLib.LineColor;
                }
            }
            else if (platL != null) { UnityEngine.Object.Destroy(platL); platL = null; }
            if (rightInput)
            {
                if (platR == null)
                {
                    platR = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    platR.transform.localScale = new Vector3(0.025f, 0.3f, 0.4f);
                    platR.transform.position = GorillaTagger.Instance.rightHandTransform.position;
                    platR.transform.rotation = GorillaTagger.Instance.rightHandTransform.rotation;
                    platR.GetComponent<Renderer>().material.shader = Shader.Find("Sprites/Default");
                }
                Renderer rendR = platR.GetComponent<Renderer>();
                if (platMode == 0) rendR.enabled = false;
                else
                {
                    rendR.enabled = true;
                    rendR.material.color = GunLib.LineColor;
                }
            }
            else if (platR != null) { UnityEngine.Object.Destroy(platR); platR = null; }
        }

        private static Vector3 wasdVelocity = Vector3.zero;
        public static float wasdFlySpeed = 15f;
        public static float wasdVerticalSpeed = 10f;
        public static float wasdSmooth = 18f;
        public static bool wasdActive;

        public static void WASDFly()
        {
            // Desktop / link PC keyboard fly relative to head look
            Transform head = null;
            try
            {
                if (GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null)
                    head = GorillaTagger.Instance.headCollider.transform;
            }
            catch { }
            if (head == null && Camera.main != null)
                head = Camera.main.transform;
            if (head == null) return;

            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = head.right;
            right.y = 0f;
            right.Normalize();

            Vector3 wish = Vector3.zero;
            if (UnityEngine.Input.GetKey(KeyCode.W)) wish += forward;
            if (UnityEngine.Input.GetKey(KeyCode.S)) wish -= forward;
            if (UnityEngine.Input.GetKey(KeyCode.A)) wish -= right;
            if (UnityEngine.Input.GetKey(KeyCode.D)) wish += right;
            if (UnityEngine.Input.GetKey(KeyCode.Space)) wish += Vector3.up;
            if (UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.C) || UnityEngine.Input.GetKey(KeyCode.LeftAlt))
                wish -= Vector3.up;

            bool moving = wish.sqrMagnitude > 0.001f;
            if (moving) wish.Normalize();

            float speed = UnityEngine.Input.GetKey(KeyCode.LeftShift) ? wasdFlySpeed * 2.2f : wasdFlySpeed;
            Vector3 target = new Vector3(wish.x * speed, 0f, wish.z * speed);
            if (UnityEngine.Input.GetKey(KeyCode.Space))
                target.y = wasdVerticalSpeed * (UnityEngine.Input.GetKey(KeyCode.LeftShift) ? 1.8f : 1f);
            else if (UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.C) || UnityEngine.Input.GetKey(KeyCode.LeftAlt))
                target.y = -wasdVerticalSpeed * (UnityEngine.Input.GetKey(KeyCode.LeftShift) ? 1.8f : 1f);

            float lerp = 1f - Mathf.Exp(-wasdSmooth * Time.unscaledDeltaTime);
            wasdVelocity = Vector3.Lerp(wasdVelocity, target, lerp);

            // Primary: move GTPlayer + zero loco velocity so game doesn't fight us
            try
            {
                var gp = GTPlayer.Instance;
                if (gp != null)
                {
                    Vector3 delta = wasdVelocity * Time.unscaledDeltaTime;
                    gp.transform.position += delta;
                    try { gp.GetComponent<Rigidbody>().linearVelocity = Vector3.zero; } catch { }
                    try { gp.GetComponent<Rigidbody>().velocity = Vector3.zero; } catch { }
                }
            }
            catch { }

            // Also push offline rig / tagger body so networking follows
            try
            {
                if (GorillaTagger.Instance != null)
                {
                    if (GorillaTagger.Instance.rigidbody != null)
                    {
                        GorillaTagger.Instance.rigidbody.useGravity = moving || wasdVelocity.sqrMagnitude > 0.05f ? false : GorillaTagger.Instance.rigidbody.useGravity;
                        GorillaTagger.Instance.rigidbody.linearVelocity = wasdVelocity;
                    }
                    if (GorillaTagger.Instance.offlineVRRig != null)
                        GorillaTagger.Instance.offlineVRRig.transform.position += wasdVelocity * Time.unscaledDeltaTime;
                }
            }
            catch { }

            wasdActive = moving || wasdVelocity.sqrMagnitude > 0.01f;
        }


    }
}
