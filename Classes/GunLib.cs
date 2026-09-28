using BepInEx;
using GorillaLocomotion;
using Wave;
using Oculus.Platform;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Vaga.Menu;
using Random = UnityEngine.Random;
using Vaga.Utilities;

namespace Vaga
{
    public class GunLib : MonoBehaviour
    {
        private void FixedUpdate()
        {
            LineColor = new Color(0.235f, 0f, 0.784f);
            PointerColor = new Color(0.235f, 0f, 0.784f);
        }
        public static void TestLib()
        {
            StartPointerSystem(() => 
            {


            }, false);
        }
        public enum GunLineStyle
        {
            Smooth,
            Straight,
            Wavy,
            DynamicPulse,
            Electric,
            Spiral,
            Throb,
            Helix,
            Flare,
            Zigzag,
            Meteor,
            Burst,
            Spiral1,
            Sparky,
            ElectricZag,
            WaterRipple,
            Webbed,
            BurstPulse,
            CoiledLine,
        }

        public static int LineCurve = 150;
        private const float LineSmoothFactor = 6f;
        private const float DestroyDelay = 0.02f;
        public static float PulseSpeed = 2f;
        public static float PulseAmplitude = 0.03f;
        public static GameObject spherepointer;
        public static VRRig LockedPlayer;
        public static bool isLocked;
        public static Vector3 lr;
        public static Color32 PointerColor = new Color(0.235f, 0f, 0.784f);
        public static Color32 LineColor = new Color(0.235f, 0f, 0.784f);
        public static GunLineStyle currentLineStyle = GunLineStyle.Straight;
        public static RaycastHit raycastHit;
        public static bool GunGrips => Mouse.current.rightButton.isPressed || ControllerInputPoller.instance.rightGrab;

        public static bool GunTriggers => Mouse.current.leftButton.isPressed || ControllerInputPoller.instance.rightControllerTriggerButton;
        public static Vector3 hitPosition { get; set; }
        public static float SphereSize = 0.04f;
        public static float GunLineWidth = 0.01f;
        private static int currentLineSize = 0;
        private static readonly float[] LineScales = { 0.001f, 0.002f, 0.003f, 0.004f, 0.005f, 0.006f, 0.007f, 0.008f, 0.009f, 0.01f, 0.011f, 0.012f, 0.013f, 0.014f, 0.015f, 0.016f, 0.017f, 0.018f, 0.019f, 0.02f };
        private static int currentSphereSize = 0;
        private static readonly float[] SphereScales = { 0.01f, 0.02f, 0.03f, 0.04f, 0.05f, 0.06f, 0.07f, 0.08f, 0.09f, 0.1f, 0.11f, 0.12f, 0.13f, 0.14f, 0.15f, 0.16f, 0.17f, 0.18f, 0.19f, 0.2f };

        private static GameObject _gunLineObject;
        private static LineRenderer _gunLineRenderer;
        private static Material _gunLineMaterial;
        private static Coroutine _gunLineCoroutine;
        private static MonoBehaviour _gunLineHost;

        private static LineRenderer GetOrCreateGunLine()
        {
            if (_gunLineRenderer == null || _gunLineObject == null)
            {
                if (_gunLineObject != null) GameObject.Destroy(_gunLineObject);
                _gunLineObject = new GameObject("GunLine_Persistent");
                _gunLineRenderer = _gunLineObject.AddComponent<LineRenderer>();
                _gunLineMaterial = new Material(Main.UberShader);
                _gunLineRenderer.material = _gunLineMaterial;
                _gunLineRenderer.useWorldSpace = true;
                _gunLineHost = _gunLineObject.AddComponent<GunLib>();
                _gunLineCoroutine = null;
            }
            return _gunLineRenderer;
        }

        private static void UpdateGunLine(Vector3 handPos, Vector3 spherePos, Vector3 smoothMid)
        {
            var lineRenderer = GetOrCreateGunLine();
            lineRenderer.startWidth = GunLineWidth;
            lineRenderer.endWidth = GunLineWidth;
            lineRenderer.startColor = LineColor;
            lineRenderer.endColor = LineColor;
            _gunLineMaterial.color = LineColor;
            _gunLineObject.SetActive(true);

            lineRenderer.startColor = Color.Lerp(new Color(0.235f, 0f, 0.784f), new Color(0.235f, 0f, 0.784f), Mathf.PingPong(Time.time * 1, 0.5f));
            lineRenderer.endColor = lineRenderer.startColor;
        }

        private static void HideGunLine()
        {
            if (_gunLineObject != null)
                _gunLineObject.SetActive(false);
        }

        private static int? noInvisLayerMask;
        public static int NoInvisLayerMask()
        {
            noInvisLayerMask ??= ~(
                (1 << LayerMask.NameToLayer("TransparentFX")) |
                (1 << LayerMask.NameToLayer("Ignore Raycast")) |
                (1 << LayerMask.NameToLayer("Zone")) |
                (1 << LayerMask.NameToLayer("Gorilla Trigger")) |
                (1 << LayerMask.NameToLayer("Gorilla Boundary")) |
                (1 << LayerMask.NameToLayer("GorillaCosmetics")) |
                (1 << LayerMask.NameToLayer("GorillaParticle"))
            );

            return noInvisLayerMask ?? GTPlayer.Instance.locomotionEnabledLayers;
        }

        public static void GunPreview()
        {
            StartPointerSystem(() => { }, false);
        }

        public static void GunPreviewLock()
        {
            StartPointerSystem(() => { }, true);
        }

        public static void SetSphereSize(float newSize)
        {
            SphereSize = Mathf.Clamp(newSize, 0.01f, 0.3f);
            if (spherepointer != null)
                spherepointer.transform.localScale = Vector3.one * SphereSize;
        }

        public static void SetGunLineWidth(float newWidth)
        {
            GunLineWidth = Mathf.Clamp(newWidth, 0.001f, 0.1f);
        }

        

        
        /// <summary>
        /// Resolve a VRRig from a raycast hit by preferring player hitboxes
        /// (body collider / head / hand colliders) then parent search.
        /// </summary>
        public static VRRig ResolvePlayerFromHit(RaycastHit hit)
        {
            if (hit.collider == null) return null;

            // Direct VRRig on collider or parents
            VRRig rig = hit.collider.GetComponentInParent<VRRig>();
            if (rig != null && rig != GorillaTagger.Instance.offlineVRRig)
                return rig;

            // Body / trigger colliders often sit under GTPlayer hierarchy
            var body = hit.collider.GetComponentInParent<GorillaBodyRenderer>();
            if (body != null)
            {
                rig = body.GetComponentInParent<VRRig>();
                if (rig != null && rig != GorillaTagger.Instance.offlineVRRig)
                    return rig;
            }

            // Sphere overlap fallback at hit point — catch nearby player hitbox
            Collider[] cols = Physics.OverlapSphere(hit.point, 0.45f, ~0, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            VRRig bestRig = null;
            foreach (var c in cols)
            {
                if (c == null) continue;
                VRRig r = c.GetComponentInParent<VRRig>();
                if (r == null || r == GorillaTagger.Instance.offlineVRRig) continue;
                float d = Vector3.Distance(hit.point, r.transform.position);
                if (d < best)
                {
                    best = d;
                    bestRig = r;
                }
            }
            return bestRig;
        }

        /// <summary>
        /// Snap lock pointer to the player's primary hitbox (body center).
        /// </summary>
        public static Vector3 GetPlayerHitboxPoint(VRRig rig)
        {
            if (rig == null) return Vector3.zero;
            try
            {
                if (rig.headMesh != null)
                    return rig.headMesh.transform.position;
            }
            catch { }
            try
            {
                if (rig.bodyRenderer != null)
                    return rig.bodyRenderer.transform.position;
            }
            catch { }
            return rig.transform.position;
        }


        public static void StartVrGunR(Action action, bool LockOn)
        {
            if (ControllerInputPoller.instance.rightGrab)
            {
                isLocked = false;
                Physics.Raycast(GorillaTagger.Instance.rightHandTransform.position, -GorillaTagger.Instance.rightHandTransform.up, out raycastHit, 512f, NoInvisLayerMask());
                if (spherepointer == null)
                {
                    spherepointer = LineLib.CreateSphere(raycastHit.point, SphereSize, LineColor, true);
                    lr = GorillaTagger.Instance.offlineVRRig.rightHandTransform.position;
                }
                if (LockedPlayer == null)
                {
                    spherepointer.transform.position = raycastHit.point;
                    spherepointer.GetComponent<Renderer>().material.color = LineColor;
                    spherepointer.GetComponent<RainbowObject>();
                    isLocked = false;
                }
                else
                {
                    spherepointer.transform.position = GetPlayerHitboxPoint(LockedPlayer);
                }

                Vector3 handPos = GorillaTagger.Instance.rightHandTransform.position;
                Vector3 spherePos = spherepointer.transform.position;

                lr = Vector3.Lerp(lr, (handPos + spherePos) / 2f, Time.deltaTime * 6f);

                UpdateGunLine(handPos, spherePos, lr);

                if (ControllerInputPoller.instance.rightControllerIndexFloat > 0.5f)
                {
                    trigger = true;
                    if (LockOn)
                    {
                        if (LockedPlayer == null)
                        {
                            LockedPlayer = ResolvePlayerFromHit(raycastHit);
                        }
                        if (LockedPlayer != null)
                        {
                            spherepointer.transform.position = GetPlayerHitboxPoint(LockedPlayer);
                            action();
                            isLocked = true;
                        }
                        return;
                    }
                    action();
                    return;
                }
                else
                {
                    trigger = false;
                    if (!StickyLock && LockedPlayer != null)
                    {
                        LockedPlayer = null;
                        return;
                    }
                }
            }
            else
            {
                HideGunLine();
                if (spherepointer != null)
                {
                    LineLib.DestroySphere(spherepointer);
                    spherepointer = null;
                    LockedPlayer = null;
                }
            }
        }

        public static void StartVrGunL(Action action, bool LockOn)
        {
            if (ControllerInputPoller.instance.leftGrab)
            {
                Physics.Raycast(GorillaTagger.Instance.leftHandTransform.position, -GorillaTagger.Instance.leftHandTransform.up, out raycastHit, 512f, NoInvisLayerMask());
                if (spherepointer == null)
                {
                    spherepointer = LineLib.CreateSphere(raycastHit.point, SphereSize, LineColor, true);
                    lr = GorillaTagger.Instance.offlineVRRig.leftHandTransform.position;
                }
                if (LockedPlayer == null)
                {
                    spherepointer.transform.position = raycastHit.point;
                    spherepointer.GetComponent<Renderer>().material.color = LineColor;
                }
                else
                {
                    spherepointer.transform.position = GetPlayerHitboxPoint(LockedPlayer);
                }

                Vector3 handPos = GorillaTagger.Instance.leftHandTransform.position;
                Vector3 spherePos = spherepointer.transform.position;

                lr = Vector3.Lerp(lr, (handPos + spherePos) / 2f, Time.deltaTime * 6f);

                UpdateGunLine(handPos, spherePos, lr);

                if (ControllerInputPoller.instance.leftControllerIndexFloat > 0.5f)
                {
                    trigger = true;
                    if (LockOn)
                    {
                        if (LockedPlayer == null)
                        {
                            LockedPlayer = ResolvePlayerFromHit(raycastHit);
                        }
                        if (LockedPlayer != null)
                        {
                            spherepointer.transform.position = GetPlayerHitboxPoint(LockedPlayer);
                            action();
                            isLocked = true;
                        }
                        return;
                    }
                    action();
                    return;
                }
                else if (LockedPlayer != null)
                {
                    LockedPlayer = null;
                    return;
                }
            }
            else
            {
                HideGunLine();
                if (spherepointer != null)
                {
                    LineLib.DestroySphere(spherepointer);
                    spherepointer = null;
                    LockedPlayer = null;
                }
            }
        }

        public static void StartPcGun(Action action, bool LockOn)
        {
            Ray ray = GameObject.Find("Shoulder Camera").activeSelf ? GameObject.Find("Shoulder Camera").GetComponent<Camera>().ScreenPointToRay(UnityInput.Current.mousePosition) : GorillaTagger.Instance.mainCamera.GetComponent<Camera>().ScreenPointToRay(UnityInput.Current.mousePosition);
            if (Mouse.current.rightButton.isPressed)
            {
                RaycastHit raycastHit;
                if (Physics.Raycast(ray.origin, ray.direction, out raycastHit, 512f, NoInvisLayerMask()) && spherepointer == null)
                {
                    if (spherepointer == null)
                    {
                        spherepointer = LineLib.CreateSphere(raycastHit.point, SphereSize, LineColor, true);
                        lr = GorillaTagger.Instance.offlineVRRig.rightHandTransform.position;
                    }
                }
                if (LockedPlayer == null)
                {
                    spherepointer.transform.position = raycastHit.point;
                    spherepointer.GetComponent<Renderer>().material.color = LineColor;
                }
                else
                {
                    spherepointer.transform.position = GetPlayerHitboxPoint(LockedPlayer);
                }

                Vector3 handPos = GorillaTagger.Instance.rightHandTransform.position;
                Vector3 spherePos = spherepointer.transform.position;

                lr = Vector3.Lerp(lr, (handPos + spherePos) / 2f, Time.deltaTime * 6f);

                UpdateGunLine(handPos, spherePos, lr);

                if (Mouse.current.leftButton.isPressed)
                {
                    trigger = true;
                    if (LockOn)
                    {
                        if (LockedPlayer == null)
                        {
                            LockedPlayer = ResolvePlayerFromHit(raycastHit);
                        }
                        if (LockedPlayer != null)
                        {
                            spherepointer.transform.position = GetPlayerHitboxPoint(LockedPlayer);
                            action();
                            isLocked = true;
                        }
                        return;
                    }
                    action();
                    return;
                }
                else
                {
                    trigger = false;
                    if (LockedPlayer != null)
                    {
                        LockedPlayer = null;
                        return;
                    }
                }
            }
            else
            {
                HideGunLine();
                if (spherepointer != null)
                {
                    LineLib.DestroySphere(spherepointer);
                    spherepointer = null;
                    LockedPlayer = null;
                }
            }
        }

        public static void StartPointerSystem(Action action, bool locko)
        {
            if (XRSettings.isDeviceActive)
            {
                if (gunLeft == true)
                {
                    StartVrGunL(action, locko);
                }
                else
                {
                    StartVrGunR(action, locko);
                }
            }
            if (!XRSettings.isDeviceActive)
            {
                StartPcGun(action, locko);
            }
        }

        public static bool trigger = false;
        public static bool gunLeft = false;
        public static bool StickyLock = true; // keep lock while grip held
    }
}
